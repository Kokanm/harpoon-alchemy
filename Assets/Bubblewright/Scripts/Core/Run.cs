using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One run, from stage 1 until the flame goes out or the last stage is cleared. Permadeath: nothing
    /// carries over to the next run except the best score. Hearts, score and upgrades live here, and the
    /// game copies the upgrade numbers onto Wick and the harpoons at the start of every stage.
    /// </summary>
    public class Run
    {
        public int Stage = 1;
        public int Score;
        public int Hearts;
        public int MaxHearts;
        public readonly int Seed;
        public readonly System.Random Rng;

        // Upgrade numbers, all neutral at the start of a run.
        public float SpeedMultiplier = 1f;
        public float HarpoonSpeedMultiplier = 1f;
        public float HarpoonWidthMultiplier = 1f;
        public float CableHold;
        public float ExtraInvulnerable;
        public int Shots = 1;

        readonly List<Upgrade> taken = new List<Upgrade>();

        public Run(int seed)
        {
            Seed = seed;
            Rng = new System.Random(seed);
            MaxHearts = Hearts = Tuning.I.hearts;
        }

        public bool IsLastStage => Stage >= Tuning.I.runLength;

        public int TimesTaken(Upgrade u)
        {
            int n = 0;
            foreach (var t in taken) if (t == u) n++;
            return n;
        }

        public void Take(Upgrade u)
        {
            u.Apply(this);
            taken.Add(u);
        }

        /// <summary>"Twin Cable, Swift Boots x2". Heals are not part of the build.</summary>
        public string BuildSummary()
        {
            var parts = new List<string>();
            foreach (var u in Upgrade.All)
            {
                if (u.Max == 0) continue;
                int n = TimesTaken(u);
                if (n > 0) parts.Add(n > 1 ? $"{u.Name} x{n}" : u.Name);
            }
            return parts.Count == 0 ? "nothing yet" : string.Join(", ", parts);
        }

        /// <summary>Up to three different upgrades this run can still take, in random order.</summary>
        public List<Upgrade> RollChoices(int count = 3)
        {
            var pool = new List<Upgrade>();
            foreach (var u in Upgrade.All) if (u.CanOffer(this)) pool.Add(u);
            var picks = new List<Upgrade>();
            while (picks.Count < count && pool.Count > 0)
            {
                int i = Rng.Next(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }
    }

    /// <summary>
    /// A pick between stages. Placeholder set: every upgrade changes a number the prototype already has,
    /// so none of them needs new art. Max 0 means it can be taken any number of times and is not listed in the build.
    /// </summary>
    public class Upgrade
    {
        public string Name;
        public string Text;
        public int Max = 1;
        public Func<Run, bool> Extra = _ => true;
        public Action<Run> Apply;

        public bool CanOffer(Run r) => (Max == 0 || r.TimesTaken(this) < Max) && Extra(r);

        public static readonly Upgrade[] All =
        {
            new Upgrade { Name = "Twin Cable", Text = "One more harpoon on screen at a time.", Max = 2, Apply = r => r.Shots++ },
            new Upgrade { Name = "Anchor Tip", Text = "The tip hooks into the ceiling and the cable stays for 1.5 s.", Apply = r => r.CableHold = 1.5f },
            new Upgrade { Name = "Quick Winch", Text = "Harpoons fly 30% faster.", Max = 2, Apply = r => r.HarpoonSpeedMultiplier += 0.3f },
            new Upgrade { Name = "Wide Barbs", Text = "Harpoons hit bubbles from twice as far to the side.", Apply = r => r.HarpoonWidthMultiplier = 2f },
            new Upgrade { Name = "Swift Boots", Text = "Run 12% faster.", Max = 3, Apply = r => r.SpeedMultiplier += 0.12f },
            new Upgrade { Name = "Long Glow", Text = "Stay safe 0.75 s longer after a hit.", Max = 2, Apply = r => r.ExtraInvulnerable += 0.75f },
            new Upgrade { Name = "Spare Wick", Text = "One more heart, and it starts lit.", Max = 2, Apply = r => { r.MaxHearts++; r.Hearts++; } },
            new Upgrade { Name = "Relight", Text = "Get back one lost heart.", Max = 0, Extra = r => r.Hearts < r.MaxHearts, Apply = r => r.Hearts++ },
        };
    }

    /// <summary>
    /// Builds a stage from the sheet's bubble budget: each stage gets a budget that grows by stage, and spends it
    /// on bubbles (XL 8, L 4, M 2, S 1, times a cost per kind). Trigloops and Leads join in later stages.
    /// </summary>
    public static class StageGenerator
    {
        public static float Budget(int stage)
        {
            var t = Tuning.I;
            return t.budgetStart + t.budgetGrowth * (stage - 1);
        }

        public static List<SpawnSpec> Generate(int stage, System.Random rng)
        {
            var t = Tuning.I;
            float budget = Budget(stage);
            var picks = new List<(BubbleKind kind, int tier)>();

            while (picks.Count < t.maxStartBubbles)
            {
                var kind = PickKind(stage, rng);
                int tier = BiggestAffordable(kind, budget);
                if (tier < 0 && kind != BubbleKind.Gloop)
                {
                    kind = BubbleKind.Gloop;
                    tier = BiggestAffordable(kind, budget);
                }
                if (tier < 0) break;
                // After the first bubble, sometimes go one size down so stages are not all XLs.
                if (picks.Count > 0 && tier < t.tiers.Length - 1 && rng.NextDouble() < 0.35) tier++;
                budget -= Cost(kind, tier);
                picks.Add((kind, tier));
            }
            if (stage > 1 && rng.NextDouble() < t.luckyChance) picks.Add((BubbleKind.Lucky, 2));

            // Biggest first, spread out to both sides and away from Wick, who starts in the middle.
            picks.Sort((a, b) => a.tier.CompareTo(b.tier));
            var specs = new List<SpawnSpec>();
            int side = rng.NextDouble() < 0.5 ? -1 : 1;
            int perSide = Mathf.CeilToInt(picks.Count / 2f);
            float inner = 5f, outer = t.HalfWidth - 2.5f;
            for (int i = 0; i < picks.Count; i++)
            {
                var (kind, tier) = picks[i];
                float r = t.tiers[tier].diameter * 0.5f;
                int rank = i / 2;
                float x = side * Mathf.Lerp(inner, outer, (rank + 0.5f) / perSide) + (float)(rng.NextDouble() * 2.0 - 1.0);
                float y = Mathf.Min(7f + (float)rng.NextDouble() * 2f, t.arenaHeight - r - 0.5f);
                specs.Add(new SpawnSpec { kind = kind, tier = tier, position = new Vector2(x, y), direction = rng.NextDouble() < 0.5 ? -1 : 1 });
                side = -side;
            }
            return specs;
        }

        static BubbleKind PickKind(int stage, System.Random rng)
        {
            var t = Tuning.I;
            double roll = rng.NextDouble();
            if (stage >= t.leadFromStage && roll < 0.25) return BubbleKind.Lead;
            if (stage >= t.trigloopFromStage && roll > 0.65) return BubbleKind.Trigloop;
            return BubbleKind.Gloop;
        }

        static float Cost(BubbleKind kind, int tier)
        {
            var t = Tuning.I;
            float scale = (int)kind < t.kindCostScale.Length ? t.kindCostScale[(int)kind] : 1f;
            return t.tierCost[Mathf.Min(tier, t.tierCost.Length - 1)] * scale;
        }

        static int BiggestAffordable(BubbleKind kind, float budget)
        {
            for (int tier = 0; tier < Tuning.I.tiers.Length; tier++)
                if (Cost(kind, tier) <= budget + 0.001f) return tier;
            return -1;
        }
    }
}

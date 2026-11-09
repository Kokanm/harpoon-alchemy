using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Named pairwise Cable-Fusions unlocked automatically when a run holds both parent upgrades.
    /// </summary>
    public enum FusionKind
    {
        TripwireGate,   // Twin Cable + Anchor Tip
        ArcTwin,        // Twin Cable + Spark Wire
        VolleyLoom,     // Twin Cable + Cross-Stitch
        TwinFlasks,     // Twin Cable + Alkahest
        TeslaRod,       // Anchor Tip + Spark Wire
        CeilingLoom,    // Anchor Tip + Cross-Stitch
        AcidDrip,       // Anchor Tip + Alkahest
        StormLance,     // Wide Barbs + Spark Wire
        SawbladeBarbs,  // Wide Barbs + Cross-Stitch
        CausticCloud,   // Wide Barbs + Alkahest
    }

    public readonly struct FusionSpec
    {
        public readonly FusionKind Kind;
        public readonly string Name;
        public readonly string FirstUpgrade;
        public readonly string SecondUpgrade;
        public readonly string Description;

        public FusionSpec(FusionKind kind, string name, string first, string second, string description)
        {
            Kind = kind;
            Name = name;
            FirstUpgrade = first;
            SecondUpgrade = second;
            Description = description;
        }
    }

    /// <summary>
    /// One run, from stage 1 until the flame goes out or the last stage is cleared. Permadeath: nothing
    /// carries over to the next run except the best score. Hearts, score, upgrades and active cable fusions
    /// live here, and the game copies the upgrade numbers onto Wick and the harpoons at the start of every stage.
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

        // Harpoon Alchemy payload ranks.
        public int SparkRank;
        public int NeedleRank;
        public int EmberRank;

        // Run-level meta/monetization state (1 free card reroll, 1 rewarded ad reroll, 1 rewarded heart relight).
        public int FreeRerollsLeft = 1;
        public bool AdRerollUsed;
        public bool RelightAdUsed;

        readonly List<Upgrade> taken = new List<Upgrade>();

        public static readonly FusionSpec[] AllFusions =
        {
            new FusionSpec(FusionKind.TripwireGate,  "Tripwire Gate",  "Twin Cable", "Anchor Tip",   "Two anchored cables snap a horizontal wire between them."),
            new FusionSpec(FusionKind.ArcTwin,       "Arc Twin",       "Twin Cable", "Spark Wire",   "Active twin cables zap bubbles between their lines."),
            new FusionSpec(FusionKind.VolleyLoom,    "Volley Loom",    "Twin Cable", "Cross-Stitch", "Harpoon pops fire a double horizontal needle burst."),
            new FusionSpec(FusionKind.TwinFlasks,    "Twin Flasks",    "Twin Cable", "Alkahest",     "Pops release two counter-drifting acid embers."),
            new FusionSpec(FusionKind.TeslaRod,      "Tesla Rod",      "Anchor Tip", "Spark Wire",   "Anchored ceiling cables pulse lightning every 0.45 s."),
            new FusionSpec(FusionKind.CeilingLoom,   "Ceiling Loom",   "Anchor Tip", "Cross-Stitch", "Ceiling hooks fire needles at both top and mid-arena."),
            new FusionSpec(FusionKind.AcidDrip,      "Acid Drip",      "Anchor Tip", "Alkahest",     "Hooked ceiling cablesspawn a rising ember at the floor."),
            new FusionSpec(FusionKind.StormLance,    "Storm Lance",    "Wide Barbs", "Spark Wire",   "Chain lightning reaches +3.5 u further and jumps twice."),
            new FusionSpec(FusionKind.SawbladeBarbs, "Sawblade Barbs", "Wide Barbs", "Cross-Stitch", "Horizontal needles are 2.5x wider and pierce 2 bubbles."),
            new FusionSpec(FusionKind.CausticCloud,  "Caustic Cloud",  "Wide Barbs", "Alkahest",     "Alkahest embers swell to 1.8x radius and last longer."),
        };

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

        public int TimesTaken(string upgradeName)
        {
            int n = 0;
            foreach (var t in taken) if (t.Name == upgradeName) n++;
            return n;
        }

        public void Take(Upgrade u)
        {
            u.Apply(this);
            taken.Add(u);
        }

        public bool HasFusion(FusionKind kind)
        {
            foreach (var f in AllFusions)
            {
                if (f.Kind != kind) continue;
                return TimesTaken(f.FirstUpgrade) > 0 && TimesTaken(f.SecondUpgrade) > 0;
            }
            return false;
        }

        public List<FusionSpec> ActiveFusions()
        {
            var list = new List<FusionSpec>();
            foreach (var f in AllFusions)
                if (TimesTaken(f.FirstUpgrade) > 0 && TimesTaken(f.SecondUpgrade) > 0)
                    list.Add(f);
            return list;
        }

        /// <summary>
        /// Returns the first new Fusion that picking candidate would immediately unlock with an already-held upgrade,
        /// or null if picking candidate does not complete a new pair.
        /// </summary>
        public FusionSpec? PreviewFusion(Upgrade candidate)
        {
            if (candidate == null || TimesTaken(candidate) > 0) return null;
            foreach (var f in AllFusions)
            {
                if (f.FirstUpgrade == candidate.Name && TimesTaken(f.SecondUpgrade) > 0) return f;
                if (f.SecondUpgrade == candidate.Name && TimesTaken(f.FirstUpgrade) > 0) return f;
            }
            return null;
        }

        /// <summary>"Twin Cable, Spark Wire [Arc Twin]". Heals are not part of the build.</summary>
        public string BuildSummary()
        {
            var parts = new List<string>();
            foreach (var u in Upgrade.All)
            {
                if (u.Max == 0) continue;
                int n = TimesTaken(u);
                if (n > 0) parts.Add(n > 1 ? $"{u.Name} x{n}" : u.Name);
            }
            var fusions = ActiveFusions();
            foreach (var f in fusions) parts.Add($"[{f.Name}]");
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
    /// A pick between stages. Harpoon Alchemy set: 3 structural cable cards, 3 elemental/split payload cards
    /// (which fuse pairwise with the structural cards), and 2 survival/utility cards.
    /// Max 0 means it can be taken any number of times and is not listed in the build.
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
            // Structural cards (3)
            new Upgrade { Name = "Twin Cable",   Text = "One more harpoon on screen at a time.", Max = 2, Apply = r => r.Shots++ },
            new Upgrade { Name = "Anchor Tip",   Text = "The tip hooks into the ceiling and the cable stays for 1.5 s.", Apply = r => r.CableHold = 1.5f },
            new Upgrade { Name = "Wide Barbs",   Text = "Harpoons hit bubbles from twice as far to the side.", Apply = r => r.HarpoonWidthMultiplier = 2f },

            // Payload cards (3) — fuse pairwise with Structural cards
            new Upgrade { Name = "Spark Wire",   Text = "Popping a bubble arcs lightning to zap a nearby bubble.", Max = 2, Apply = r => r.SparkRank++ },
            new Upgrade { Name = "Cross-Stitch", Text = "Pops and ceiling hits fire horizontal needles left and right.", Max = 2, Apply = r => r.NeedleRank++ },
            new Upgrade { Name = "Alkahest",     Text = "Popped bubbles release a rising acid ember that pops another.", Max = 2, Apply = r => r.EmberRank++ },

            // Utility & survival cards (2)
            new Upgrade { Name = "Swift Boots",  Text = "Run 15% faster and stay safe 0.6 s longer after a hit.", Max = 2, Apply = r => { r.SpeedMultiplier += 0.15f; r.ExtraInvulnerable += 0.6f; } },
            new Upgrade { Name = "Relight",      Text = "Get back one lost heart.", Max = 0, Extra = r => r.Hearts < r.MaxHearts, Apply = r => r.Hearts++ },
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

        /// <summary>Total pops required to completely clear a single bubble of (kind, tier) and all its descendants.</summary>
        public static int TotalPopsForBubble(BubbleKind kind, int tier)
        {
            var t = Tuning.I;
            int tiersRemaining = Mathf.Max(0, t.tiers.Length - 1 - tier);
            int children = Mathf.Max(1, t.Kind(kind).children);
            int total = 1;
            int layer = 1;
            for (int i = 0; i < tiersRemaining; i++)
            {
                layer *= children;
                total += layer;
            }
            return total;
        }

        /// <summary>Weighted difficulty score for a generated stage (total pops weighted by bubble horizontal speed).</summary>
        public static float StageDifficultyScore(List<SpawnSpec> specs)
        {
            var t = Tuning.I;
            float score = 0f;
            foreach (var s in specs)
            {
                if (s.kind == BubbleKind.Lucky) continue;
                int pops = TotalPopsForBubble(s.kind, s.tier);
                float speedWeight = t.Kind(s.kind).speedScale;
                score += pops * speedWeight;
            }
            return score;
        }

        /// <summary>
        /// Validates that a generated stage has at least 1 non-Lucky bubble, stays inside arena walls/ceiling,
        /// and keeps a safe horizontal spawn corridor around Wick at x = 0.
        /// </summary>
        public static bool ValidateStage(List<SpawnSpec> specs, out string reason)
        {
            var t = Tuning.I;
            if (specs == null || specs.Count == 0)
            {
                reason = "Stage spawned zero bubbles.";
                return false;
            }
            int combatBubbles = 0;
            foreach (var s in specs)
            {
                if (s.kind != BubbleKind.Lucky) combatBubbles++;
                float r = t.tiers[Mathf.Clamp(s.tier, 0, t.tiers.Length - 1)].diameter * 0.5f;
                if (Mathf.Abs(s.position.x) + r > t.HalfWidth + 0.01f)
                {
                    reason = $"Bubble {s.kind} at x={s.position.x:F2} overlaps wall.";
                    return false;
                }
                if (s.position.y + r > t.arenaHeight + 0.01f || s.position.y - r <= 0f)
                {
                    reason = $"Bubble {s.kind} at y={s.position.y:F2} out of vertical bounds.";
                    return false;
                }
                if (Mathf.Abs(s.position.x) < 3.5f)
                {
                    reason = $"Bubble {s.kind} at x={s.position.x:F2} spawns too close to Wick at x=0.";
                    return false;
                }
            }
            if (combatBubbles == 0)
            {
                reason = "Stage has no combat bubbles.";
                return false;
            }
            reason = null;
            return true;
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

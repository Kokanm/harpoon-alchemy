using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// M0–M3 verification tests for Harpoon Alchemy:
    /// - 200-seed stage validation & rising difficulty curve
    /// - Pairwise Cable-Fusion matrix activation & preview badges
    /// - Stubbed monetization & analytics service hooks
    /// </summary>
    public class HarpoonAlchemyTests
    {
        [Test]
        public void StageGenerator_200Seeds_AllStagesValidAndDifficultyRises()
        {
            const int seeds = 200;
            int stages = Tuning.I.runLength;
            float[] avgDifficulty = new float[stages];

            for (int seed = 1; seed <= seeds; seed++)
            {
                var rng = new System.Random(seed);
                for (int stage = 1; stage <= stages; stage++)
                {
                    var specs = StageGenerator.Generate(stage, rng);
                    bool valid = StageGenerator.ValidateStage(specs, out string reason);
                    Assert.IsTrue(valid, $"Seed {seed} Stage {stage} failed validation: {reason}");
                    avgDifficulty[stage - 1] += StageGenerator.StageDifficultyScore(specs);
                }
            }

            for (int i = 0; i < stages; i++) avgDifficulty[i] /= seeds;
            for (int i = 1; i < stages; i++)
            {
                Assert.Greater(avgDifficulty[i], avgDifficulty[i - 1],
                    $"Average stage difficulty did not rise from Stage {i} ({avgDifficulty[i - 1]:F1}) to Stage {i + 1} ({avgDifficulty[i]:F1})");
            }
        }

        [Test]
        public void EveryPairwiseFusion_UnlocksAndPreviewsCorrectly()
        {
            Assert.AreEqual(10, Run.AllFusions.Length);

            foreach (var f in Run.AllFusions)
            {
                var run = new Run(42);
                var first = Upgrade.All.FirstOrDefault(u => u.Name == f.FirstUpgrade);
                var second = Upgrade.All.FirstOrDefault(u => u.Name == f.SecondUpgrade);

                Assert.IsNotNull(first, $"Missing upgrade {f.FirstUpgrade} for fusion {f.Name}");
                Assert.IsNotNull(second, $"Missing upgrade {f.SecondUpgrade} for fusion {f.Name}");
                Assert.IsFalse(run.HasFusion(f.Kind));

                // Taking the first upgrade should make the second upgrade preview this fusion.
                run.Take(first);
                Assert.IsFalse(run.HasFusion(f.Kind));
                var preview = run.PreviewFusion(second);
                Assert.IsTrue(preview.HasValue, $"{second.Name} should preview a fusion when {first.Name} is held");
                Assert.AreEqual(f.Kind, preview.Value.Kind);

                // Taking the second upgrade activates the fusion and includes it in BuildSummary.
                run.Take(second);
                Assert.IsTrue(run.HasFusion(f.Kind), $"Fusion {f.Name} should be active after taking {first.Name} + {second.Name}");
                StringAssert.Contains($"[{f.Name}]", run.BuildSummary());
            }
        }

        [Test]
        public void StubAdAndAnalyticsServices_RecordRewardedAndEventFlows()
        {
            GameServices.ResetToStubs();
            var ads = (StubAdService)GameServices.Ads;
            var analytics = (StubAnalyticsService)GameServices.Analytics;

            bool rewardedGranted = false;
            GameServices.Ads.ShowRewarded("relight_heart", () => rewardedGranted = true);

            Assert.IsTrue(rewardedGranted);
            Assert.AreEqual(1, ads.RewardedShownCount);
            Assert.AreEqual("relight_heart", ads.LastPlacement);
            Assert.IsTrue(analytics.Events.Any(e => e.Name == "ad_rewarded_shown"));
        }
    }
}

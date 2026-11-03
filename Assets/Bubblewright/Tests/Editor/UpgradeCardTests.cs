using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Upgrade cards: every upgrade has its own readable icon, and its text fits the card.</summary>
    public class UpgradeCardTests
    {
        [Test]
        public void EveryUpgradeHasItsOwnIcon()
        {
            var fallback = PlaceholderArt.I.UpgradeIcon("(no such upgrade)").GetPixels32();
            var seen = new System.Collections.Generic.List<(string name, Color32[] pixels)>();
            foreach (var u in Upgrade.All)
            {
                var icon = PlaceholderArt.I.UpgradeIcon(u.Name);
                Assert.IsNotNull(icon, u.Name);
                Assert.AreEqual(PlaceholderArt.UpgradeIconSize, icon.width, u.Name);
                Assert.AreEqual(FilterMode.Point, icon.filterMode, $"{u.Name} icon must stay pixel sharp");
                var pixels = icon.GetPixels32();
                Assert.IsFalse(Same(pixels, fallback), $"{u.Name} has no icon of its own, it uses the fallback");
                foreach (var other in seen)
                    Assert.IsFalse(Same(pixels, other.pixels), $"{u.Name} and {other.name} share an icon");
                seen.Add((u.Name, pixels));
            }
        }

        [Test]
        public void EveryIconStandsOutFromItsBox()
        {
            // Icons sit on an ink box. Most of the drawing (not counting the ink outline) must reach 3:1 against it.
            foreach (var u in Upgrade.All)
            {
                var drawn = PlaceholderArt.I.UpgradeIcon(u.Name).GetPixels32().Where(p => p.a > 0 && !Equal(p, Palette.Ink)).ToArray();
                Assert.Greater(drawn.Length, 20, $"{u.Name} icon is nearly empty");
                float visible = drawn.Count(p => UiText.Contrast(p, Palette.Ink) >= UiText.MinControlContrast) / (float)drawn.Length;
                Assert.GreaterOrEqual(visible, 0.8f, $"{u.Name}: only {visible:P0} of the icon stands out from the ink box");
            }
        }

        [Test]
        public void CardTextFits()
        {
            foreach (var u in Upgrade.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(u.Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(u.Text), $"{u.Name} has no description");
                Assert.LessOrEqual(u.Name.Length, 12, $"{u.Name}: name too long for the space beside the icon");
                Assert.LessOrEqual(u.Text.Length, 72, $"{u.Name}: description too long for four lines on the card");
            }
        }

        static bool Equal(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        static bool Same(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!Equal(a[i], b[i])) return false;
            return true;
        }
    }
}

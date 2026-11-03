using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// Text contrast checks against WCAG AA. Each pair is a text colour the game draws and the background it is
    /// drawn on (see UiText). Run from Window > General > Test Runner > EditMode.
    /// </summary>
    public class ReadabilityTests
    {
        static IEnumerable<TestCaseData> TextPairs()
        {
            foreach (var (name, text, background) in UiText.TextPairs())
                yield return new TestCaseData(text, background).SetName($"Text readable: {name}");
        }

        static IEnumerable<TestCaseData> ControlPairs()
        {
            foreach (var (name, edge, background) in UiText.ControlPairs())
                yield return new TestCaseData(edge, background).SetName($"Control visible: {name}");
        }

        [TestCaseSource(nameof(TextPairs))]
        public void TextMeetsAAContrast(Color text, Color background)
        {
            float ratio = UiText.Contrast(text, background);
            Assert.GreaterOrEqual(ratio, UiText.MinTextContrast,
                $"Contrast {ratio:0.00}:1 is below {UiText.MinTextContrast}:1 (text #{ColorUtility.ToHtmlStringRGB(text)} on #{ColorUtility.ToHtmlStringRGB(background)})");
        }

        [TestCaseSource(nameof(ControlPairs))]
        public void ControlEdgeMeetsAAContrast(Color edge, Color background)
        {
            float ratio = UiText.Contrast(edge, background);
            Assert.GreaterOrEqual(ratio, UiText.MinControlContrast,
                $"Contrast {ratio:0.00}:1 is below {UiText.MinControlContrast}:1");
        }

        [Test]
        public void ContrastMatchesWcagReferenceValues()
        {
            Assert.AreEqual(21f, UiText.Contrast(Color.black, Color.white), 0.01f);
            Assert.AreEqual(1f, UiText.Contrast(Palette.Dusk, Palette.Dusk), 0.001f);
            Assert.AreEqual(UiText.Contrast(Palette.Cream, Palette.Ink), UiText.Contrast(Palette.Ink, Palette.Cream), 0.001f);
            // #767676 on white is the classic "just passes AA" grey.
            Assert.AreEqual(4.54f, UiText.Contrast(new Color32(0x76, 0x76, 0x76, 0xFF), Color.white), 0.01f);
        }

        [Test]
        public void PlainLilacIsTooFaintOnTheSky()
        {
            // The reason Mist exists: guards against switching secondary text back to lilac.
            Assert.Less(UiText.Contrast(Palette.Lilac, UiText.SkyWorst), UiText.MinTextContrast);
            Assert.AreNotEqual((Color)Palette.Lilac, (Color)UiText.Secondary);
        }
    }
}

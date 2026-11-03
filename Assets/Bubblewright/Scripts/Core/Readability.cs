using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The text colours the HUD and menus use, the backgrounds they sit on, and the WCAG contrast maths.
    /// The game draws with these, and the accessibility tests check every pair, so a palette change that makes
    /// text hard to read fails a test instead of shipping.
    /// </summary>
    public static class UiText
    {
        // Text roles
        public static readonly Color32 Primary = Palette.Cream;   // banners, titles, numbers
        public static readonly Color32 Secondary = Palette.Mist;  // prompts, hints, descriptions
        public static readonly Color32 Accent = Palette.Mint;     // score lines, the highlighted card
        public static readonly Color32 Warning = Palette.Lemon;   // storm timer, storm banner, active cheats

        // How dark the backing panels are. Plates sit behind banner text; Dim covers the stage under the upgrade cards.
        public const float PlateAlpha = 0.8f;
        public const float DimAlpha = 0.7f;
        public const float TimerTrackAlpha = 0.6f;

        // Backgrounds, worst case first. The sky is dithered deep and dusk; dusk is the lighter, harder one.
        public static Color SkyWorst => Palette.Dusk;
        public static Color Plate => Over(Palette.Ink, PlateAlpha, SkyWorst);
        public static Color Dimmed => Over(Palette.Ink, DimAlpha, SkyWorst);
        public static Color CardFill => Palette.Deep;
        public static Color CardFillHover => Palette.Mix(Palette.Deep, Palette.Mint, 0.18f);
        public static Color GroundStrip => Palette.Ink;
        public static Color TimerTrack => Over(Palette.Ink, TimerTrackAlpha, SkyWorst);
        public static Color DebugPanel => Over(Palette.Ink, 0.95f, Dimmed);
        public static Color DebugButtonHover => Palette.Dusk;
        public static Color DebugButtonOn => Palette.Mint;

        /// <summary>WCAG AA: 4.5:1 for text, 3:1 for the edges of controls such as a card's border.</summary>
        public const float MinTextContrast = 4.5f;
        public const float MinControlContrast = 3f;

        /// <summary>Every text colour on every background it is drawn on, named for test output.</summary>
        public static IEnumerable<(string name, Color text, Color background)> TextPairs()
        {
            yield return ("HUD stage and score on the sky", Primary, SkyWorst);
            yield return ("Score line on the sky", Accent, SkyWorst);
            yield return ("Banner on a plate", Primary, Plate);
            yield return ("Score line on a plate", Accent, Plate);
            yield return ("Prompt on a plate", Secondary, Plate);
            yield return ("Pick title on the dimmed stage", Primary, Dimmed);
            yield return ("Pick hints on the dimmed stage", Secondary, Dimmed);
            yield return ("Card title", Primary, CardFill);
            yield return ("Card description", Secondary, CardFill);
            yield return ("Card number", Secondary, CardFill);
            yield return ("Hovered card title", Accent, CardFillHover);
            yield return ("Hovered card description", Primary, CardFillHover);
            yield return ("Desktop help in the ground strip", Secondary, GroundStrip);
            yield return ("Build line in the ground strip", Primary, GroundStrip);
            yield return ("Timer warning and cheats on the sky", Warning, SkyWorst);
            yield return ("Storm banner on a plate", Warning, Plate);
            yield return ("Pause button label", Primary, CardFill);
            yield return ("Debug panel title", Warning, DebugPanel);
            yield return ("Debug panel labels", Secondary, DebugPanel);
            yield return ("Debug button", Primary, CardFill);
            yield return ("Debug button under the mouse", Primary, DebugButtonHover);
            yield return ("Debug toggle that is on", Palette.Ink, DebugButtonOn);
        }

        /// <summary>Card borders against the dimmed stage around them.</summary>
        public static IEnumerable<(string name, Color edge, Color background)> ControlPairs()
        {
            yield return ("Card border", Palette.Lilac, Dimmed);
            yield return ("Hovered card border", Palette.Mint, Dimmed);
            yield return ("Pause button border on the sky", Palette.Mist, SkyWorst);
            yield return ("Timer bar", Palette.Mint, TimerTrack);
            yield return ("Timer bar, last seconds", Palette.Lemon, TimerTrack);
            yield return ("Timer bar, storm", Palette.Coral, TimerTrack);
            yield return ("Debug button border on the panel", Palette.Lilac, DebugPanel);
        }

        /// <summary>Colour a with alpha laid over an opaque background.</summary>
        public static Color Over(Color a, float alpha, Color background) => Color.Lerp(background, a, alpha);

        public static float RelativeLuminance(Color c)
        {
            static float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        /// <summary>WCAG contrast ratio, from 1 (same colour) to 21 (black on white).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }
    }
}

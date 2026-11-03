using UnityEngine;

namespace Bubblewright
{
    /// <summary>Colours from the design sheet (section 03).</summary>
    public static class Palette
    {
        // World
        public static readonly Color32 Ink = Hex(0x1A1633);
        public static readonly Color32 Deep = Hex(0x2B2757);
        public static readonly Color32 Dusk = Hex(0x433D85);
        public static readonly Color32 Lilac = Hex(0x8F88D1);
        public static readonly Color32 Cream = Hex(0xFFF3D6);
        // Wick and UI
        public static readonly Color32 Saffron = Hex(0xFFA53A);
        public static readonly Color32 SaffronDark = Hex(0xD9782A);
        public static readonly Color32 SaffronLine = Hex(0xB85A1C);
        public static readonly Color32 Mint = Hex(0x5BE6C1);
        // Weapons
        public static readonly Color32 SteelLight = Hex(0xDCE2EE);
        public static readonly Color32 Steel = Hex(0x9AA4BC);
        public static readonly Color32 SteelDark = Hex(0x5C6480);
        // Bubbles
        public static readonly Color32 Coral = Hex(0xFF6A5C);
        public static readonly Color32 Azure = Hex(0x3FA9FF);
        public static readonly Color32 Plum = Hex(0xC062E8);
        public static readonly Color32 Lemon = Hex(0xF4E85A);

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static Color32 Hex(int rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);

        public static Color32 Shade(Color32 c, float k) =>
            new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);

        public static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);
    }

    /// <summary>Sorting layers from the sheet, back to front.</summary>
    public enum Layer { Sky, Stage, Bubbles, Shots, Player, FX, UI }

    public static class Sorting
    {
        /// <summary>
        /// Uses the real sorting layer when it exists (ProjectSettings/TagManager.asset),
        /// otherwise falls back to Default with a large order offset so the stacking is the same.
        /// </summary>
        public static void Apply(Renderer r, Layer layer, int order = 0)
        {
            int id = SortingLayer.NameToID(layer.ToString());
            if (id != 0 && SortingLayer.IsValid(id))
            {
                r.sortingLayerID = id;
                r.sortingOrder = order;
            }
            else
            {
                r.sortingLayerID = 0;
                r.sortingOrder = (int)layer * 1000 + order;
            }
        }
    }
}

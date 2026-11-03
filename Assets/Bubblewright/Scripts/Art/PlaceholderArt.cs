using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Placeholder sprites painted in code from the design sheet's shapes and palette, at exactly
    /// 15 px per unit with a 1 px ink outline on the outer edge. Real art (SVG exported to PNG) replaces
    /// these later without touching gameplay code: swap the sprites, keep the pivots.
    /// </summary>
    public class PlaceholderArt
    {
        public Sprite WickBack, WickFront, Flame;
        public Sprite[,] Bubbles; // [kind, tier]
        public Sprite[] PopRings; // per tier
        public Sprite HarpoonTip, Rope;
        public Sprite Background;
        public Sprite FireButton, FireButtonPressed, MoveHint, MoveKnob, MoveDot;
        public Sprite Droplet, Pixel;

        /// <summary>Height of the harpoon tip sprite above its pivot, in pixels.</summary>
        public const int HarpoonTipHeight = 8;

        static PlaceholderArt instance;
        public static PlaceholderArt I => instance ??= new PlaceholderArt(Tuning.I);

        readonly int ppu;

        PlaceholderArt(Tuning t)
        {
            ppu = t.pixelsPerUnit;
            WickBack = BuildWick(false);
            WickFront = BuildWick(true);
            Flame = BuildFlame();

            int kindCount = System.Enum.GetValues(typeof(BubbleKind)).Length;
            Bubbles = new Sprite[kindCount, t.tiers.Length];
            PopRings = new Sprite[t.tiers.Length];
            for (int tier = 0; tier < t.tiers.Length; tier++)
            {
                int d = Mathf.RoundToInt(t.tiers[tier].diameter * ppu);
                for (int k = 0; k < kindCount; k++)
                    Bubbles[k, tier] = BuildBubble((BubbleKind)k, t.Kind((BubbleKind)k).color, d);
                PopRings[tier] = BuildRing(Mathf.RoundToInt(d * 1.35f));
            }

            HarpoonTip = BuildHarpoonTip();
            Rope = BuildRope();
            Background = BuildBackground(t);
            FireButton = BuildFireButton(false);
            FireButtonPressed = BuildFireButton(true);
            MoveHint = BuildMoveHint();
            MoveKnob = BuildDisc(14, Palette.Lilac, "MoveKnob");
            MoveDot = BuildDisc(8, Palette.Cream, "MoveDot");

            var drop = new PixelCanvas(2, 2);
            drop.Rect(0, 0, 2, 2, Palette.Cream);
            Droplet = drop.ToSprite("Droplet", new Vector2(1, 1), ppu);

            var px = new PixelCanvas(1, 1);
            px.Set(0, 0, new Color32(255, 255, 255, 255));
            Pixel = px.ToSprite("Pixel", new Vector2(0.5f, 0.5f), ppu);
        }

        // ---------------------------------------------------------------- Wick (23 x 30 px, pivot at feet)

        Sprite BuildWick(bool front)
        {
            var c = new PixelCanvas(23, 30);
            // Cloak: round body
            c.Ellipse(11.5f, 11f, 8.5f, 8f, Palette.Saffron);
            c.EllipseOnFilled(11.5f, 2.5f, 10f, 3.5f, Palette.SaffronDark);
            // Belt: interior line in darker orange, not ink
            c.RectOnFilled(3, 8, 17, 1, Palette.SaffronLine);
            // Boots (shared by both views), drawn over the cloak hem
            c.Rect(6, 1, 5, 3, Palette.Ink);
            c.Rect(12, 1, 5, 3, Palette.Ink);
            c.Rect(7, 3, 3, 1, Palette.Deep);
            c.Rect(13, 3, 3, 1, Palette.Deep);
            // Hood
            c.Ellipse(11.5f, 19.5f, 6.5f, 5.5f, Palette.Saffron);
            c.Ellipse(11.5f, 24f, 2f, 2.2f, Palette.Saffron);
            c.RectOnFilled(6, 14, 12, 1, Palette.SaffronDark);
            // Candle wick on top (the flame is its own sprite)
            c.Rect(11, 26, 1, 2, Palette.Ink);

            if (front)
            {
                // Face in the hood opening, cream eyes. Only the front view has eyes.
                c.Ellipse(11.5f, 19f, 4f, 3.2f, Palette.Ink);
                c.Rect(9, 19, 1, 2, Palette.Cream);
                c.Rect(14, 19, 1, 2, Palette.Cream);
                c.RectOnFilled(11, 4, 1, 9, Palette.SaffronLine); // cloak opening
                DrawLauncher(c, 6);  // his right hand is on the viewer's left
            }
            else
            {
                c.RectOnFilled(11, 15, 1, 9, Palette.SaffronLine); // hood seam
                DrawLauncher(c, 15); // raised in the right hand: shots start ~+0.35 u right of centre
            }

            c.Outline(Palette.Ink);
            return c.ToSprite(front ? "Wick_Front" : "Wick_Back", new Vector2(11.5f, 0f), ppu);
        }

        static void DrawLauncher(PixelCanvas c, int x)
        {
            c.Rect(x, 14, 3, 11, Palette.Steel);
            c.Rect(x, 14, 1, 11, Palette.SteelLight);
            c.Rect(x + 2, 14, 1, 11, Palette.SteelDark);
            c.Rect(x, 24, 3, 1, Palette.SteelLight);
            // Hand gripping it
            c.Rect(x - 1, 15, 2, 2, Palette.Saffron);
        }

        Sprite BuildFlame()
        {
            var c = new PixelCanvas(9, 12);
            c.Ellipse(4.5f, 4f, 3f, 3.2f, Palette.Saffron);
            c.Ellipse(4.5f, 7f, 1.8f, 3f, Palette.Saffron);
            c.Ellipse(4.5f, 4f, 1.5f, 2f, Palette.Cream);
            c.Outline(Palette.Ink);
            return c.ToSprite("Flame", new Vector2(4.5f, 1f), ppu);
        }

        // ---------------------------------------------------------------- Bubbles (no eyes: colour + centre mark)

        Sprite BuildBubble(BubbleKind kind, Color32 color, int d)
        {
            var c = new PixelCanvas(d, d);
            float cx = d * 0.5f, cy = d * 0.5f, r = d * 0.5f - 1f;
            c.Circle(cx, cy, r, color);

            // Darker crescent on the lower right
            var shade = Palette.Shade(color, 0.78f);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    if (!c.Filled(x, y)) continue;
                    float dx = x + 0.5f - (cx - r * 0.18f), dy = y + 0.5f - (cy + r * 0.18f);
                    if (dx * dx + dy * dy > r * r * 0.97f) c.Set(x, y, shade);
                }

            // Centre mark per type (colour-blind backup)
            var mark = Palette.Shade(color, 0.68f);
            switch (kind)
            {
                case BubbleKind.Trigloop:
                    {
                        float dr = Mathf.Max(0.8f, r * 0.11f), ring = r * 0.32f;
                        for (int i = 0; i < 3; i++)
                        {
                            float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                            c.Circle(cx + Mathf.Cos(a) * ring, cy + Mathf.Sin(a) * ring - r * 0.05f, dr, mark);
                        }
                        break;
                    }
                case BubbleKind.Lead:
                    {
                        int h = Mathf.Max(1, Mathf.RoundToInt(r * 0.22f));
                        c.RectOnFilled(0, Mathf.RoundToInt(cy - h * 0.5f), d, h, mark);
                        break;
                    }
                case BubbleKind.Lucky:
                    {
                        int len = Mathf.Max(1, Mathf.RoundToInt(r * 0.35f));
                        int mx = Mathf.FloorToInt(cx), my = Mathf.FloorToInt(cy);
                        c.Rect(mx - len, my, len * 2 + 1, 1, Palette.Cream);
                        c.Rect(mx, my - len, 1, len * 2 + 1, Palette.Cream);
                        break;
                    }
            }

            // Shine stays high on the body
            c.Ellipse(cx - r * 0.42f, cy + r * 0.42f, Mathf.Max(1f, r * 0.22f), Mathf.Max(0.8f, r * 0.14f), Palette.Cream);
            if (d >= 24) c.Circle(cx - r * 0.1f, cy + r * 0.66f, Mathf.Max(0.8f, r * 0.07f), Palette.Cream);

            c.Outline(Palette.Ink);
            return c.ToSprite($"Bubble_{kind}_{d}px", new Vector2(cx, cy), ppu);
        }

        Sprite BuildRing(int d)
        {
            var c = new PixelCanvas(d, d);
            float cx = d * 0.5f, cy = d * 0.5f, r = d * 0.5f - 1f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(dist - r) > 0.6f) continue;
                    float a = Mathf.Atan2(dy, dx);
                    if (Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2f) * 16f) % 2 == 0)
                        c.Set(x, y, Palette.Cream);
                }
            return c.ToSprite($"PopRing_{d}px", new Vector2(cx, cy), ppu);
        }

        // ---------------------------------------------------------------- Harpoon: barbed steel tip + 1 px twisted cable

        Sprite BuildHarpoonTip()
        {
            var c = new PixelCanvas(9, 11);
            c.Rect(4, 1, 1, 3, Palette.Steel);        // short shaft into the cable
            c.Rect(2, 3, 5, 1, Palette.SteelDark);    // barb bar
            c.Set(2, 2, Palette.SteelDark);           // barbs hook down
            c.Set(6, 2, Palette.SteelDark);
            c.Rect(3, 4, 3, 2, Palette.Steel);
            c.Rect(3, 4, 1, 2, Palette.SteelLight);
            c.Rect(4, 6, 1, 3, Palette.SteelLight);   // point
            c.Outline(Palette.Ink);
            return c.ToSprite("HarpoonTip", new Vector2(4.5f, 1f), ppu);
        }

        Sprite BuildRope()
        {
            var c = new PixelCanvas(1, 3);
            c.Set(0, 0, Palette.SteelDark);
            c.Set(0, 1, Palette.Steel);
            c.Set(0, 2, Palette.SteelLight);
            return c.ToSprite("Rope", new Vector2(0.5f, 0f), ppu);
        }

        // ---------------------------------------------------------------- Stage: night sky, rooftops, lilac floor, ground strip

        Sprite BuildBackground(Tuning t)
        {
            const float left = -32f, bottom = -10f, top = 24f, right = 32f;
            int w = Mathf.RoundToInt((right - left) * ppu);
            int h = Mathf.RoundToInt((top - bottom) * ppu);
            var c = new PixelCanvas(w, h);
            int Px(float wx) => Mathf.RoundToInt((wx - left) * ppu);
            int Py(float wy) => Mathf.RoundToInt((wy - bottom) * ppu);

            // Sky: deep at the top, dusk at the horizon, ordered dither between bands.
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            int horizon = Py(2f), skyTop = Py(t.arenaHeight + 2f);
            for (int y = Py(0f); y < h; y++)
            {
                float k = Mathf.Clamp01((float)(y - horizon) / (skyTop - horizon));
                for (int x = 0; x < w; x++)
                {
                    float threshold = (bayer[(y % 4) * 4 + (x % 4)] + 0.5f) / 16f;
                    c.Set(x, y, k > threshold ? Palette.Deep : Palette.Dusk);
                }
            }

            var rng = new System.Random(7);
            // Stars
            for (int i = 0; i < 260; i++)
            {
                int x = rng.Next(w), y = rng.Next(Py(4f), h);
                c.Set(x, y, rng.NextDouble() < 0.3 ? Palette.Cream : Palette.Lilac);
            }
            // Moon
            c.Circle(Px(10.5f), Py(11.2f), 17f, Palette.Cream);
            c.Circle(Px(10.5f) + 5, Py(11.2f) - 4, 3.5f, Palette.Mix(Palette.Cream, Palette.Lilac, 0.35f));
            c.Circle(Px(10.5f) - 6, Py(11.2f) + 5, 2.2f, Palette.Mix(Palette.Cream, Palette.Lilac, 0.35f));

            // Rooftop silhouettes (deep, darker than the horizon)
            var roofColor = Palette.Mix(Palette.Deep, Palette.Ink, 0.35f);
            int bx = 0;
            while (bx < w)
            {
                int bw = rng.Next(30, 75);
                int bh = Mathf.RoundToInt((1.2f + (float)rng.NextDouble() * 3.8f) * ppu);
                int y0 = Py(0f);
                c.Rect(bx, y0, bw, bh, roofColor);
                if (rng.NextDouble() < 0.5)
                    for (int i = 0; i < bw / 2; i++) c.Rect(bx + i, y0 + bh + i / 2 - 1, bw - 2 * i, 1, roofColor); // pitched roof
                if (rng.NextDouble() < 0.6) c.Rect(bx + rng.Next(4, Mathf.Max(5, bw - 8)), y0 + bh, 4, 7, roofColor); // chimney
                for (int wy = y0 + 5; wy < y0 + bh - 4; wy += 9)
                    for (int wx = bx + 4; wx < bx + bw - 4; wx += 8)
                        if (rng.NextDouble() < 0.18) c.Rect(wx, wy, 2, 3, Palette.Mix(roofColor, Palette.Lilac, 0.6f));
                bx += bw + rng.Next(0, 6);
            }

            // Arena walls and ceiling beam (only visible on screens wider or taller than 16:9)
            int wallW = 6;
            int ceilY = Py(t.arenaHeight);
            for (int side = -1; side <= 1; side += 2)
            {
                int wx = side < 0 ? Px(-t.HalfWidth) - wallW : Px(t.HalfWidth);
                c.Rect(wx, Py(0f), wallW, ceilY - Py(0f) + 4, Palette.Lilac);
                c.Rect(side < 0 ? wx + wallW - 1 : wx, Py(0f), 1, ceilY - Py(0f) + 4, Palette.Mix(Palette.Lilac, Palette.Cream, 0.4f));
                c.Rect(side < 0 ? wx - 1 : wx + wallW, Py(0f), 1, ceilY - Py(0f) + 4, Palette.Ink);
            }
            c.Rect(Px(-t.HalfWidth) - wallW, ceilY, Px(t.HalfWidth) - Px(-t.HalfWidth) + wallW * 2, 4, Palette.Lilac);
            c.Rect(Px(-t.HalfWidth), ceilY, Px(t.HalfWidth) - Px(-t.HalfWidth), 1, Palette.Ink);

            // Floor: a lilac ledge, then the ground strip that holds the thumb controls
            int floorTop = Py(0f);
            c.Rect(0, floorTop - 6, w, 6, Palette.Lilac);
            c.Rect(0, floorTop - 1, w, 1, Palette.Mix(Palette.Lilac, Palette.Cream, 0.45f));
            c.Rect(0, floorTop - 6, w, 1, Palette.Shade(Palette.Lilac, 0.7f));
            c.Rect(0, floorTop - 7, w, 1, Palette.Ink);
            c.Rect(0, 0, w, floorTop - 7, Palette.Ink);
            // Faint brick courses in the ground strip
            var brick = Palette.Mix(Palette.Ink, Palette.Deep, 0.55f);
            for (int y = floorTop - 15, row = 0; y > 0; y -= 8, row++)
            {
                c.Rect(0, y, w, 1, brick);
                for (int x = (row % 2) * 12; x < w; x += 24) c.Rect(x, y - 7, 1, 7, brick);
            }

            return c.ToSprite("Background", new Vector2(-left * ppu, -bottom * ppu), ppu);
        }

        // ---------------------------------------------------------------- Touch controls (live in the ground strip)

        Sprite BuildFireButton(bool pressed)
        {
            var c = new PixelCanvas(46, 46);
            var face = pressed ? Palette.Shade(Palette.Mint, 0.8f) : Palette.Mint;
            c.Circle(23f, 23f, 21f, Palette.Shade(Palette.Mint, 0.62f));
            c.Circle(23f, pressed ? 22f : 24f, 20f, face);
            // Harpoon icon pointing up
            int ix = 23, iy = pressed ? 10 : 12;
            c.Rect(ix - 1, iy, 2, 16, Palette.Ink);
            for (int i = 0; i < 6; i++) c.Rect(ix - 6 + i, iy + 15 + i, 12 - 2 * i, 1, Palette.Ink);
            c.Outline(Palette.Ink);
            return c.ToSprite(pressed ? "FireButton_Pressed" : "FireButton", new Vector2(23f, 23f), ppu);
        }

        Sprite BuildMoveHint()
        {
            var c = new PixelCanvas(60, 13);
            var col = Palette.Mix(Palette.Lilac, Palette.Ink, 0.35f);
            for (int i = 0; i < 6; i++)
            {
                c.Rect(1 + i, 6 - i, 1, 1 + i * 2, col);       // left arrow
                c.Rect(58 - i, 6 - i, 1, 1 + i * 2, col);      // right arrow
            }
            for (int x = 12; x < 48; x += 4) c.Rect(x, 6, 2, 1, col);
            return c.ToSprite("MoveHint", new Vector2(30f, 6.5f), ppu);
        }

        Sprite BuildDisc(int d, Color32 color, string name)
        {
            var c = new PixelCanvas(d, d);
            c.Circle(d * 0.5f, d * 0.5f, d * 0.5f - 1f, color);
            c.Outline(Palette.Ink);
            return c.ToSprite(name, new Vector2(d * 0.5f, d * 0.5f), ppu);
        }
    }
}

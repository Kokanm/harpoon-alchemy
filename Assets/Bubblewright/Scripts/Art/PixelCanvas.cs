using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Tiny pixel painter used to build the placeholder sprites in code, straight from the sheet's shapes.
    /// (0,0) is the bottom-left pixel. Shapes are filled where a pixel's centre falls inside them.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        readonly Color32[] pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 Get(int x, int y) => InBounds(x, y) ? pixels[y * Width + x] : Palette.Clear;

        public void Set(int x, int y, Color32 c)
        {
            if (InBounds(x, y)) pixels[y * Width + x] = c;
        }

        public bool Filled(int x, int y) => Get(x, y).a > 0;

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        public void Circle(float cx, float cy, float r, Color32 c) => Ellipse(cx, cy, r, r, c);

        /// <summary>Recolours only pixels that are already filled.</summary>
        public void RectOnFilled(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    if (Filled(i, j)) Set(i, j, c);
        }

        /// <summary>Draws c only over pixels that are already filled (for shading inside a shape).</summary>
        public void EllipseOnFilled(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f && Filled(x, y)) Set(x, y, c);
                }
        }

        /// <summary>1 px outline on the OUTER edge only: empty pixels touching a filled one become ink.</summary>
        public void Outline(Color32 ink)
        {
            var copy = (Color32[])pixels.Clone();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (copy[y * Width + x].a > 0) continue;
                    bool touches =
                        (x > 0 && copy[y * Width + x - 1].a > 0) ||
                        (x < Width - 1 && copy[y * Width + x + 1].a > 0) ||
                        (y > 0 && copy[(y - 1) * Width + x].a > 0) ||
                        (y < Height - 1 && copy[(y + 1) * Width + x].a > 0);
                    if (touches) pixels[y * Width + x] = ink;
                }
        }

        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Pivot is in pixels from the bottom-left.</summary>
        public Sprite ToSprite(string name, Vector2 pivotPixels, int ppu)
        {
            var tex = ToTexture(name);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Width, Height),
                new Vector2(pivotPixels.x / Width, pivotPixels.y / Height), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}

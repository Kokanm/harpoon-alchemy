using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The starting weapon: a barbed steel tip trailing a 1 px twisted steel cable, fired straight up
    /// from the launcher tip at 20 u/s. The cable stays where it was fired. A bubble touching the cable
    /// or the tip pops. Gone when it pops something or reaches the ceiling. One on screen at a time.
    /// </summary>
    public class Harpoon : MonoBehaviour
    {
        public bool Active { get; private set; }

        Tuning T => Tuning.I;
        SpriteRenderer tip, rope;
        float baseY, tipY, x;

        public static Harpoon Create(Transform parent)
        {
            var go = new GameObject("Harpoon");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Harpoon>();
            var art = PlaceholderArt.I;

            var ropeGo = new GameObject("Cable");
            ropeGo.transform.SetParent(go.transform, false);
            h.rope = ropeGo.AddComponent<SpriteRenderer>();
            h.rope.sprite = art.Rope;
            h.rope.drawMode = SpriteDrawMode.Tiled;
            h.rope.tileMode = SpriteTileMode.Continuous;
            Sorting.Apply(h.rope, Layer.Shots, 0);
            Game.ApplySpriteMaterial(h.rope);

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(go.transform, false);
            h.tip = tipGo.AddComponent<SpriteRenderer>();
            h.tip.sprite = art.HarpoonTip;
            Sorting.Apply(h.tip, Layer.Shots, 1);
            Game.ApplySpriteMaterial(h.tip);

            h.Hide();
            return h;
        }

        public void Fire(Vector2 from)
        {
            // Snap to the pixel grid so the 1 px cable never straddles two columns.
            float px = 1f / T.pixelsPerUnit;
            x = Mathf.Round(from.x / px) * px;
            baseY = from.y;
            tipY = from.y;
            Active = true;
            tip.enabled = rope.enabled = true;
            UpdateVisual();
        }

        public void Hide()
        {
            Active = false;
            if (tip) tip.enabled = false;
            if (rope) rope.enabled = false;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            tipY += T.harpoonSpeed * dt;
            if (tipY + TipHeight >= T.arenaHeight)
            {
                Hide();
                return;
            }
            UpdateVisual();
        }

        float TipHeight => (float)PlaceholderArt.HarpoonTipHeight / T.pixelsPerUnit;

        void UpdateVisual()
        {
            float px = 1f / T.pixelsPerUnit;
            tip.transform.position = new Vector3(x, tipY, 0f);
            rope.transform.position = new Vector3(x, baseY, 0f);
            rope.size = new Vector2(px, Mathf.Max(px, tipY - baseY + px));
        }

        /// <summary>True if the bubble touches the cable or the tip.</summary>
        public bool Hits(Bubble b)
        {
            if (!Active) return false;
            float px = 1f / T.pixelsPerUnit;
            var cable = new Rect(x - px, baseY, px * 2f, tipY - baseY);
            var head = new Rect(x - 3.5f * px, tipY, 7f * px, TipHeight);
            return b.Overlaps(cable) || b.Overlaps(head);
        }
    }
}

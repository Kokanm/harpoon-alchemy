using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A kinematic bubble with its own bounce maths (no Rigidbody, no physics materials).
    /// Horizontal speed is constant. Each floor bounce launches it with exactly the speed that makes its
    /// top reach the tier's peak height, so every tier always peaks at the same height wherever it was born.
    /// Pivot at the centre.
    /// </summary>
    public class Bubble : MonoBehaviour
    {
        public BubbleKind Kind { get; private set; }
        public int Tier { get; private set; }
        public Vector2 Velocity;
        public bool Popped { get; set; }

        Tuning T => Tuning.I;
        SpriteRenderer sr;
        float grow = 1f;      // 0.7 -> 1 for freshly split children
        float growTimer = -1f;
        float squashTimer = -1f;

        public float FullRadius => T.tiers[Tier].diameter * 0.5f;
        public float Radius => FullRadius * grow;
        public Vector2 Position => transform.position;

        public static Bubble Create(Transform parent, BubbleKind kind, int tier, Vector2 position, Vector2 velocity, bool child)
        {
            var go = new GameObject($"Bubble {kind} {Tuning.I.tiers[tier].name}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var b = go.AddComponent<Bubble>();
            b.Kind = kind;
            b.Tier = tier;
            b.Velocity = velocity;
            b.sr = go.AddComponent<SpriteRenderer>();
            b.sr.sprite = PlaceholderArt.I.Bubbles[(int)kind, tier];
            Sorting.Apply(b.sr, Layer.Bubbles, 3 - tier); // smaller bubbles draw over bigger ones
            Game.ApplySpriteMaterial(b.sr);
            if (child)
            {
                b.grow = Tuning.I.childStartScale;
                b.growTimer = 0f;
            }
            b.ApplyVisual();
            return b;
        }

        public float SpeedX => T.bubbleSpeedX * T.Kind(Kind).speedScale;

        public void Tick(float dt)
        {
            float g = T.Gravity(Kind, Tier);
            var p = (Vector2)transform.position;

            // Exact ballistic step (constant gravity), and the floor bounce sets velocity exactly,
            // so the apex never drifts with frame rate.
            p.x += Velocity.x * dt;
            p.y += Velocity.y * dt - 0.5f * g * dt * dt;
            Velocity.y -= g * dt;

            float r = Radius;
            if (p.y - r <= 0f)
            {
                p.y = r;
                Velocity.y = T.BounceSpeed(Kind, Tier);
                squashTimer = 0f;
                Sfx.Play(Sound.Bounce, 1f + Tier * 0.25f, 0.5f - Tier * 0.1f); // big bubbles thud, small ones tap
            }
            if (p.y + r >= T.arenaHeight)
            {
                p.y = T.arenaHeight - r;
                Velocity.y = -Mathf.Abs(Velocity.y);
            }
            if (p.x - r <= -T.HalfWidth)
            {
                p.x = -T.HalfWidth + r;
                Velocity.x = Mathf.Abs(Velocity.x);
            }
            if (p.x + r >= T.HalfWidth)
            {
                p.x = T.HalfWidth - r;
                Velocity.x = -Mathf.Abs(Velocity.x);
            }
            transform.position = p;

            if (growTimer >= 0f)
            {
                growTimer += dt;
                float k = Mathf.Clamp01(growTimer / Mathf.Max(0.001f, T.childGrowTime));
                grow = Mathf.Lerp(T.childStartScale, 1f, k);
                if (k >= 1f) growTimer = -1f;
            }
            if (squashTimer >= 0f)
            {
                squashTimer += dt;
                if (squashTimer > 0.1f) squashTimer = -1f;
            }
            ApplyVisual();
        }

        void ApplyVisual()
        {
            // Bounce: squash 1.2 x 0.8 on contact for 50 ms, stretch 0.9 x 1.12 leaving the floor.
            Vector3 s = Vector3.one * grow;
            if (squashTimer >= 0f)
                s = squashTimer < 0.05f
                    ? new Vector3(1.2f * grow, 0.8f * grow, 1f)
                    : new Vector3(0.9f * grow, 1.12f * grow, 1f);
            s.z = 1f;
            transform.localScale = s;
        }

        /// <summary>Hit: swell during the hit-stop, then the game pops it.</summary>
        public void Swell(float scale)
        {
            Popped = true;
            transform.localScale = new Vector3(grow * scale, grow * scale, 1f);
        }

        /// <summary>Circle vs axis-aligned rectangle.</summary>
        public bool Overlaps(Rect rect)
        {
            var c = Position;
            float cx = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            float r = Radius;
            return dx * dx + dy * dy < r * r;
        }
    }
}

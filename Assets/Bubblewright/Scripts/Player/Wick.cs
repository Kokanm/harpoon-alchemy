using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The lamplighter. Floor only, no jump. Pivot at his feet.
    /// Rig: Wick (position) > Rig (squash, lean) > Back / Front groups (body + flame).
    /// He plays with his back to the camera and faces it only for the stage intro and the celebration.
    /// </summary>
    public class Wick : MonoBehaviour
    {
        public float VelocityX { get; private set; }
        public int Hearts { get; private set; }
        public bool FacingFront { get; private set; }
        public bool Invulnerable => invulnerable > 0f;
        public bool Dead => Hearts <= 0;

        Tuning T => Tuning.I;
        Transform rig;
        GameObject backSet, frontSet;
        SpriteRenderer bodyBack, bodyFront, flameBack, flameFront;

        float invulnerable;
        float knockTimer, knockVelocity;
        float turnTimer = -1f;
        bool turnToFront, swapped;
        float fireKick = -1f;
        float stepClock;
        float celebrateTimer = -1f;
        float flameClock;
        float lean;

        public static Wick Create(Transform parent)
        {
            var go = new GameObject("Wick");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<Wick>();
            w.Build();
            return w;
        }

        void Build()
        {
            var art = PlaceholderArt.I;
            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            backSet = new GameObject("Back").transform.gameObject;
            backSet.transform.SetParent(rig, false);
            frontSet = new GameObject("Front").transform.gameObject;
            frontSet.transform.SetParent(rig, false);

            bodyBack = MakePart(backSet.transform, "Body", art.WickBack, Vector2.zero, 0);
            flameBack = MakePart(backSet.transform, "Flame", art.Flame, new Vector2(0f, 27f / 15f), 1);
            bodyFront = MakePart(frontSet.transform, "Body", art.WickFront, Vector2.zero, 0);
            flameFront = MakePart(frontSet.transform, "Flame", art.Flame, new Vector2(0f, 27f / 15f), 1);

            ResetForStage(0f);
        }

        SpriteRenderer MakePart(Transform parent, string name, Sprite sprite, Vector2 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.Player, order);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        public void ResetForStage(float x)
        {
            transform.position = new Vector3(x, 0f, 0f);
            VelocityX = 0f;
            Hearts = T.hearts;
            invulnerable = 0f;
            knockTimer = 0f;
            turnTimer = -1f;
            celebrateTimer = -1f;
            fireKick = -1f;
            SetFacing(true);
            rig.localScale = Vector3.one;
            rig.localRotation = Quaternion.identity;
            SetVisible(true);
        }

        void SetFacing(bool front)
        {
            FacingFront = front;
            frontSet.SetActive(front);
            backSet.SetActive(!front);
        }

        void SetVisible(bool v)
        {
            bodyBack.enabled = bodyFront.enabled = v;
            flameBack.enabled = flameFront.enabled = v && !Dead;
        }

        /// <summary>Squash to 10% width, swap front/back at the thinnest point, spring back.</summary>
        public void Turn(bool toFront)
        {
            if (FacingFront == toFront && turnTimer < 0f) return;
            turnToFront = toFront;
            turnTimer = 0f;
            swapped = false;
        }

        public Vector2 LauncherTip => (Vector2)transform.position + T.launcherTip;

        public Rect Hitbox
        {
            get
            {
                var p = transform.position;
                return new Rect(p.x - T.hitbox.x * 0.5f, p.y, T.hitbox.x, T.hitbox.y);
            }
        }

        public void OnFire() => fireKick = 0f;

        public void Hurt(float fromX)
        {
            if (Invulnerable || Dead) return;
            Hearts--;
            invulnerable = T.invulnerableTime;
            knockTimer = T.knockbackTime;
            float dir = transform.position.x >= fromX ? 1f : -1f;
            knockVelocity = dir * T.knockback / Mathf.Max(0.01f, T.knockbackTime);
            VelocityX = 0f;
            if (Dead)
            {
                invulnerable = 0f;
                flameBack.enabled = flameFront.enabled = false;
            }
        }

        public void Celebrate() => celebrateTimer = 0f;

        /// <summary>Physics and procedural animation. move is -1..1. dt is game time.</summary>
        public void Tick(float move, float dt, bool controllable)
        {
            float half = T.HalfWidth - 11.5f / T.pixelsPerUnit; // keep the drawn body inside the walls
            var pos = transform.position;

            if (knockTimer > 0f)
            {
                knockTimer -= dt;
                pos.x += knockVelocity * dt;
            }
            else
            {
                float target = controllable && !Dead ? Mathf.Clamp(move, -1f, 1f) * T.runSpeed : 0f;
                bool stopping = target == 0f || (VelocityX != 0f && Mathf.Sign(target) != Mathf.Sign(VelocityX));
                float rate = T.runSpeed / Mathf.Max(0.001f, stopping ? T.stopTime : T.accelTime);
                VelocityX = Mathf.MoveTowards(VelocityX, target, rate * dt);
                pos.x += VelocityX * dt;
            }

            if (pos.x < -half) { pos.x = -half; VelocityX = 0f; }
            if (pos.x > half) { pos.x = half; VelocityX = 0f; }
            transform.position = pos;

            if (invulnerable > 0f) invulnerable -= dt;
            Animate(dt);
        }

        void Animate(float dt)
        {
            float px = 1f / T.pixelsPerUnit;
            Vector3 scale = Vector3.one;
            float offsetY = 0f;

            // Run: lean into the direction, bob 1 px per step at 8 steps/s.
            float speed01 = T.runSpeed > 0f ? VelocityX / T.runSpeed : 0f;
            lean = Mathf.MoveTowards(lean, -speed01 * 8f, 120f * dt);
            if (Mathf.Abs(speed01) > 0.1f)
            {
                stepClock += dt * 8f;
                offsetY = (Mathf.FloorToInt(stepClock) % 2) * px;
            }
            else
            {
                stepClock = 0f;
                // Idle: slow 1.6 s breathing squash
                float breath = Mathf.Sin(Time.time * Mathf.PI * 2f / 1.6f);
                scale = new Vector3(1f + breath * 0.02f, 1f - breath * 0.02f, 1f);
            }

            // Fire: launcher kick, body squashes 1.1 x 0.9 for 70 ms
            if (fireKick >= 0f)
            {
                fireKick += dt;
                if (fireKick < 0.07f) scale = new Vector3(1.1f, 0.9f, 1f);
                else fireKick = -1f;
            }

            // Celebrate: two hops in 900 ms
            if (celebrateTimer >= 0f)
            {
                celebrateTimer += dt;
                float t = celebrateTimer % 0.45f;
                if (celebrateTimer < 0.9f)
                {
                    offsetY = Mathf.Sin(t / 0.45f * Mathf.PI) * 0.8f;
                    scale = t < 0.08f ? new Vector3(0.9f, 1.12f, 1f) : Vector3.one;
                }
                else offsetY = 0f;
                flameBack.transform.localScale = flameFront.transform.localScale = Vector3.one * 1.3f;
            }

            // Turn: squash to 10% width, swap at the thinnest point, spring back with overshoot
            if (turnTimer >= 0f)
            {
                turnTimer += dt;
                float half = T.turnTime * 0.5f;
                if (turnTimer < half)
                {
                    scale.x *= Mathf.Lerp(1f, 0.1f, turnTimer / half);
                }
                else
                {
                    if (!swapped) { SetFacing(turnToFront); swapped = true; }
                    float k = Mathf.Clamp01((turnTimer - half) / half);
                    scale.x *= Mathf.Lerp(0.1f, 1f, EaseOutBack(k));
                    if (turnTimer >= T.turnTime) turnTimer = -1f;
                }
            }

            if (Dead)
            {
                scale = new Vector3(1.08f, 0.85f, 1f); // slump, stays turned away
                lean = 0f;
            }

            rig.localScale = scale;
            rig.localPosition = new Vector3(0f, Mathf.Round(offsetY * T.pixelsPerUnit) * px, 0f);
            rig.localRotation = Quaternion.Euler(0f, 0f, lean);

            // Flame = health: full at 3, small at 2, flickering at 1
            if (!Dead && celebrateTimer < 0f)
            {
                flameClock += dt;
                float s = Hearts >= 3 ? 1f : Hearts == 2 ? 0.75f : 0.55f;
                if (Hearts <= 1 && Mathf.FloorToInt(flameClock * 14f) % 3 == 0) s *= 0.7f;
                else if (Mathf.FloorToInt(flameClock * 10f) % 2 == 0) s *= 0.93f; // two-step flicker
                flameBack.transform.localScale = flameFront.transform.localScale = new Vector3(s, s, 1f);
            }

            // Blink at 10 Hz while invulnerable
            bool visible = invulnerable <= 0f || Mathf.FloorToInt(invulnerable * 20f) % 2 == 0;
            SetVisible(visible);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}

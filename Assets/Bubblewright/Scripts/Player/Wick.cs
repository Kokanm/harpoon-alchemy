using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The lamplighter. Floor only, no jump. Pivot at his feet.
    /// Rig: Wick (position) > Rig (squash, lean, hop) > Back / Front groups (body, launcher, flame; the front adds a free hand).
    /// He plays with his back to the camera and faces it only for the stage intro and the celebration.
    /// All animation is procedural, with timings from the sheet's animation table (Tuning > Wick animation).
    /// </summary>
    public class Wick : MonoBehaviour
    {
        public float VelocityX { get; private set; }
        public int Hearts { get; private set; }
        public bool FacingFront { get; private set; }
        public bool Invulnerable => invulnerable > 0f;
        public bool Dead => Hearts <= 0;
        /// <summary>Set by the game during the stage intro: he waves his free hand while facing the camera.</summary>
        public bool Waving { get; set; }

        Tuning T => Tuning.I;
        float Px => 1f / T.pixelsPerUnit;
        Transform rig, hand;
        Effects fx;
        GameObject backSet, frontSet;
        SpriteRenderer bodyBack, bodyFront, flameBack, flameFront, launcherBack, launcherFront, handFront;
        Vector3 launcherBackHome, launcherFrontHome;

        float invulnerable;
        float knockTimer, knockVelocity;
        float turnTimer = -1f;
        bool turnToFront, swapped;
        float fireKick = -1f;
        float runStart = -1f, skid = -1f;
        int runDir, skidDir, lastWantDir;
        float hurtFlash = -1f;
        float stepClock;
        float celebrateTimer = -1f;
        int celebrateLandings;
        float flameClock, idleClock;
        float lean;

        public static Wick Create(Transform parent, Effects fx)
        {
            var go = new GameObject("Wick");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<Wick>();
            w.fx = fx;
            w.Build();
            return w;
        }

        void Build()
        {
            var art = PlaceholderArt.I;
            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            backSet = new GameObject("Back");
            backSet.transform.SetParent(rig, false);
            frontSet = new GameObject("Front");
            frontSet.transform.SetParent(rig, false);

            var flamePos = new Vector2(0f, 27f * Px);
            bodyBack = MakePart(backSet.transform, "Body", art.WickBack, Vector2.zero, 0);
            launcherBack = MakePart(backSet.transform, "Launcher", art.Launcher, PlaceholderArt.LauncherOffsetPixels(false) * Px, 2);
            flameBack = MakePart(backSet.transform, "Flame", art.Flame, flamePos, 1);
            bodyFront = MakePart(frontSet.transform, "Body", art.WickFront, Vector2.zero, 0);
            launcherFront = MakePart(frontSet.transform, "Launcher", art.Launcher, PlaceholderArt.LauncherOffsetPixels(true) * Px, 2);
            flameFront = MakePart(frontSet.transform, "Flame", art.Flame, flamePos, 1);
            handFront = MakePart(frontSet.transform, "Hand", art.Hand, Vector2.zero, 2);
            hand = handFront.transform;
            launcherBackHome = launcherBack.transform.localPosition;
            launcherFrontHome = launcherFront.transform.localPosition;

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
            fireKick = runStart = skid = hurtFlash = -1f;
            lastWantDir = 0;
            lean = 0f;
            Waving = false;
            SetFacing(true);
            rig.localScale = Vector3.one;
            rig.localRotation = Quaternion.identity;
            flameBack.transform.localScale = flameFront.transform.localScale = Vector3.one;
            ApplyFlash(false);
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
            bodyBack.enabled = bodyFront.enabled = launcherBack.enabled = launcherFront.enabled = v;
            flameBack.enabled = flameFront.enabled = v && !Dead;
        }

        void ApplyFlash(bool on)
        {
            var art = PlaceholderArt.I;
            bodyBack.sprite = on ? art.WickBackFlash : art.WickBack;
            bodyFront.sprite = on ? art.WickFrontFlash : art.WickFront;
            launcherBack.sprite = launcherFront.sprite = on ? art.LauncherFlash : art.Launcher;
        }

        /// <summary>Squash to 10% width, swap front/back at the thinnest point behind a dust puff, spring back.</summary>
        public void Turn(bool toFront)
        {
            if (FacingFront == toFront && turnTimer < 0f) return;
            turnToFront = toFront;
            turnTimer = 0f;
            swapped = false;
            Sfx.Play(Sound.Turn);
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
            runStart = skid = -1f;
            // Flash now: the game freezes right after this, so Animate will not run until the freeze ends.
            hurtFlash = T.hurtFlashTime;
            ApplyFlash(true);
            SetVisible(true);
            if (Dead)
            {
                invulnerable = 0f;
                flameBack.enabled = flameFront.enabled = false;
            }
        }

        public void Celebrate()
        {
            celebrateTimer = 0f;
            celebrateLandings = 0;
        }

        /// <summary>Physics and procedural animation. move is -1..1. dt is game time.</summary>
        public void Tick(float move, float dt, bool controllable)
        {
            float half = T.HalfWidth - 11.5f * Px; // keep the drawn body inside the walls
            var pos = transform.position;

            if (knockTimer > 0f)
            {
                knockTimer -= dt;
                pos.x += knockVelocity * dt;
                lastWantDir = 0;
            }
            else
            {
                float target = controllable && !Dead ? Mathf.Clamp(move, -1f, 1f) * T.runSpeed : 0f;
                DetectRunEdges(target);
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

        /// <summary>Starts the run-start stretch and the skid when the wanted direction changes.</summary>
        void DetectRunEdges(float target)
        {
            int want = target > 0f ? 1 : target < 0f ? -1 : 0;
            if (want == lastWantDir) return;
            int moving = VelocityX > 0f ? 1 : VelocityX < 0f ? -1 : 0;
            if (moving != 0 && want != moving && Mathf.Abs(VelocityX) >= T.skidMinSpeed * T.runSpeed)
            {
                skid = 0f;
                skidDir = moving;
                fx.Dust(Feet(moving * 5), moving);
                Sfx.Play(Sound.Skid);
            }
            if (want != 0)
            {
                runStart = 0f;
                runDir = want;
                fx.Dust(Feet(-3), -want);
                fx.Dust(Feet(3), -want);
            }
            lastWantDir = want;
        }

        Vector2 Feet(int pixelsX) => (Vector2)transform.position + new Vector2(pixelsX * Px, 0f);

        void Animate(float dt)
        {
            Vector3 scale = Vector3.one;
            float offsetY = 0f;
            float speed01 = T.runSpeed > 0f ? VelocityX / T.runSpeed : 0f;
            float leanRate = T.runLean / Mathf.Max(0.01f, T.runStartTime);

            // Run: lean into the direction, bob 1 px per step at 8 steps/s. Idle: slow 1.6 s breathing squash.
            if (Mathf.Abs(speed01) > 0.1f)
            {
                stepClock += dt * 8f;
                offsetY = (Mathf.FloorToInt(stepClock) % 2) * Px;
            }
            else
            {
                stepClock = 0f;
                idleClock += dt;
                float breath = Mathf.Sin(idleClock * Mathf.PI * 2f / 1.6f);
                scale = new Vector3(1f + breath * 0.02f, 1f - breath * 0.02f, 1f);
            }

            // Run start: stretch 0.9 x 1.1 that eases back to round
            if (runStart >= 0f)
            {
                runStart += dt;
                float k = runStart / Mathf.Max(0.001f, T.runStartTime);
                if (k >= 1f) runStart = -1f;
                else scale = Vector3.Scale(scale, Squash(T.runStartStretch, EaseOutQuad(k)));
            }

            // Skid: hold a lean back for the first half, then settle with a 1.05 x 0.95 squash
            bool holdSkidLean = false;
            if (skid >= 0f)
            {
                skid += dt;
                float k = skid / Mathf.Max(0.001f, T.skidTime);
                if (k >= 1f) skid = -1f;
                else if (k < 0.5f) holdSkidLean = true;
                else scale = Vector3.Scale(scale, Squash(T.skidSettle, (k - 0.5f) * 2f));
            }
            if (holdSkidLean) lean = skidDir * T.skidLean;
            else lean = Mathf.MoveTowards(lean, -speed01 * T.runLean, leanRate * dt);

            // Fire: launcher kicks down 2 px, body squashes 1.1 x 0.9
            float kick = 0f;
            if (fireKick >= 0f)
            {
                fireKick += dt;
                if (fireKick < T.fireTime)
                {
                    scale = Vector3.Scale(scale, new Vector3(T.fireSquash.x, T.fireSquash.y, 1f));
                    kick = -T.fireKickPixels * Px;
                }
                else fireKick = -1f;
            }

            // Free hand (front view only): waves in the intro, goes up with the launcher on the second celebration hop
            bool handUp = false;
            float launcherLift = 0f;
            Vector2 handPixels = new Vector2(19f, 15f);

            if (celebrateTimer >= 0f)
            {
                Celebrating(dt, ref scale, ref offsetY, ref handUp, ref launcherLift, ref handPixels);
                lean = 0f;
            }
            else if (Waving && FacingFront)
            {
                handUp = true;
                handPixels = new Vector2(19f + (Mathf.FloorToInt(Time.time * 6f) % 2), 21f);
                lean = Mathf.Sin(Time.time * Mathf.PI * 2f * 1.5f) * 3f; // a little rock while he waves
            }

            // Turn: squash to 10% width (and a touch taller), swap at the thinnest point, spring back with overshoot
            if (turnTimer >= 0f)
            {
                turnTimer += dt;
                float half = T.turnTime * 0.5f;
                if (turnTimer < half)
                {
                    float k = turnTimer / half;
                    scale.x *= Mathf.Lerp(1f, 0.1f, k);
                    scale.y *= Mathf.Lerp(1f, T.turnStretchY, k);
                }
                else
                {
                    if (!swapped)
                    {
                        SetFacing(turnToFront);
                        swapped = true;
                        fx.Dust(Feet(0), 0f); // hides the swap
                    }
                    float k = Mathf.Clamp01((turnTimer - half) / half);
                    scale.x *= Mathf.Lerp(0.1f, 1f, EaseOutBack(k));
                    scale.y *= Mathf.Lerp(T.turnStretchY, 1f, k);
                    if (turnTimer >= T.turnTime) turnTimer = -1f;
                }
            }

            if (Dead)
            {
                scale = new Vector3(1.08f, 0.85f, 1f); // slump, stays turned away
                lean = 0f;
            }

            rig.localScale = scale;
            rig.localPosition = new Vector3(0f, Mathf.Round(offsetY * T.pixelsPerUnit) * Px, 0f);
            rig.localRotation = Quaternion.Euler(0f, 0f, lean);
            launcherBack.transform.localPosition = launcherBackHome + new Vector3(0f, kick, 0f);
            launcherFront.transform.localPosition = launcherFrontHome + new Vector3(0f, kick + launcherLift, 0f);
            handFront.enabled = handUp && bodyFront.enabled;
            hand.localPosition = new Vector3((handPixels.x - 11.5f) * Px, handPixels.y * Px, 0f);

            // Flame = health: full at 3, small at 2, flickering at 1
            if (!Dead && celebrateTimer < 0f)
            {
                flameClock += dt;
                float s = Hearts >= 3 ? 1f : Hearts == 2 ? 0.75f : 0.55f;
                if (Hearts <= 1 && Mathf.FloorToInt(flameClock * 14f) % 3 == 0) s *= 0.7f;
                else if (Mathf.FloorToInt(flameClock * 10f) % 2 == 0) s *= 0.93f; // two-step flicker
                flameBack.transform.localScale = flameFront.transform.localScale = new Vector3(s, s, 1f);
            }

            // Hurt flash, then blink at 10 Hz while invulnerable
            if (hurtFlash >= 0f)
            {
                hurtFlash -= dt;
                if (hurtFlash < 0f) ApplyFlash(false);
            }
            bool visible = hurtFlash >= 0f || invulnerable <= 0f || Mathf.FloorToInt(invulnerable * 20f) % 2 == 0;
            SetVisible(visible);
        }

        /// <summary>Two hops: stretch on take-off, squash on landing with dust, arms up on the second hop, flame at 130%.</summary>
        void Celebrating(float dt, ref Vector3 scale, ref float offsetY, ref bool handUp, ref float launcherLift, ref Vector2 handPixels)
        {
            celebrateTimer += dt;
            float hopTime = T.celebrateTime * 0.5f;
            const float air = 0.8f; // fraction of each hop in the air; the rest is the landing squash
            if (celebrateTimer < T.celebrateTime)
            {
                int hop = Mathf.FloorToInt(celebrateTimer / hopTime);
                float u = (celebrateTimer - hop * hopTime) / hopTime;
                if (u < air)
                {
                    float a = u / air;
                    offsetY = Mathf.Sin(a * Mathf.PI) * T.celebrateHop;
                    scale = a < 0.25f ? new Vector3(0.9f, 1.12f, 1f) : Vector3.one;
                }
                else
                {
                    if (celebrateLandings <= hop)
                    {
                        celebrateLandings = hop + 1;
                        fx.Dust(Feet(-4), -1f);
                        fx.Dust(Feet(4), 1f);
                    }
                    float k = (u - air) / (1f - air);
                    scale = Squash(new Vector2(1.12f, 0.88f), k);
                }
                if (hop >= 1)
                {
                    handUp = true;
                    handPixels = new Vector2(20f, 25f);
                    launcherLift = 4f * Px;
                }
            }
            else
            {
                // Done: stand proud, arms still up.
                handUp = true;
                handPixels = new Vector2(20f, 25f);
                launcherLift = 4f * Px;
            }
            float s = T.celebrateFlameScale;
            flameBack.transform.localScale = flameFront.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Scale (x, y) at k = 0, easing to round at k = 1.</summary>
        static Vector3 Squash(Vector2 s, float k) =>
            new Vector3(Mathf.Lerp(s.x, 1f, k), Mathf.Lerp(s.y, 1f, k), 1f);

        static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}

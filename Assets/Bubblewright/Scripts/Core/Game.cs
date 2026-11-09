using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if BW_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Bubblewright
{
    /// <summary>
    /// The pause menu and the debug panel live in Game.Menus.cs.
    /// Builds and runs the prototype entirely from code: camera, stage, Wick, harpoons, bubbles,
    /// cable fusions (Harpoon Alchemy), touch controls and HUD. A run is a few generated stages with a
    /// pick-one-of-three upgrade between them; losing every heart ends the run (with 1 optional Relight
    /// revive per run), and the next one starts again from stage 1.
    ///
    /// Units: 1 unit = 15 art pixels. Floor at y = 0, ceiling at y = 14, walls at x = -16 and +16.
    /// The ground strip (thumb controls) is y = -4 to 0.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public partial class Game : MonoBehaviour
    {
        enum State { Intro, Play, Clear, Pick, Over }
        const string BestScoreKey = "Bubblewright.BestScore";

        class ActiveShard
        {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float distLeft, halfHeight;
            public int piercesLeft;
        }

        class ActiveEmber
        {
            public SpriteRenderer sr;
            public Vector2 pos;
            public float baseX, driftSpeed, age, life, radius, scale;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindAnyObjectByType<Game>() == null) new GameObject("Game").AddComponent<Game>();
        }

        Tuning T => Tuning.I;
        readonly GameInput input = new GameInput();
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly List<Bubble> hitBubbles = new List<Bubble>(); // swelling during the hit-stop, popped when it ends
        readonly List<ActiveShard> shards = new List<ActiveShard>();
        readonly List<ActiveEmber> embers = new List<ActiveEmber>();

        Camera cam;
        Transform world, bubbleRoot;
        Wick wick;
        readonly List<Harpoon> harpoons = new List<Harpoon>();
        SpriteRenderer tripwireSr;
        Effects fx;
        TouchControlsView touchView;

        State state;
        float stateTime;
        float sessionStartTime;
        float freeze;
        float slowMo;
        float shake;
        bool turnedForCelebration, celebrating;
        float shotTimer;
        float stormClock, stormBanner;
        bool storm;
        Run run;
        int bestScore;
        bool newBest;
        List<Upgrade> choices = new List<Upgrade>();
        int selected, lastMoveDir;
        readonly Rect[] cardRects = new Rect[3]; // GUI space, written by OnGUI, read by TickPick
        Rect rerollRect, relightRect;
        bool showHitboxes;
        Vector3 cameraHome;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Application.targetFrameRate = 60; // mobile defaults to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            ConfigureCamera(cam);
            cameraHome = cam.transform.position;

            world = new GameObject("World").transform;
            world.SetParent(transform, false);

            var bg = new GameObject("Stage Background").AddComponent<SpriteRenderer>();
            bg.transform.SetParent(world, false);
            bg.sprite = PlaceholderArt.I.Background;
            Sorting.Apply(bg, Layer.Sky);
            ApplySpriteMaterial(bg);

            bubbleRoot = new GameObject("Bubbles").transform;
            bubbleRoot.SetParent(world, false);
            harpoons.Add(Harpoon.Create(world));

            var twGo = new GameObject("Tripwire");
            twGo.transform.SetParent(world, false);
            tripwireSr = twGo.AddComponent<SpriteRenderer>();
            tripwireSr.sprite = PlaceholderArt.I.Pixel;
            tripwireSr.color = Palette.Lemon;
            Sorting.Apply(tripwireSr, Layer.Shots, 2);
            ApplySpriteMaterial(tripwireSr);
            tripwireSr.enabled = false;

            fx = Effects.Create(world);
            wick = Wick.Create(world, fx);
            Sfx.Create(transform);
            touchView = TouchControlsView.Create(world, cam);

            sessionStartTime = Time.unscaledTime;
            GameServices.Analytics.Log("session_start");

            try { bestScore = PlayerPrefs.GetInt(BestScoreKey, 0); } catch { bestScore = 0; }
            StartRun();
        }

        void OnApplicationQuit()
        {
            GameServices.Analytics.Log("session_end", ("duration_s", Mathf.RoundToInt(Time.unscaledTime - sessionStartTime)));
        }

        /// <summary>
        /// Orthographic camera framing the 32 x 18 unit screen (arena + ground strip) at 480 x 270,
        /// with URP's Pixel Perfect Camera (upscale render texture) when URP is installed.
        /// Wider or taller screens show extra scenery around it.
        /// </summary>
        public static void ConfigureCamera(Camera cam)
        {
            var t = Tuning.I;
            cam.orthographic = true;
            cam.orthographicSize = t.referenceResolution.y * 0.5f / t.pixelsPerUnit; // 9
            cam.transform.position = new Vector3(0f, (t.arenaHeight - t.groundStrip) * 0.5f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Ink;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
#if BW_URP
            var ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc == null) ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = t.pixelsPerUnit;
            ppc.refResolutionX = t.referenceResolution.x;
            ppc.refResolutionY = t.referenceResolution.y;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
#endif
        }

        public const string SpriteUnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        static Material spriteMaterial;
        static bool spriteMaterialChecked;

        /// <summary>
        /// Under the URP 2D renderer, use the unlit sprite material so the prototype needs no 2D lights.
        /// Under any other pipeline, keep Unity's default sprite material.
        /// </summary>
        public static void ApplySpriteMaterial(SpriteRenderer sr)
        {
            if (!spriteMaterialChecked)
            {
                spriteMaterialChecked = true;
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    // Created by the project setup in Resources, so builds always include the shader.
                    spriteMaterial = Resources.Load<Material>("SpriteUnlit");
                    if (spriteMaterial == null)
                    {
                        var shader = Shader.Find(SpriteUnlitShader);
                        if (shader != null) spriteMaterial = new Material(shader) { name = "Sprite-Unlit (Bubblewright)" };
                    }
                }
            }
            if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        }

        /// <summary>Permadeath: a new run starts from stage 1 with full hearts and no upgrades.</summary>
        void StartRun()
        {
            run = new Run(Random.Range(1, 1000000));
            newBest = false;
            StartStage();
        }

        void StartStage()
        {
            foreach (var b in bubbles) if (b) Destroy(b.gameObject);
            bubbles.Clear();
            hitBubbles.Clear();
            ClearHazards();
            fx.Clear();
            ApplyRunUpgrades();
            HideHarpoons();
            wick.ResetForStage(0f, run.Hearts);
            wick.Waving = true;
            freeze = slowMo = shake = 0f;
            shotTimer = 0f;
            turnedForCelebration = celebrating = false;
            stormClock = stormBanner = 0f;
            storm = false;

            foreach (var s in StageGenerator.Generate(run.Stage, run.Rng))
            {
                int tier = Mathf.Clamp(s.tier, 0, T.tiers.Length - 1);
                SpawnBubble(s.kind, tier, s.position, s.direction >= 0 ? 1 : -1, 0f, false);
            }
            GameServices.Analytics.Log("level_start", ("stage", run.Stage), ("seed", run.Seed), ("build", run.BuildSummary()));
            SetState(State.Intro);
        }

        void ClearHazards()
        {
            foreach (var s in shards) if (s.sr) Destroy(s.sr.gameObject);
            shards.Clear();
            foreach (var e in embers) if (e.sr) Destroy(e.sr.gameObject);
            embers.Clear();
            if (tripwireSr) tripwireSr.enabled = false;
        }

        void ApplyRunUpgrades()
        {
            wick.SpeedMultiplier = run.SpeedMultiplier;
            wick.ExtraInvulnerable = run.ExtraInvulnerable;
            while (harpoons.Count < run.Shots) harpoons.Add(Harpoon.Create(world));
            foreach (var h in harpoons)
            {
                h.SpeedMultiplier = run.HarpoonSpeedMultiplier;
                h.WidthMultiplier = run.HarpoonWidthMultiplier;
                h.HoldTime = run.CableHold;
            }
        }

        void HideHarpoons()
        {
            foreach (var h in harpoons) h.Hide();
            if (tripwireSr) tripwireSr.enabled = false;
        }

        void TickHarpoons(float dt)
        {
            foreach (var h in harpoons)
            {
                bool wasHooked = h.IsHooked;
                h.Tick(dt);
                if (!h.Active) continue;

                // Trigger ceiling payload effects once when the tip reaches the ceiling (or hooks).
                if (!h.CeilingTriggered && (h.IsHooked || h.TipY + (float)PlaceholderArt.HarpoonTipHeight / T.pixelsPerUnit >= T.arenaHeight - 0.05f))
                {
                    h.CeilingTriggered = true;
                    OnHarpoonCeiling(h);
                }

                // Hooked ceiling periodic effects (Tesla Rod & Acid Drip).
                if (h.IsHooked && h.HookedTime >= h.NextPulseTime)
                {
                    h.NextPulseTime += 0.45f;
                    OnHookedCablePulse(h);
                }
            }
        }

        void SetState(State s)
        {
            state = s;
            stateTime = 0f;
        }

        Bubble SpawnBubble(BubbleKind kind, int tier, Vector2 pos, int dir, float vy, bool child)
        {
            var b = Bubble.Create(bubbleRoot, kind, tier, pos, Vector2.zero, child);
            b.Velocity = new Vector2(dir * b.SpeedX, vy);
            bubbles.Add(b);
            return b;
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            input.Update();
            HandleDebugKeys();

            // Phones are locked to landscape; if we still end up in portrait, pause behind a prompt.
            if (Screen.height > Screen.width)
            {
                touchView.Refresh(input, T);
                return;
            }

            if (UpdatePause())
            {
                touchView.Refresh(input, T);
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            if (freeze > 0f) // hit-stop
            {
                freeze -= dt;
                UpdateCamera(dt);
                touchView.Refresh(input, T);
                return;
            }
            PopHitBubbles();
            if (slowMo > 0f)
            {
                slowMo -= dt;
                dt *= 0.5f;
            }
            stateTime += dt;

            switch (state)
            {
                case State.Intro: TickIntro(dt); break;
                case State.Play: TickPlay(dt); break;
                case State.Clear: TickClear(dt); break;
                case State.Pick: TickPick(dt); break;
                case State.Over: TickOver(dt); break;
            }

            fx.Tick(dt);
            UpdateCamera(dt);
            touchView.Refresh(input, T);
        }

        void TickIntro(float dt)
        {
            // Faces the camera at stage start. Any input starts the turn, and input is live from then on.
            wick.Tick(0f, dt, false);
            if (stateTime >= T.introTime || input.AnyInput || input.Move != 0f || input.FireHeld)
            {
                wick.Waving = false;
                wick.Turn(false);
                SetState(State.Play);
            }
        }

        void TickPlay(float dt)
        {
            wick.Tick(input.Move, dt, true);

            // Up to run.Shots cables at once. Holding fire spaces extra cables out so they do not stack on one spot.
            shotTimer += dt;
            Harpoon free = null;
            bool anyActive = false;
            for (int i = 0; i < run.Shots && i < harpoons.Count; i++)
            {
                if (harpoons[i].Active) anyActive = true;
                else if (free == null) free = harpoons[i];
            }
            if (free != null && (input.FirePressed || (T.holdToRefire && input.FireHeld && (!anyActive || shotTimer >= T.multiShotGap))))
            {
                free.Fire(wick.LauncherTip);
                shotTimer = 0f;
                wick.OnFire();
                fx.MuzzleFlash(wick.LauncherTip);
                Sfx.Play(Sound.Fire);
                OnHarpoonFired(free);
            }
            TickHarpoons(dt);

            if (!debug.freezeTimer) stormClock += dt;
            if (!storm && stormClock >= StageTimeLimit) StartStorm();
            if (stormBanner > 0f) stormBanner -= dt;

            for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(BubbleDt(dt));

            foreach (var h in harpoons)
            {
                if (!h.Active) continue;
                for (int i = 0; i < bubbles.Count; i++)
                {
                    if (bubbles[i].Popped || !h.Hits(bubbles[i])) continue;
                    var hitPos = bubbles[i].Position;
                    h.Hide();
                    Hit(bubbles[i]);
                    OnHarpoonHitBubble(hitPos);
                    break;
                }
            }

            TickTripwire();
            TickShards(dt);
            TickEmbers(dt);

            if (!debug.god && !wick.Invulnerable && !wick.Dead)
            {
                var box = wick.Hitbox;
                foreach (var b in bubbles)
                {
                    if (b.Popped || !b.Overlaps(box)) continue;
                    wick.Hurt(b.Position.x, storm ? T.stormDamage : 1);
                    freeze = T.hurtFreeze;
                    shake = T.shakeTime;
                    Sfx.Play(Sound.Hurt);
                    if (wick.Dead)
                    {
                        HideHarpoons();
                        ClearHazards();
                        run.Hearts = 0;
                        RecordScore();
                        GameServices.Analytics.Log("level_fail",
                            ("stage", run.Stage),
                            ("time_s", Mathf.RoundToInt(stormClock)),
                            ("storm_active", storm),
                            ("build", run.BuildSummary()));
                        SetState(State.Over);
                        Sfx.Play(Sound.Over);
                    }
                    break;
                }
            }

            if (state == State.Play && bubbles.Count == 0 && hitBubbles.Count == 0)
            {
                slowMo = T.lastPopSlowMo;
                ClearHazards();
                run.Hearts = wick.Hearts;
                if (run.IsLastStage) RecordScore();
                GameServices.Analytics.Log("level_complete",
                    ("stage", run.Stage),
                    ("time_s", Mathf.RoundToInt(stormClock)),
                    ("hearts_left", run.Hearts),
                    ("fusions", run.ActiveFusions().Count));
                SetState(State.Clear);
            }
        }

        // ------------------------------------------------------------------ Harpoon Alchemy payloads & fusions

        void OnHarpoonFired(Harpoon fired)
        {
            // Arc Twin (Twin Cable + Spark Wire): firing a second cable while another is active zaps across them.
            if (!run.HasFusion(FusionKind.ArcTwin)) return;
            foreach (var other in harpoons)
            {
                if (other == fired || !other.Active) continue;
                Vector2 a = new Vector2(fired.X, Mathf.Max(3f, fired.TipY));
                Vector2 b = new Vector2(other.X, Mathf.Max(3f, other.TipY * 0.65f));
                fx.Zap(a, b, Palette.Mint, 0.18f);
                ZapBubblesNearSegment(a, b, 1.3f);
            }
        }

        void OnHarpoonCeiling(Harpoon h)
        {
            if (run.NeedleRank > 0)
            {
                SpawnNeedlePair(new Vector2(h.X, T.arenaHeight - 0.6f));
                if (run.HasFusion(FusionKind.CeilingLoom))
                    SpawnNeedlePair(new Vector2(h.X, T.arenaHeight * 0.5f));
            }
            if (run.HasFusion(FusionKind.AcidDrip))
                SpawnEmber(new Vector2(h.X, T.arenaHeight - 1.2f), 0f);
        }

        void OnHookedCablePulse(Harpoon h)
        {
            if (run.HasFusion(FusionKind.TeslaRod))
            {
                Vector2 top = new Vector2(h.X, T.arenaHeight - 0.5f);
                Vector2 bot = new Vector2(h.X, 1.5f);
                fx.Zap(bot, top, Palette.Lemon, 0.12f);
                Bubble target = FindNearestBubble(new Vector2(h.X, T.arenaHeight * 0.5f), 3.2f);
                if (target != null)
                {
                    fx.Zap(new Vector2(h.X, target.Position.y), target.Position, Palette.Lemon, 0.16f);
                    Hit(target);
                }
            }
        }

        void OnHarpoonHitBubble(Vector2 hitPos)
        {
            if (run.NeedleRank > 0)
            {
                SpawnNeedlePair(hitPos);
                if (run.HasFusion(FusionKind.VolleyLoom))
                {
                    SpawnNeedlePair(hitPos + new Vector2(0f, 0.6f));
                }
            }
        }

        void TriggerPopPayloads(Bubble b)
        {
            // 1. Spark Wire: chain lightning to nearest unpopped bubble.
            if (run.SparkRank > 0)
            {
                float range = 6.0f + (run.SparkRank - 1) * 2.5f + (run.HasFusion(FusionKind.StormLance) ? 3.5f : 0f);
                int maxJumps = run.HasFusion(FusionKind.StormLance) ? 2 : 1;
                Vector2 origin = b.Position;
                for (int j = 0; j < maxJumps; j++)
                {
                    Bubble target = FindNearestBubble(origin, range, b);
                    if (target == null) break;
                    fx.Zap(origin, target.Position, Palette.Lemon, 0.16f);
                    Sfx.Play(Sound.Pop, 1.8f, 0.5f);
                    origin = target.Position;
                    Hit(target);
                }
            }

            // 2. Alkahest: rising acid/alchemy ember orb.
            if (run.EmberRank > 0)
            {
                if (run.HasFusion(FusionKind.TwinFlasks))
                {
                    SpawnEmber(b.Position + new Vector2(-0.4f, 0f), -1.4f);
                    SpawnEmber(b.Position + new Vector2(0.4f, 0f), 1.4f);
                }
                else
                {
                    SpawnEmber(b.Position, 0f);
                }
            }
        }

        void SpawnNeedlePair(Vector2 at)
        {
            float range = 5.5f + (run.NeedleRank - 1) * 2.5f;
            bool saw = run.HasFusion(FusionKind.SawbladeBarbs);
            float halfH = saw ? 0.65f : 0.25f;
            int pierces = saw ? 2 : 1;
            float scaleY = saw ? 2.2f : 1f;

            for (int dir = -1; dir <= 1; dir += 2)
            {
                var go = new GameObject("NeedleShard");
                go.transform.SetParent(world, false);
                go.transform.position = at;
                go.transform.localScale = new Vector3(dir, scaleY, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderArt.I.NeedleShard;
                Sorting.Apply(sr, Layer.Shots, 2);
                ApplySpriteMaterial(sr);
                shards.Add(new ActiveShard
                {
                    sr = sr,
                    pos = at,
                    vel = new Vector2(dir * 22f, 0f),
                    distLeft = range,
                    halfHeight = halfH,
                    piercesLeft = pierces,
                });
            }
        }

        void TickShards(float dt)
        {
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];
                float step = Mathf.Abs(s.vel.x) * dt;
                s.pos += s.vel * dt;
                s.distLeft -= step;
                s.sr.transform.position = s.pos;

                var rect = new Rect(s.pos.x - 0.3f, s.pos.y - s.halfHeight, 0.6f, s.halfHeight * 2f);
                for (int b = 0; b < bubbles.Count; b++)
                {
                    if (bubbles[b].Popped || !bubbles[b].Overlaps(rect)) continue;
                    Hit(bubbles[b]);
                    s.piercesLeft--;
                    if (s.piercesLeft <= 0) break;
                }

                if (s.piercesLeft <= 0 || s.distLeft <= 0f || Mathf.Abs(s.pos.x) >= T.HalfWidth)
                {
                    Destroy(s.sr.gameObject);
                    shards.RemoveAt(i);
                }
            }
        }

        void SpawnEmber(Vector2 at, float driftX)
        {
            bool caustic = run.HasFusion(FusionKind.CausticCloud);
            float scale = caustic ? 1.75f : 1f;
            float radius = 0.45f * scale;
            float life = 1.6f + (run.EmberRank - 1) * 0.6f + (caustic ? 0.6f : 0f);

            var go = new GameObject("AlkahestEmber");
            go.transform.SetParent(world, false);
            go.transform.position = at;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.I.AlkahestOrb;
            Sorting.Apply(sr, Layer.Shots, 1);
            ApplySpriteMaterial(sr);

            embers.Add(new ActiveEmber
            {
                sr = sr,
                pos = at,
                baseX = at.x,
                driftSpeed = driftX,
                age = 0f,
                life = life,
                radius = radius,
                scale = scale,
            });
        }

        void TickEmbers(float dt)
        {
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                var e = embers[i];
                e.age += dt;
                e.baseX = Mathf.Clamp(e.baseX + e.driftSpeed * dt, -T.HalfWidth + 1f, T.HalfWidth - 1f);
                e.pos.x = e.baseX + Mathf.Sin(e.age * 8f) * 0.35f;
                e.pos.y += 2.2f * dt;
                e.sr.transform.position = e.pos;

                float pulse = e.scale * (0.92f + 0.12f * Mathf.Sin(e.age * 14f));
                e.sr.transform.localScale = new Vector3(pulse, pulse, 1f);

                var rect = new Rect(e.pos.x - e.radius, e.pos.y - e.radius, e.radius * 2f, e.radius * 2f);
                bool hitAny = false;
                for (int b = 0; b < bubbles.Count; b++)
                {
                    if (bubbles[b].Popped || !bubbles[b].Overlaps(rect)) continue;
                    Hit(bubbles[b]);
                    hitAny = true;
                    break;
                }

                if (hitAny || e.age >= e.life || e.pos.y >= T.arenaHeight - 0.3f)
                {
                    Destroy(e.sr.gameObject);
                    embers.RemoveAt(i);
                }
            }
        }

        /// <summary>Tripwire Gate (Twin Cable + Anchor Tip): horizontal plasma wire between two active cables.</summary>
        void TickTripwire()
        {
            if (!run.HasFusion(FusionKind.TripwireGate))
            {
                if (tripwireSr) tripwireSr.enabled = false;
                return;
            }

            Harpoon first = null, second = null;
            for (int i = 0; i < harpoons.Count; i++)
            {
                if (!harpoons[i].Active || harpoons[i].TipY < 4f) continue;
                if (first == null) first = harpoons[i];
                else { second = harpoons[i]; break; }
            }

            if (first == null || second == null || Mathf.Abs(first.X - second.X) < 0.5f)
            {
                tripwireSr.enabled = false;
                return;
            }

            float minX = Mathf.Min(first.X, second.X);
            float maxX = Mathf.Max(first.X, second.X);
            float wireY = Mathf.Min(first.TipY, second.TipY) - 0.4f;
            float px = 1f / T.pixelsPerUnit;

            tripwireSr.enabled = true;
            tripwireSr.color = Mathf.FloorToInt(Time.unscaledTime * 20f) % 2 == 0 ? Palette.Lemon : Palette.Mint;
            tripwireSr.transform.position = new Vector3((minX + maxX) * 0.5f, wireY, 0f);
            tripwireSr.transform.localScale = new Vector3((maxX - minX) * T.pixelsPerUnit, 2f, 1f);

            var wireRect = new Rect(minX, wireY - px * 2f, maxX - minX, px * 4f);
            for (int i = 0; i < bubbles.Count; i++)
            {
                if (bubbles[i].Popped || !bubbles[i].Overlaps(wireRect)) continue;
                fx.Zap(new Vector2(minX, wireY), new Vector2(maxX, wireY), Palette.Lemon, 0.12f);
                Hit(bubbles[i]);
                break;
            }
        }

        Bubble FindNearestBubble(Vector2 from, float maxDist, Bubble ignore = null)
        {
            Bubble best = null;
            float bestSq = maxDist * maxDist;
            foreach (var b in bubbles)
            {
                if (b == null || b == ignore || b.Popped) continue;
                float sq = (b.Position - from).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = b;
                }
            }
            return best;
        }

        void ZapBubblesNearSegment(Vector2 a, Vector2 b, float radius)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            foreach (var bub in bubbles)
            {
                if (bub.Popped) continue;
                float t = lenSq > 0.001f ? Mathf.Clamp01(Vector2.Dot(bub.Position - a, ab) / lenSq) : 0f;
                Vector2 closest = a + ab * t;
                if ((bub.Position - closest).sqrMagnitude <= (radius + bub.Radius) * (radius + bub.Radius))
                {
                    Hit(bub);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ storm timer

        float StageTimeLimit => T.stageTime + T.stageTimeGrowth * (run.Stage - 1);

        /// <summary>The storm speeds every bubble up by playing their motion faster; their arcs stay the same shape.</summary>
        float BubbleDt(float dt) => storm ? dt * T.stormSpeed : dt;

        /// <summary>The clock ran out: bubbles go fast and hit harder until the stage is cleared.</summary>
        void StartStorm()
        {
            storm = true;
            stormClock = Mathf.Max(stormClock, StageTimeLimit);
            stormBanner = 1.8f;
            shake = T.shakeTime * 2f;
            Sfx.Play(Sound.Hurt, 0.6f, 0.9f);
        }

        void TickClear(float dt)
        {
            wick.Tick(0f, dt, false);
            TickHarpoons(dt);
            if (!turnedForCelebration && slowMo <= 0f)
            {
                turnedForCelebration = true;
                wick.Turn(true);
            }
            if (turnedForCelebration && !celebrating && wick.FacingFront && stateTime > T.lastPopSlowMo * 0.5f + T.turnTime)
            {
                celebrating = true;
                wick.Celebrate();
                fx.Confetti((Vector2)wick.transform.position + new Vector2(0f, 2.5f), 40);
                Sfx.Play(Sound.Clear);
            }
            if (stateTime > 1.2f && input.ConfirmPressed)
            {
                if (run.IsLastStage) StartRun();
                else OpenPick();
            }
        }

        void TickOver(float dt)
        {
            wick.Tick(0f, dt, false);
            for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(BubbleDt(dt));
            if (stateTime <= 0.8f) return;

            if (!run.RelightAdUsed && input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                if (relightRect.Contains(gui))
                {
                    TriggerRewardedRelight();
                    return;
                }
            }
            if (!run.RelightAdUsed && input.RerollPressed)
            {
                TriggerRewardedRelight();
                return;
            }
            if (input.ConfirmPressed)
            {
                if (Time.unscaledTime - sessionStartTime >= 180f)
                    GameServices.Ads.ShowInterstitial("run_end");
                StartRun();
            }
        }

        void TriggerRewardedRelight()
        {
            if (run.RelightAdUsed) return;
            GameServices.Ads.ShowRewarded("relight_heart", () =>
            {
                run.RelightAdUsed = true;
                run.Hearts = 1;
                wick.ResetForStage(wick.transform.position.x, 1);
                wick.Turn(false);
                Sfx.Play(Sound.Clear, 1.2f, 0.8f);
                SetState(State.Play);
            });
        }

        void RecordScore()
        {
            if (run.Score <= bestScore) return;
            bestScore = run.Score;
            newBest = true;
            try { PlayerPrefs.SetInt(BestScoreKey, bestScore); PlayerPrefs.Save(); } catch { }
        }

        // ------------------------------------------------------------------ upgrade pick

        void OpenPick()
        {
            choices = run.RollChoices(cardRects.Length);
            if (choices.Count == 0)
            {
                NextStage();
                return;
            }
            selected = choices.Count / 2;
            lastMoveDir = (int)Mathf.Sign(input.Move) * (input.Move != 0f ? 1 : 0);
            for (int i = 0; i < cardRects.Length; i++) cardRects[i] = Rect.zero;
            rerollRect = Rect.zero;
            SetState(State.Pick);
        }

        bool CanRerollPick => run.FreeRerollsLeft > 0 || (!run.AdRerollUsed && GameServices.Ads.IsRewardedReady("reroll_upgrades"));

        void TryRerollPick()
        {
            if (!CanRerollPick) return;
            if (run.FreeRerollsLeft > 0)
            {
                run.FreeRerollsLeft--;
                GameServices.Analytics.Log("booster_used", ("type", "free_reroll"));
                choices = run.RollChoices(cardRects.Length);
                selected = choices.Count / 2;
                Sfx.Play(Sound.Turn, 1.3f, 0.8f);
            }
            else if (!run.AdRerollUsed)
            {
                GameServices.Ads.ShowRewarded("reroll_upgrades", () =>
                {
                    run.AdRerollUsed = true;
                    choices = run.RollChoices(cardRects.Length);
                    selected = choices.Count / 2;
                    Sfx.Play(Sound.Turn, 1.5f, 0.8f);
                });
            }
        }

        /// <summary>Tap or click a card; or left/right then Enter, Space or pad A; press 1-3; or Tab / tap Reroll.</summary>
        void TickPick(float dt)
        {
            wick.Tick(0f, dt, false);
            int dir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            bool moved = dir != 0 && dir != lastMoveDir;
            lastMoveDir = dir;
            if (stateTime < 0.35f) return; // so a tap meant for the clear screen does not pick a card

            if (moved) selected = (selected + dir + choices.Count) % choices.Count;
            if (input.RerollPressed && CanRerollPick) { TryRerollPick(); return; }
            if (input.DebugSpawn >= 1 && input.DebugSpawn <= choices.Count) { Choose(input.DebugSpawn - 1); return; }
            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                if (CanRerollPick && rerollRect.Contains(gui)) { TryRerollPick(); return; }
                for (int i = 0; i < choices.Count; i++)
                    if (cardRects[i].Contains(gui)) { Choose(i); return; }
            }
            if (input.KeyConfirmPressed) Choose(selected);
        }

        void Choose(int i)
        {
            var picked = choices[i];
            var preview = run.PreviewFusion(picked);
            run.Take(picked);
            if (preview.HasValue)
                GameServices.Analytics.Log("booster_used", ("type", "fusion_unlocked"), ("fusion", preview.Value.Name));
            Sfx.Play(Sound.Pop, preview.HasValue ? 2.0f : 1.6f, 0.8f);
            NextStage();
        }

        void NextStage()
        {
            run.Stage++;
            StartStage();
        }

        /// <summary>The bubble swells and the game freezes for the hit-stop; it pops when the freeze ends.</summary>
        void Hit(Bubble b)
        {
            if (b.Popped) return;
            b.Swell(T.popSwell);
            hitBubbles.Add(b);
            freeze = Mathf.Max(freeze, T.popHitStop);
            if (b.Tier == 0) shake = T.shakeTime; // only XL pops shake, so it keeps its meaning
        }

        void PopHitBubbles()
        {
            if (hitBubbles.Count == 0) return;
            // Copy to a temporary array because Spark Wire chain-zaps may add newly hit bubbles during Pop.
            var batch = hitBubbles.ToArray();
            hitBubbles.Clear();
            foreach (var b in batch)
                if (b != null) Pop(b);
        }

        void Pop(Bubble b)
        {
            bubbles.Remove(b);
            var kind = T.Kind(b.Kind);
            run.Score += T.tiers[b.Tier].score;
            if (b.Kind == BubbleKind.Lucky) run.Score += 500; // placeholder reward

            fx.Pop(b.Position, b.Tier, kind.color, T.popSwell);
            Sfx.Play(Sound.Pop, 1f + b.Tier * 0.2f, 0.8f);

            int next = b.Tier + 1;
            if (next < T.tiers.Length)
            {
                float g = T.Gravity(b.Kind, next);
                float kick = Mathf.Sqrt(2f * g * T.childKickHeight);
                float childR = T.tiers[next].diameter * 0.5f;
                int count = Mathf.Max(1, kind.children);
                int parentDir = b.Velocity.x >= 0f ? 1 : -1;
                for (int i = 0; i < count; i++)
                {
                    // Up and out: first child left, second right; a Trigloop's third follows the parent, kicked higher.
                    int dir = i == 0 ? -1 : i == 1 ? 1 : parentDir;
                    float vy = i >= 2 ? kick * T.trigloopMiddleKick : kick;
                    var pos = b.Position + new Vector2(dir * childR * 0.5f, 0f);
                    pos.y = Mathf.Max(pos.y, childR);
                    SpawnBubble(b.Kind, next, pos, dir, vy, true);
                }
            }

            TriggerPopPayloads(b);
            Destroy(b.gameObject);
        }

        void UpdateCamera(float dt)
        {
            var pos = cameraHome;
            if (shake > 0f)
            {
                shake -= dt;
                float px = 1f / T.pixelsPerUnit;
                pos.x += Random.Range(-T.shakePixels, T.shakePixels + 1) * px;
                pos.y += Random.Range(-T.shakePixels, T.shakePixels + 1) * px;
            }
            cam.transform.position = pos;
        }

        void HandleDebugKeys()
        {
            if (input.DebugToggleHitboxes) showHitboxes = !showHitboxes;
            if (input.DebugSpawn > 0 && !paused && (state == State.Play || state == State.Intro))
                DebugSpawn((BubbleKind)(input.DebugSpawn - 1));
        }

        void DebugSpawn(BubbleKind kind)
        {
            float x = Random.Range(-T.HalfWidth + 3f, T.HalfWidth - 3f);
            SpawnBubble(kind, 0, new Vector2(x, 9f), Random.value < 0.5f ? -1 : 1, 0f, false);
        }

        // ------------------------------------------------------------------ HUD (placeholder, OnGUI)

        GUIStyle textStyle;
        Texture2D whiteTex;

        void OnGUI()
        {
            int k = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / (float)T.referenceResolution.x, Screen.height / (float)T.referenceResolution.y)));
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
                whiteTex = Texture2D.whiteTexture;
            }
            textStyle.fontSize = 8 * k;

            if (Screen.height > Screen.width)
            {
                GUI.color = Palette.Ink;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex);
                GUI.color = Color.white;
                Centered("Turn your phone sideways", Screen.height * 0.5f, 10 * k, Palette.Cream);
                return;
            }

            // Arena corners in screen space (the arena may not fill wide screens).
            Vector2 topLeft = WorldToGui(new Vector2(-T.HalfWidth, T.arenaHeight));
            Vector2 topRight = WorldToGui(new Vector2(T.HalfWidth, T.arenaHeight));
            float pad = 6 * k;

            // Hearts as little flames, top left
            var flame = PlaceholderArt.I.Flame;
            var ft = flame.texture;
            for (int i = 0; i < run.MaxHearts; i++)
            {
                GUI.color = i < wick.Hearts ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(topLeft.x + pad + i * (ft.width + 2) * k, topLeft.y + pad, ft.width * k, ft.height * k), ft);
            }
            GUI.color = Color.white;
            Label($"STAGE {run.Stage}/{T.runLength}", topLeft.x + pad + run.MaxHearts * (ft.width + 2) * k + 4 * k, topLeft.y + pad + 2 * k, UiText.Primary, TextAnchor.UpperLeft, 200 * k);

            // Pause button in the top right corner, the score just left of it.
            pauseButtonRect = new Rect(topRight.x - pad - 14 * k, topLeft.y + pad - k, 14 * k, 14 * k);
            DrawPauseButton(pauseButtonRect, k);
            Label($"{run.Score:000000}", pauseButtonRect.x - 6 * k - 200 * k, topLeft.y + pad + 2 * k, UiText.Primary, TextAnchor.UpperRight, 200 * k);

            DrawStormTimer(topLeft, topRight, pad, k);
            string cheats = DebugSummary();
            if (cheats != null) Label(cheats, topLeft.x + pad, topLeft.y + pad + 16 * k, UiText.Warning, TextAnchor.UpperLeft, 300 * k);

            bool touchUi = Application.isMobilePlatform || input.TouchUsed;
            if (!touchUi)
            {
                Vector2 strip = WorldToGui(new Vector2(-T.HalfWidth, -T.groundStrip * 0.5f));
                Label("A/D or arrows: move    Space: fire    Pad: stick + A    1-4: spawn bubble    F1: hitboxes    R: continue",
                    strip.x + pad, strip.y - 10 * k, UiText.Secondary, TextAnchor.UpperLeft, Screen.width);
                Label($"BUILD: {run.BuildSummary()}", strip.x + pad, strip.y + 2 * k, UiText.Primary, TextAnchor.UpperLeft, Screen.width);
            }
            string best = newBest ? $"NEW BEST {bestScore}" : $"BEST {bestScore}";

            switch (state)
            {
                case State.Clear when stateTime > 0.4f:
                    if (stateTime > 1.2f) Plate(Screen.height * 0.32f - 6 * k, (run.IsLastStage ? 50 : 38) * k, k);
                    {
                        // Drops in from above the arena and bounces to rest.
                        float drop = Mathf.Clamp01((stateTime - 0.4f) / Mathf.Max(0.01f, T.bannerDropTime));
                        float y = Mathf.LerpUnclamped(topLeft.y - 20 * k, Screen.height * 0.32f, EaseOutBounce(drop));
                        Centered(run.IsLastStage ? "RUN COMPLETE" : "STAGE CLEAR", y, 16 * k, Palette.Cream);
                    }
                    if (stateTime > 1.2f)
                    {
                        float y0 = Screen.height * 0.32f + 22 * k;
                        if (run.IsLastStage)
                        {
                            Centered($"SCORE {run.Score}    {best}", y0, 8 * k, UiText.Accent);
                            Centered(touchUi ? "Tap to start a new run" : "Press R, Enter or Start for a new run", y0 + 12 * k, 8 * k, UiText.Secondary);
                        }
                        else Centered(touchUi ? "Tap to pick an upgrade" : "Press R, Enter or Start to pick an upgrade", y0, 8 * k, UiText.Secondary);
                    }
                    break;
                case State.Pick:
                    DrawPick(k, touchUi);
                    break;
                case State.Over:
                    Plate(Screen.height * 0.32f - 6 * k, (stateTime > 0.8f ? 96 : 26) * k, k);
                    Centered("THE FLAME WENT OUT", Screen.height * 0.32f, 14 * k, UiText.Primary);
                    if (stateTime > 0.8f)
                    {
                        float y0 = Screen.height * 0.32f + 20 * k;
                        Centered($"STAGE {run.Stage} OF {T.runLength}    SCORE {run.Score}    {best}", y0, 8 * k, UiText.Accent);
                        Centered($"BUILD: {run.BuildSummary()}", y0 + 12 * k, 8 * k, UiText.Primary);
                        Centered(touchUi ? "Tap to start a new run" : "Press R, Enter or Start for a new run", y0 + 24 * k, 8 * k, UiText.Secondary);
                        Centered($"seed {run.Seed}", y0 + 36 * k, 8 * k, UiText.Secondary);
                        if (!run.RelightAdUsed)
                        {
                            float bw = 156 * k, bh = 18 * k;
                            relightRect = new Rect((Screen.width - bw) * 0.5f, y0 + 48 * k, bw, bh);
                            DrawMenuButton(relightRect, "RELIGHT +1 HEART (AD / TAB)", false, k);
                        }
                        else relightRect = Rect.zero;
                    }
                    break;
            }

            if (stormBanner > 0f && state == State.Play)
            {
                Plate(Screen.height * 0.32f - 6 * k, 38 * k, k);
                Centered("STORM!", Screen.height * 0.32f, 16 * k, UiText.Warning);
                Centered($"Bubbles are faster and take {T.stormDamage} hearts", Screen.height * 0.32f + 20 * k, 8 * k, UiText.Primary);
            }

            if (showHitboxes) DrawHitboxes(k);
            if (paused) DrawPause(k, touchUi);
        }

        /// <summary>A thin bar across the top of the arena: mint, then blinking lemon near the end, coral once the storm hits.</summary>
        void DrawStormTimer(Vector2 topLeft, Vector2 topRight, float pad, int k)
        {
            float limit = Mathf.Max(1f, StageTimeLimit);
            float left = Mathf.Max(0f, limit - stormClock);
            var track = new Rect(topLeft.x + pad, topLeft.y + 2 * k, topRight.x - topLeft.x - 2 * pad, 2 * k);
            FillRect(track, WithAlpha(Palette.Ink, UiText.TimerTrackAlpha));

            bool warn = !storm && left <= T.stormWarning;
            bool blinkOff = warn && Mathf.FloorToInt(Time.unscaledTime * 4f) % 2 == 1 && !paused;
            Color fill = storm ? Palette.Coral : warn ? Palette.Lemon : Palette.Mint;
            if (storm) fill.a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
            float w = storm ? track.width : track.width * (left / limit);
            if (!blinkOff) FillRect(new Rect(track.x, track.y, w, track.height), fill);

            string text = storm ? "STORM" : $"{Mathf.FloorToInt(Mathf.Ceil(left) / 60f)}:{Mathf.CeilToInt(left) % 60:00}";
            Centered(text, topLeft.y + pad + 2 * k, 8 * k, storm || warn ? UiText.Warning : UiText.Primary);
        }

        GUIStyle cardStyle;
        Vector2 lastMouse;

        /// <summary>
        /// Three cards over a dimmed stage. On desktop the card under the mouse (or picked with the keys) glows
        /// as a whole: halo, mint border, lighter fill and a small lift. Cards that complete a Cable-Fusion show
        /// a lemon FUSES banner.
        /// </summary>
        void DrawPick(int k, bool touchUi)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Palette.Ink, UiText.DimAlpha));
            float cy = Screen.height * 0.5f;
            Centered($"STAGE {run.Stage} CLEAR  ·  PICK ONE", cy - 72 * k, 12 * k, UiText.Primary);

            if (cardStyle == null) cardStyle = new GUIStyle(textStyle) { wordWrap = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
            cardStyle.fontSize = 8 * k;

            float w = 120 * k, h = 92 * k, gap = 10 * k;
            float x0 = (Screen.width - (choices.Count * w + (choices.Count - 1) * gap)) * 0.5f;
            float top = cy - 48 * k;
            bool canHover = !Application.isMobilePlatform;

            // Hit-test against the resting card positions, so the lift never makes the hover flicker.
            // Only a moving mouse takes the highlight, so a resting cursor does not fight the arrow keys.
            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = mouse;
            for (int i = 0; i < choices.Count; i++)
            {
                cardRects[i] = new Rect(x0 + i * (w + gap), top, w, h);
                if (canHover && mouseMoved && cardRects[i].Contains(mouse)) selected = i;
            }

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < choices.Count; i++)
            {
                var u = choices[i];
                var fusionPreview = run.PreviewFusion(u);
                bool lit = canHover && i == selected;
                var r = cardRects[i];
                if (lit)
                {
                    r.y -= 2 * k;
                    for (int g = 4; g >= 1; g--) // soft halo around the whole card, outermost first
                        FillRect(Grow(r, g * k), WithAlpha(Palette.Mint, 0.12f * pulse * (5 - g) / 4f));
                }
                int border = lit || fusionPreview.HasValue ? 2 * k : k;
                FillRect(r, lit ? Palette.Mint : fusionPreview.HasValue ? Palette.Lemon : Palette.Lilac);
                var inner = Grow(r, -border);
                FillRect(inner, lit ? UiText.CardFillHover : UiText.CardFill);

                var iconBox = new Rect(inner.x + 5 * k, inner.y + 5 * k, 22 * k, 22 * k);
                FillRect(iconBox, Palette.Ink);
                if (lit) FillRect(Grow(iconBox, -k), WithAlpha(Palette.Mint, 0.15f));
                int iconPx = PlaceholderArt.UpgradeIconSize;
                GUI.DrawTexture(new Rect(iconBox.x + 2 * k, iconBox.y + 2 * k, iconPx * k, iconPx * k), PlaceholderArt.I.UpgradeIcon(u.Name));

                int n = run.TimesTaken(u);
                string title = u.Max > 1 && n > 0 ? $"{u.Name} {n + 1}" : u.Name;
                Label(title, iconBox.xMax + 5 * k, iconBox.y + 6 * k, lit ? UiText.Accent : UiText.Primary, TextAnchor.UpperLeft, inner.xMax - iconBox.xMax - 8 * k);
                SetTextColor(cardStyle, lit ? UiText.Primary : UiText.Secondary);
                GUI.Label(new Rect(inner.x + 6 * k, iconBox.yMax + 4 * k, inner.width - 12 * k, inner.yMax - iconBox.yMax - 16 * k), u.Text, cardStyle);

                if (fusionPreview.HasValue)
                    Label($"FUSES: {fusionPreview.Value.Name}", inner.x + 6 * k, inner.yMax - 13 * k, UiText.Warning, TextAnchor.UpperLeft, inner.width - 18 * k);

                if (!touchUi) Label($"{i + 1}", inner.xMax - 10 * k, inner.yMax - 13 * k, UiText.Secondary, TextAnchor.UpperLeft, 10 * k);
            }

            if (CanRerollPick)
            {
                float rw = 148 * k, rh = 16 * k;
                rerollRect = new Rect((Screen.width - rw) * 0.5f, top + h + 6 * k, rw, rh);
                string rerollLabel = run.FreeRerollsLeft > 0 ? "REROLL CARDS (1 FREE · TAB)" : "REROLL CARDS (AD · TAB)";
                DrawMenuButton(rerollRect, rerollLabel, canHover && rerollRect.Contains(mouse), k);
            }
            else rerollRect = Rect.zero;

            Centered($"HEARTS {run.Hearts}/{run.MaxHearts}    NEXT: STAGE {run.Stage + 1} OF {T.runLength}", top + h + 26 * k, 8 * k, UiText.Secondary);
            Centered(touchUi ? "Tap a card" : "Left/right and Enter, 1-3, Tab to reroll, or click a card", top + h + 38 * k, 8 * k, UiText.Secondary);
        }

        /// <summary>A dark band behind banner text, so it reads over stars, the moon and bubbles.</summary>
        void Plate(float y, float height, int k)
        {
            float w = Mathf.Min(Screen.width, 300 * k);
            FillRect(new Rect((Screen.width - w) * 0.5f, y, w, height), WithAlpha(Palette.Ink, UiText.PlateAlpha));
        }

        static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2 * by, r.height + 2 * by);

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Same colour in every state, so IMGUI never recolours just the label under the mouse.</summary>
        static void SetTextColor(GUIStyle style, Color c)
        {
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = c;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = c;
        }

        void FillRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, whiteTex);
            GUI.color = Color.white;
        }

        static float EaseOutBounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }

        Vector2 WorldToGui(Vector2 world)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            return new Vector2(s.x, Screen.height - s.y);
        }

        void Label(string text, float x, float y, Color color, TextAnchor anchor, float width)
        {
            textStyle.alignment = anchor;
            var r = new Rect(x, y, width, textStyle.fontSize * 2f);
            SetTextColor(textStyle, Palette.Ink);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, textStyle);
            SetTextColor(textStyle, color);
            GUI.Label(r, text, textStyle);
        }

        void Centered(string text, float y, int size, Color color)
        {
            int old = textStyle.fontSize;
            textStyle.fontSize = size;
            Label(text, 0f, y, color, TextAnchor.UpperCenter, Screen.width);
            textStyle.fontSize = old;
        }

        void DrawHitboxes(int k)
        {
            DrawWorldRect(wick.Hitbox, Palette.Mint);
            foreach (var b in bubbles)
            {
                float r = b.Radius;
                DrawWorldRect(new Rect(b.Position.x - r, b.Position.y - r, r * 2f, r * 2f), Palette.Cream);
            }
        }

        void DrawWorldRect(Rect r, Color c)
        {
            Vector2 a = WorldToGui(new Vector2(r.xMin, r.yMax));
            Vector2 b = WorldToGui(new Vector2(r.xMax, r.yMin));
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y, b.x - a.x, 1), whiteTex);
            GUI.DrawTexture(new Rect(a.x, b.y, b.x - a.x, 1), whiteTex);
            GUI.DrawTexture(new Rect(a.x, a.y, 1, b.y - a.y), whiteTex);
            GUI.DrawTexture(new Rect(b.x, a.y, 1, b.y - a.y), whiteTex);
            GUI.color = Color.white;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if BW_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Bubblewright
{
    /// <summary>
    /// Builds and runs the prototype entirely from code: camera, stage, Wick, harpoons, bubbles,
    /// touch controls and HUD. A run is a few generated stages with a pick-one-of-three upgrade between them;
    /// losing every heart ends the run, and the next one starts again from stage 1. Drop it on an empty GameObject, or just press Play in any scene: if no Game
    /// exists one is created automatically.
    ///
    /// Units: 1 unit = 15 art pixels. Floor at y = 0, ceiling at y = 14, walls at x = -16 and +16.
    /// The ground strip (thumb controls) is y = -4 to 0.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Game : MonoBehaviour
    {
        enum State { Intro, Play, Clear, Pick, Over }
        const string BestScoreKey = "Bubblewright.BestScore";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindAnyObjectByType<Game>() == null) new GameObject("Game").AddComponent<Game>();
        }

        Tuning T => Tuning.I;
        readonly GameInput input = new GameInput();
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly List<Bubble> hitBubbles = new List<Bubble>(); // swelling during the hit-stop, popped when it ends

        Camera cam;
        Transform world, bubbleRoot;
        Wick wick;
        readonly List<Harpoon> harpoons = new List<Harpoon>();
        Effects fx;
        TouchControlsView touchView;

        State state;
        float stateTime;
        float freeze;
        float slowMo;
        float shake;
        bool turnedForCelebration, celebrating;
        float shotTimer;
        Run run;
        int bestScore;
        bool newBest;
        List<Upgrade> choices = new List<Upgrade>();
        int selected, lastMoveDir;
        readonly Rect[] cardRects = new Rect[3]; // GUI space, written by OnGUI, read by TickPick
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
            fx = Effects.Create(world);
            wick = Wick.Create(world, fx);
            Sfx.Create(transform);
            touchView = TouchControlsView.Create(world, cam);

            try { bestScore = PlayerPrefs.GetInt(BestScoreKey, 0); } catch { bestScore = 0; }
            StartRun();
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
            fx.Clear();
            ApplyRunUpgrades();
            HideHarpoons();
            wick.ResetForStage(0f, run.Hearts);
            wick.Waving = true;
            freeze = slowMo = shake = 0f;
            shotTimer = 0f;
            turnedForCelebration = celebrating = false;

            foreach (var s in StageGenerator.Generate(run.Stage, run.Rng))
            {
                int tier = Mathf.Clamp(s.tier, 0, T.tiers.Length - 1);
                SpawnBubble(s.kind, tier, s.position, s.direction >= 0 ? 1 : -1, 0f, false);
            }
            SetState(State.Intro);
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
        }

        void TickHarpoons(float dt)
        {
            foreach (var h in harpoons) h.Tick(dt);
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
            }
            TickHarpoons(dt);

            for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(dt);

            foreach (var h in harpoons)
            {
                if (!h.Active) continue;
                for (int i = 0; i < bubbles.Count; i++)
                {
                    if (bubbles[i].Popped || !h.Hits(bubbles[i])) continue;
                    h.Hide();
                    Hit(bubbles[i]);
                    break;
                }
            }

            if (!wick.Invulnerable && !wick.Dead)
            {
                var box = wick.Hitbox;
                foreach (var b in bubbles)
                {
                    if (b.Popped || !b.Overlaps(box)) continue;
                    wick.Hurt(b.Position.x);
                    freeze = T.hurtFreeze;
                    shake = T.shakeTime;
                    Sfx.Play(Sound.Hurt);
                    if (wick.Dead)
                    {
                        HideHarpoons();
                        run.Hearts = 0;
                        RecordScore();
                        SetState(State.Over);
                        Sfx.Play(Sound.Over);
                    }
                    break;
                }
            }

            if (state == State.Play && bubbles.Count == 0 && hitBubbles.Count == 0)
            {
                slowMo = T.lastPopSlowMo;
                run.Hearts = wick.Hearts;
                if (run.IsLastStage) RecordScore();
                SetState(State.Clear);
            }
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
            for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(dt);
            if (stateTime > 0.8f && input.ConfirmPressed) StartRun();
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
            SetState(State.Pick);
        }

        /// <summary>Tap or click a card; or left/right then Enter, Space or pad A; or press 1-3.</summary>
        void TickPick(float dt)
        {
            wick.Tick(0f, dt, false);
            int dir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            bool moved = dir != 0 && dir != lastMoveDir;
            lastMoveDir = dir;
            if (stateTime < 0.35f) return; // so a tap meant for the clear screen does not pick a card

            if (moved) selected = (selected + dir + choices.Count) % choices.Count;
            if (input.DebugSpawn >= 1 && input.DebugSpawn <= choices.Count) { Choose(input.DebugSpawn - 1); return; }
            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                for (int i = 0; i < choices.Count; i++)
                    if (cardRects[i].Contains(gui)) { Choose(i); return; }
            }
            if (input.KeyConfirmPressed) Choose(selected);
        }

        void Choose(int i)
        {
            run.Take(choices[i]);
            Sfx.Play(Sound.Pop, 1.6f, 0.8f);
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
            b.Swell(T.popSwell);
            hitBubbles.Add(b);
            freeze = Mathf.Max(freeze, T.popHitStop);
            if (b.Tier == 0) shake = T.shakeTime; // only XL pops shake, so it keeps its meaning
        }

        void PopHitBubbles()
        {
            if (hitBubbles.Count == 0) return;
            foreach (var b in hitBubbles) Pop(b);
            hitBubbles.Clear();
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
            if (input.DebugSpawn > 0 && (state == State.Play || state == State.Intro))
            {
                var kind = (BubbleKind)(input.DebugSpawn - 1);
                float x = Random.Range(-T.HalfWidth + 3f, T.HalfWidth - 3f);
                SpawnBubble(kind, 0, new Vector2(x, 9f), Random.value < 0.5f ? -1 : 1, 0f, false);
            }
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
            Label($"STAGE {run.Stage}/{T.runLength}", topLeft.x + pad + run.MaxHearts * (ft.width + 2) * k + 4 * k, topLeft.y + pad + 2 * k, Palette.Cream, TextAnchor.UpperLeft, 200 * k);
            Label($"{run.Score:000000}", topRight.x - pad - 200 * k, topLeft.y + pad + 2 * k, Palette.Cream, TextAnchor.UpperRight, 200 * k);

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
                    Plate(Screen.height * 0.32f - 6 * k, (stateTime > 0.8f ? 74 : 26) * k, k);
                    Centered("THE FLAME WENT OUT", Screen.height * 0.32f, 14 * k, UiText.Primary);
                    if (stateTime > 0.8f)
                    {
                        float y0 = Screen.height * 0.32f + 20 * k;
                        Centered($"STAGE {run.Stage} OF {T.runLength}    SCORE {run.Score}    {best}", y0, 8 * k, UiText.Accent);
                        Centered($"BUILD: {run.BuildSummary()}", y0 + 12 * k, 8 * k, UiText.Primary);
                        Centered(touchUi ? "Tap to start a new run" : "Press R, Enter or Start for a new run", y0 + 24 * k, 8 * k, UiText.Secondary);
                        Centered($"seed {run.Seed}", y0 + 36 * k, 8 * k, UiText.Secondary);
                    }
                    break;
            }

            if (showHitboxes) DrawHitboxes(k);
        }

        GUIStyle cardStyle;
        Vector2 lastMouse;

        /// <summary>
        /// Three cards over a dimmed stage. On desktop the card under the mouse (or picked with the keys) glows
        /// as a whole: halo, mint border, lighter fill and a small lift. Phones have no hover, so no card is lit there.
        /// Placeholder look until the real UI pass.
        /// </summary>
        void DrawPick(int k, bool touchUi)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Palette.Ink, UiText.DimAlpha));
            float cy = Screen.height * 0.5f;
            Centered($"STAGE {run.Stage} CLEAR  ·  PICK ONE", cy - 68 * k, 12 * k, UiText.Primary);

            if (cardStyle == null) cardStyle = new GUIStyle(textStyle) { wordWrap = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
            cardStyle.fontSize = 8 * k;

            float w = 120 * k, h = 84 * k, gap = 10 * k;
            float x0 = (Screen.width - (choices.Count * w + (choices.Count - 1) * gap)) * 0.5f;
            float top = cy - 44 * k;
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
                bool lit = canHover && i == selected;
                var r = cardRects[i];
                if (lit)
                {
                    r.y -= 2 * k;
                    for (int g = 4; g >= 1; g--) // soft halo around the whole card, outermost first
                        FillRect(Grow(r, g * k), WithAlpha(Palette.Mint, 0.12f * pulse * (5 - g) / 4f));
                }
                int border = lit ? 2 * k : k;
                FillRect(r, lit ? Palette.Mint : Palette.Lilac);
                var inner = Grow(r, -border);
                FillRect(inner, lit ? UiText.CardFillHover : UiText.CardFill);

                var u = choices[i];
                var iconBox = new Rect(inner.x + 5 * k, inner.y + 5 * k, 22 * k, 22 * k);
                FillRect(iconBox, Palette.Ink);
                if (lit) FillRect(Grow(iconBox, -k), WithAlpha(Palette.Mint, 0.15f));
                int iconPx = PlaceholderArt.UpgradeIconSize;
                GUI.DrawTexture(new Rect(iconBox.x + 2 * k, iconBox.y + 2 * k, iconPx * k, iconPx * k), PlaceholderArt.I.UpgradeIcon(u.Name));

                int n = run.TimesTaken(u);
                string title = u.Max > 1 && n > 0 ? $"{u.Name} {n + 1}" : u.Name;
                Label(title, iconBox.xMax + 5 * k, iconBox.y + 6 * k, lit ? UiText.Accent : UiText.Primary, TextAnchor.UpperLeft, inner.xMax - iconBox.xMax - 8 * k);
                SetTextColor(cardStyle, lit ? UiText.Primary : UiText.Secondary);
                GUI.Label(new Rect(inner.x + 6 * k, iconBox.yMax + 4 * k, inner.width - 12 * k, inner.yMax - iconBox.yMax - 6 * k), u.Text, cardStyle);
                if (!touchUi) Label($"{i + 1}", inner.xMax - 10 * k, inner.yMax - 14 * k, UiText.Secondary, TextAnchor.UpperLeft, 10 * k);
            }

            Centered($"HEARTS {run.Hearts}/{run.MaxHearts}    NEXT: STAGE {run.Stage + 1} OF {T.runLength}", top + h + 10 * k, 8 * k, UiText.Secondary);
            Centered(touchUi ? "Tap a card" : "Left/right and Enter, 1-3, or click a card", top + h + 22 * k, 8 * k, UiText.Secondary);
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

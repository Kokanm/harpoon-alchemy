using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if BW_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Bubblewright
{
    /// <summary>
    /// Builds and runs the single prototype stage entirely from code: camera, stage, Wick, harpoon, bubbles,
    /// touch controls and HUD. Drop it on an empty GameObject, or just press Play in any scene: if no Game
    /// exists one is created automatically.
    ///
    /// Units: 1 unit = 15 art pixels. Floor at y = 0, ceiling at y = 14, walls at x = -16 and +16.
    /// The ground strip (thumb controls) is y = -4 to 0.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Game : MonoBehaviour
    {
        enum State { Intro, Play, Clear, Over }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (FindAnyObjectByType<Game>() == null) new GameObject("Game").AddComponent<Game>();
        }

        Tuning T => Tuning.I;
        readonly GameInput input = new GameInput();
        readonly List<Bubble> bubbles = new List<Bubble>();

        Camera cam;
        Transform world, bubbleRoot;
        Wick wick;
        Harpoon harpoon;
        Effects fx;
        TouchControlsView touchView;

        State state;
        float stateTime;
        float freeze;
        float slowMo;
        float shake;
        bool turnedForCelebration, celebrating;
        int score;
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
            harpoon = Harpoon.Create(world);
            wick = Wick.Create(world);
            fx = Effects.Create(world);
            touchView = TouchControlsView.Create(world, cam);

            StartStage();
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

        void StartStage()
        {
            foreach (var b in bubbles) if (b) Destroy(b.gameObject);
            bubbles.Clear();
            fx.Clear();
            harpoon.Hide();
            wick.ResetForStage(0f);
            score = 0;
            freeze = slowMo = shake = 0f;
            turnedForCelebration = celebrating = false;

            foreach (var s in T.stageBubbles)
            {
                int tier = Mathf.Clamp(s.tier, 0, T.tiers.Length - 1);
                SpawnBubble(s.kind, tier, s.position, s.direction >= 0 ? 1 : -1, 0f, false);
            }
            SetState(State.Intro);
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
                wick.Turn(false);
                SetState(State.Play);
            }
        }

        void TickPlay(float dt)
        {
            wick.Tick(input.Move, dt, true);

            if (!harpoon.Active && (input.FirePressed || (T.holdToRefire && input.FireHeld)))
            {
                harpoon.Fire(wick.LauncherTip);
                wick.OnFire();
            }
            harpoon.Tick(dt);

            for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(dt);

            if (harpoon.Active)
            {
                for (int i = 0; i < bubbles.Count; i++)
                {
                    if (!harpoon.Hits(bubbles[i])) continue;
                    harpoon.Hide();
                    Pop(bubbles[i]);
                    break;
                }
            }

            if (!wick.Invulnerable && !wick.Dead)
            {
                var box = wick.Hitbox;
                foreach (var b in bubbles)
                {
                    if (!b.Overlaps(box)) continue;
                    wick.Hurt(b.Position.x);
                    freeze = T.hurtFreeze;
                    shake = T.shakeTime;
                    if (wick.Dead)
                    {
                        harpoon.Hide();
                        SetState(State.Over);
                    }
                    break;
                }
            }

            if (state == State.Play && bubbles.Count == 0)
            {
                slowMo = T.lastPopSlowMo;
                SetState(State.Clear);
            }
        }

        void TickClear(float dt)
        {
            wick.Tick(0f, dt, false);
            harpoon.Tick(dt);
            if (!turnedForCelebration && slowMo <= 0f)
            {
                turnedForCelebration = true;
                wick.Turn(true);
            }
            if (turnedForCelebration && !celebrating && wick.FacingFront && stateTime > T.lastPopSlowMo * 0.5f + T.turnTime)
            {
                celebrating = true;
                wick.Celebrate();
            }
            if (stateTime > 1.2f && input.ConfirmPressed) StartStage();
        }

        void TickOver(float dt)
        {
            wick.Tick(0f, dt, false);
            for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(dt);
            if (stateTime > 0.8f && input.ConfirmPressed) StartStage();
        }

        void Pop(Bubble b)
        {
            bubbles.Remove(b);
            var kind = T.Kind(b.Kind);
            score += T.tiers[b.Tier].score;
            if (b.Kind == BubbleKind.Lucky) score += 500; // placeholder reward until upgrades exist

            fx.Pop(b.Position, b.Tier, kind.color);
            freeze = T.popHitStop;
            if (b.Tier == 0) shake = T.shakeTime; // only XL pops shake, so it keeps its meaning

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
            for (int i = 0; i < T.hearts; i++)
            {
                GUI.color = i < wick.Hearts ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(topLeft.x + pad + i * (ft.width + 2) * k, topLeft.y + pad, ft.width * k, ft.height * k), ft);
            }
            GUI.color = Color.white;
            Label($"STAGE 1", topLeft.x + pad + T.hearts * (ft.width + 2) * k + 4 * k, topLeft.y + pad + 2 * k, Palette.Cream, TextAnchor.UpperLeft, 200 * k);
            Label($"{score:000000}", topRight.x - pad - 200 * k, topLeft.y + pad + 2 * k, Palette.Cream, TextAnchor.UpperRight, 200 * k);

            bool touchUi = Application.isMobilePlatform || input.TouchUsed;
            if (!touchUi)
            {
                Vector2 strip = WorldToGui(new Vector2(-T.HalfWidth, -T.groundStrip * 0.5f));
                Label("A/D or arrows: move    Space: fire    Pad: stick + A    1-4: spawn bubble    F1: hitboxes    R: restart",
                    strip.x + pad, strip.y - 4 * k, Palette.Lilac, TextAnchor.UpperLeft, Screen.width);
            }

            switch (state)
            {
                case State.Clear when stateTime > 0.4f:
                    Centered("STAGE CLEAR", Screen.height * 0.32f, 16 * k, Palette.Cream);
                    if (stateTime > 1.2f) Centered(touchUi ? "Tap to play again" : "Press R, Enter or Start to play again", Screen.height * 0.32f + 22 * k, 8 * k, Palette.Lilac);
                    break;
                case State.Over:
                    Centered("THE FLAME WENT OUT", Screen.height * 0.32f, 14 * k, Palette.Cream);
                    if (stateTime > 0.8f) Centered(touchUi ? "Tap to try again" : "Press R, Enter or Start to try again", Screen.height * 0.32f + 20 * k, 8 * k, Palette.Lilac);
                    break;
            }

            if (showHitboxes) DrawHitboxes(k);
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
            textStyle.normal.textColor = Palette.Ink;
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, textStyle);
            textStyle.normal.textColor = color;
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

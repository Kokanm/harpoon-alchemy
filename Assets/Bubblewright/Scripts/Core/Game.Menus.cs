using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Pause menu and debug panel. Pause with Esc, P, pad Start or the button in the top right corner; F2 pauses
    /// straight into the debug panel. The game does not tick while paused. Cheats stay on across runs until turned off.
    /// </summary>
    public partial class Game
    {
        struct DebugOptions
        {
            public bool god;          // bubbles pass through Wick
            public bool freezeTimer;  // the storm clock stops
        }

        DebugOptions debug;
        bool paused, debugPanel;
        int pauseSelected, debugStage = 1;
        Rect pauseButtonRect;
        readonly Rect[] pauseRects = new Rect[3];
        static readonly string[] PauseItems = { "RESUME", "RESTART RUN", "DEBUG" };

        bool CanPause => state == State.Intro || state == State.Play;

        /// <summary>Handles pausing and the pause menu. True while paused, so the game skips its tick.</summary>
        bool UpdatePause()
        {
            bool tapPause = input.TapPosition is Vector2 t && pauseButtonRect.Contains(new Vector2(t.x, Screen.height - t.y));
            if (!paused)
            {
                if (!CanPause || !(input.PausePressed || input.DebugPanelPressed || tapPause)) return false;
                Pause(input.DebugPanelPressed);
                return true;
            }

            if (input.PausePressed || tapPause)
            {
                Resume();
                return true; // skip this frame too, so the tap that resumed does not also fire
            }
            if (input.DebugPanelPressed) debugPanel = !debugPanel;

            int dir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            if (dir != 0 && dir != lastMoveDir) pauseSelected = (pauseSelected + dir + PauseItems.Length) % PauseItems.Length;
            lastMoveDir = dir;

            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                for (int i = 0; i < PauseItems.Length; i++)
                    if (pauseRects[i].Contains(gui)) { Activate(i); return paused; }
            }
            if (input.KeyConfirmPressed) Activate(pauseSelected);
            return paused;
        }

        void Pause(bool openDebug)
        {
            paused = true;
            debugPanel = openDebug;
            pauseSelected = openDebug ? 2 : 0;
            debugStage = run.Stage;
            lastMoveDir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            for (int i = 0; i < pauseRects.Length; i++) pauseRects[i] = Rect.zero;
            Sfx.Play(Sound.Turn);
        }

        void Resume()
        {
            paused = false;
            debugPanel = false;
        }

        void Activate(int item)
        {
            switch (item)
            {
                case 0: Resume(); break;
                case 1: Resume(); StartRun(); break;
                case 2: debugPanel = !debugPanel; break;
            }
        }

        /// <summary>"GOD · TIMER FROZEN" for the HUD, or null when no cheat is on.</summary>
        string DebugSummary()
        {
            if (!debug.god && !debug.freezeTimer) return null;
            if (debug.god && debug.freezeTimer) return "GOD MODE · TIMER FROZEN";
            return debug.god ? "GOD MODE" : "TIMER FROZEN";
        }

        // ------------------------------------------------------------------ drawing

        void DrawPauseButton(Rect r, int k)
        {
            FillRect(r, Palette.Mist);
            FillRect(Grow(r, -k), UiText.CardFill);
            float barW = 2 * k, barH = r.height - 6 * k;
            FillRect(new Rect(r.center.x - 3 * k, r.y + 3 * k, barW, barH), UiText.Primary);
            FillRect(new Rect(r.center.x + k, r.y + 3 * k, barW, barH), UiText.Primary);
        }

        void DrawPause(int k, bool touchUi)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Palette.Ink, UiText.DimAlpha));
            float top = debugPanel ? 12 * k : Screen.height * 0.5f - 40 * k;
            Centered("PAUSED", top, 14 * k, UiText.Primary);

            float w = 84 * k, h = 22 * k, gap = 8 * k;
            float x0 = (Screen.width - (PauseItems.Length * w + (PauseItems.Length - 1) * gap)) * 0.5f;
            float y = top + 20 * k;
            bool canHover = !Application.isMobilePlatform;
            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = mouse;
            for (int i = 0; i < PauseItems.Length; i++)
            {
                pauseRects[i] = new Rect(x0 + i * (w + gap), y, w, h);
                if (canHover && mouseMoved && pauseRects[i].Contains(mouse)) pauseSelected = i;
            }
            for (int i = 0; i < PauseItems.Length; i++)
            {
                string label = i == 2 ? (debugPanel ? "HIDE DEBUG" : "DEBUG") : PauseItems[i];
                DrawMenuButton(pauseRects[i], label, canHover && i == pauseSelected, k);
            }

            if (debugPanel) DrawDebugPanel(new Rect(Screen.width * 0.5f - 220 * k, y + h + 10 * k, 440 * k, Screen.height - (y + h + 18 * k)), k);
            else Centered(touchUi ? "Tap the pause button to resume" : "Esc, P or Start to resume    F2: debug", y + h + 12 * k, 8 * k, UiText.Secondary);
        }

        /// <summary>Same look as the upgrade cards: the whole button glows when lit.</summary>
        void DrawMenuButton(Rect r, string label, bool lit, int k)
        {
            if (lit)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
                for (int g = 3; g >= 1; g--)
                    FillRect(Grow(r, g * k), WithAlpha(Palette.Mint, 0.12f * pulse * (4 - g) / 3f));
            }
            int border = lit ? 2 * k : k;
            FillRect(r, lit ? Palette.Mint : Palette.Lilac);
            FillRect(Grow(r, -border), lit ? UiText.CardFillHover : UiText.CardFill);
            Label(label, r.x, r.y + (r.height - 10 * k) * 0.5f, lit ? UiText.Accent : UiText.Primary, TextAnchor.UpperCenter, r.width);
        }

        // ------------------------------------------------------------------ debug panel

        GUIStyle dbgButton, dbgOn, dbgLabel, dbgTitle;
        int dbgScale;

        /// <summary>A button background: fill with a k-pixel lilac border, so buttons stand out from the panel (3:1).</summary>
        static Texture2D Framed(Color fill, int k)
        {
            int size = 2 * k + 1;
            var t = new Texture2D(size, size) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, x < k || y < k || x >= size - k || y >= size - k ? (Color)Palette.Lilac : fill);
            t.Apply();
            return t;
        }

        void EnsureDebugStyles(int k)
        {
            if (dbgButton == null || dbgScale != k)
            {
                dbgScale = k;
                var dbgFill = Framed(UiText.CardFill, k);
                var dbgFillHover = Framed(UiText.DebugButtonHover, k);
                var dbgFillOn = Framed(UiText.DebugButtonOn, k);
                var dbgFillPressed = Framed(Palette.Mint, k);
                dbgButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                dbgButton.normal.background = dbgFill;
                dbgButton.hover.background = dbgFillHover;
                dbgButton.active.background = dbgFillPressed;
                dbgButton.focused.background = dbgFill;
                SetTextColor(dbgButton, UiText.Primary);
                dbgButton.active.textColor = Palette.Ink;
                dbgOn = new GUIStyle(dbgButton);
                dbgOn.normal.background = dbgOn.focused.background = dbgFillOn;
                dbgOn.hover.background = dbgFillOn;
                SetTextColor(dbgOn, Palette.Ink);
                dbgLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
                SetTextColor(dbgLabel, UiText.Secondary);
                dbgTitle = new GUIStyle(dbgLabel) { fontStyle = FontStyle.Bold };
                SetTextColor(dbgTitle, UiText.Warning);
            }
            foreach (var st in new[] { dbgButton, dbgOn, dbgLabel, dbgTitle })
            {
                st.fontSize = 8 * k;
                st.fixedHeight = 15 * k;
                st.margin = new RectOffset(k, k, k, k);
                st.padding = new RectOffset(3 * k, 3 * k, 0, 0);
                st.border = new RectOffset(k, k, k, k);
            }
        }

        bool DbgButton(string text, int k, float width) => GUILayout.Button(text, dbgButton, GUILayout.Width(width * k));

        bool DbgToggle(string text, bool on, int k, float width) =>
            GUILayout.Button(text + (on ? ": ON" : ": OFF"), on ? dbgOn : dbgButton, GUILayout.Width(width * k)) ? !on : on;

        void DrawDebugPanel(Rect panel, int k)
        {
            EnsureDebugStyles(k);
            FillRect(panel, Palette.Lilac);
            FillRect(Grow(panel, -k), WithAlpha(Palette.Ink, 0.95f));

            GUILayout.BeginArea(Grow(panel, -6 * k));
            GUILayout.Label("DEBUG MODE  ·  cheats stay on across runs  ·  1-4 spawn XL bubbles, F1 hitboxes", dbgTitle);

            GUILayout.BeginHorizontal();
            debug.god = DbgToggle("GOD MODE", debug.god, k, 100);
            debug.freezeTimer = DbgToggle("FREEZE TIMER", debug.freezeTimer, k, 100);
            showHitboxes = DbgToggle("HITBOXES", showHitboxes, k, 100);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (DbgButton("STORM NOW", k, 80) && !storm && (state == State.Play || state == State.Intro)) StartStorm();
            if (DbgButton("CLEAR STAGE", k, 80)) DebugClearStage();
            if (DbgButton("FULL HEARTS", k, 80)) DebugSetHearts(run.MaxHearts, run.MaxHearts);
            if (DbgButton("+1 HEART SLOT", k, 90)) DebugSetHearts(run.MaxHearts + 1, wick.Hearts + 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("STAGE", dbgLabel, GUILayout.Width(40 * k));
            if (DbgButton("-", k, 18)) debugStage = Mathf.Max(1, debugStage - 1);
            GUILayout.Label($"{debugStage}/{T.runLength}", dbgLabel, GUILayout.Width(28 * k));
            if (DbgButton("+", k, 18)) debugStage = Mathf.Min(T.runLength, debugStage + 1);
            if (DbgButton("GO TO STAGE", k, 80)) DebugGoToStage(debugStage);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("SPAWN XL", dbgLabel, GUILayout.Width(52 * k));
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                if (DbgButton(kind.ToString().ToUpperInvariant(), k, 62)) DebugSpawn(kind);
            GUILayout.EndHorizontal();

            GUILayout.Label("ADD UPGRADE (no limits)", dbgLabel);
            for (int row = 0; row < Upgrade.All.Length; row += 4)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < Mathf.Min(row + 4, Upgrade.All.Length); i++)
                {
                    var u = Upgrade.All[i];
                    int n = run.TimesTaken(u);
                    if (DbgButton(n > 0 ? $"{u.Name} ({n})" : u.Name, k, 104)) DebugTake(u);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"BUILD: {run.BuildSummary()}", dbgLabel);
            GUILayout.EndArea();
        }

        void DebugClearStage()
        {
            foreach (var b in bubbles) if (b) Destroy(b.gameObject);
            bubbles.Clear();
            hitBubbles.Clear();
            if (state == State.Intro)
            {
                wick.Waving = false;
                wick.Turn(false);
                SetState(State.Play);
            }
            Resume();
        }

        void DebugSetHearts(int max, int hearts)
        {
            run.MaxHearts = max;
            run.Hearts = Mathf.Clamp(hearts, 1, max);
            wick.SetHearts(run.Hearts);
        }

        void DebugGoToStage(int stage)
        {
            run.Hearts = Mathf.Max(1, wick.Hearts);
            run.Stage = Mathf.Clamp(stage, 1, T.runLength);
            Resume();
            StartStage();
        }

        /// <summary>Applies an upgrade straight away, ignoring its limit.</summary>
        void DebugTake(Upgrade u)
        {
            run.Hearts = wick.Hearts;
            run.Take(u);
            run.Hearts = Mathf.Min(run.Hearts, run.MaxHearts);
            ApplyRunUpgrades();
            wick.SetHearts(run.Hearts);
        }
    }
}

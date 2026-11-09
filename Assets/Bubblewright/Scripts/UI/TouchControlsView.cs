using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Draws the thumb controls inside the ground strip, as world sprites so they stay pixel perfect.
    /// Left: move pad hint, plus a knob where the thumb landed and a dot that follows it.
    /// Right: the big mint fire button. Shown on phones, or on desktop once the mouse has been used as a finger.
    /// </summary>
    public class TouchControlsView : MonoBehaviour
    {
        SpriteRenderer hint, knob, dot, fire;
        Camera cam;

        public static TouchControlsView Create(Transform parent, Camera cam)
        {
            var go = new GameObject("Touch Controls");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TouchControlsView>();
            v.cam = cam;
            var art = PlaceholderArt.I;
            v.hint = v.Part("Move Hint", art.MoveHint, 0);
            v.knob = v.Part("Move Knob", art.MoveKnob, 1);
            v.dot = v.Part("Move Dot", art.MoveDot, 2);
            v.fire = v.Part("Fire Button", art.FireButton, 0);
            return v;
        }

        SpriteRenderer Part(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.UI, order);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        public void Refresh(GameInput input, Tuning t)
        {
            bool show = Application.isMobilePlatform || input.TouchUsed;
            float stripMid = -t.groundStrip * 0.5f - 0.2f;

            // Anchor to the visible screen edges (inside the safe area), not the arena, so thumbs reach them.
            var safe = Screen.safeArea;
            float leftX = cam.ScreenToWorldPoint(new Vector3(safe.xMin, 0f, 0f)).x;
            float rightX = cam.ScreenToWorldPoint(new Vector3(safe.xMax, 0f, 0f)).x;

            hint.enabled = show && !input.PadActive;
            float hintX = input.FlipTouchSides ? rightX - 4f : leftX + 4f;
            float fireX = input.FlipTouchSides ? leftX + 3f : rightX - 3f;
            hint.transform.position = Snap(new Vector2(hintX, stripMid), t);

            fire.enabled = show;
            fire.sprite = input.TouchFireHeld ? PlaceholderArt.I.FireButtonPressed : PlaceholderArt.I.FireButton;
            fire.transform.position = Snap(new Vector2(fireX, stripMid), t);

            knob.enabled = dot.enabled = show && input.PadActive;
            if (input.PadActive)
            {
                Vector2 o = cam.ScreenToWorldPoint(input.PadOrigin);
                Vector2 c = cam.ScreenToWorldPoint(input.PadCurrent);
                knob.transform.position = Snap(o, t);
                dot.transform.position = Snap(new Vector2(c.x, o.y), t);
            }
        }

        static Vector3 Snap(Vector2 p, Tuning t)
        {
            float px = 1f / t.pixelsPerUnit;
            return new Vector3(Mathf.Round(p.x / px) * px, Mathf.Round(p.y / px) * px, 0f);
        }
    }
}

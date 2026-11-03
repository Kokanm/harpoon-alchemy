using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Pooled sprite particles: pop flash, ring and droplets, dust puffs, muzzle flash and confetti.
    /// Everything runs on game time, so hit-stop and slow-mo freeze and slow them too.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        class Particle
        {
            public SpriteRenderer sr;
            public Vector2 velocity, size;
            public float age, life, startScale, endScale, gravity, drag, spin;
            public int frames; // > 0: lives this many ticks instead of a time (one-frame flashes)
        }

        readonly List<Particle> live = new List<Particle>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        static readonly Color[] ConfettiColors = { Palette.Coral, Palette.Azure, Palette.Plum, Palette.Lemon, Palette.Mint };

        public static Effects Create(Transform parent)
        {
            var go = new GameObject("Effects");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Effects>();
        }

        SpriteRenderer Take(Sprite sprite, int order = 0)
        {
            SpriteRenderer sr;
            if (pool.Count > 0) sr = pool.Pop();
            else
            {
                var go = new GameObject("FX");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                Game.ApplySpriteMaterial(sr);
            }
            Sorting.Apply(sr, Layer.FX, order);
            sr.sprite = sprite;
            sr.enabled = true;
            sr.color = Color.white;
            sr.transform.localRotation = Quaternion.identity;
            return sr;
        }

        Particle Add(SpriteRenderer sr, Vector2 at, float life)
        {
            sr.transform.position = at;
            var p = new Particle { sr = sr, life = life, startScale = 1f, endScale = 1f, size = Vector2.one };
            live.Add(p);
            return p;
        }

        /// <summary>One cream flash frame the size of the swollen bubble, a dashed ring and 6 droplets.</summary>
        public void Pop(Vector2 at, int tier, Color color, float swell)
        {
            var art = PlaceholderArt.I;
            var flash = Add(Take(art.BubbleFlash[tier], 2), at, 1f);
            flash.frames = 1;
            flash.startScale = flash.endScale = swell;

            var ring = Add(Take(art.PopRings[tier]), at, 0.22f);
            ring.startScale = 0.8f;
            ring.endScale = 1.25f;

            float speed = 3f + (3 - tier) * 0.8f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + 0.3f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var d = Take(art.Droplet, 1);
                d.color = i % 2 == 0 ? Color.white : color;
                var p = Add(d, at + dir * 0.3f, 0.3f);
                p.velocity = dir * speed;
                p.gravity = 12f;
            }
        }

        /// <summary>A small dust puff at the boots, drifting away from dirX (-1, 0 or +1).</summary>
        public void Dust(Vector2 at, float dirX)
        {
            var p = Add(Take(PlaceholderArt.I.Dust), at, 0.28f);
            p.velocity = new Vector2(dirX * 2.2f + Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 0.9f));
            p.drag = 6f;
            p.startScale = 0.6f;
            p.endScale = 1.3f;
        }

        /// <summary>Mint star at the launcher tip for exactly one frame.</summary>
        public void MuzzleFlash(Vector2 at)
        {
            var p = Add(Take(PlaceholderArt.I.MuzzleFlash, 3), at, 1f);
            p.frames = 1;
        }

        /// <summary>Confetti in bubble colours, thrown up from a point.</summary>
        public void Confetti(Vector2 at, int count)
        {
            var art = PlaceholderArt.I;
            for (int i = 0; i < count; i++)
            {
                var sr = Take(art.Pixel, 1);
                sr.color = ConfettiColors[i % ConfettiColors.Length];
                var p = Add(sr, at + new Vector2(Random.Range(-0.4f, 0.4f), 0f), Random.Range(0.9f, 1.4f));
                float a = Random.Range(25f, 155f) * Mathf.Deg2Rad;
                p.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(7f, 12f);
                p.gravity = 14f;
                p.drag = 1.5f;
                p.size = Random.value < 0.5f ? new Vector2(2f, 1f) : new Vector2(1f, 2f);
                p.spin = Random.Range(-720f, 720f);
            }
        }

        public void Tick(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                bool done;
                float k;
                if (p.frames > 0)
                {
                    // Shown this frame; gone on the next tick.
                    done = p.age > 0f;
                    p.age += 1f;
                    k = 0f;
                }
                else
                {
                    p.age += dt;
                    k = p.age / p.life;
                    done = k >= 1f;
                }
                if (done)
                {
                    Release(p.sr);
                    live.RemoveAt(i);
                    continue;
                }

                if (p.velocity != Vector2.zero || p.gravity != 0f)
                {
                    p.velocity.y -= p.gravity * dt;
                    if (p.drag > 0f) p.velocity *= Mathf.Max(0f, 1f - p.drag * dt);
                    p.sr.transform.position += (Vector3)(p.velocity * dt);
                }
                if (p.spin != 0f) p.sr.transform.localRotation = Quaternion.Euler(0f, 0f, p.spin * p.age);

                float s = Mathf.Lerp(p.startScale, p.endScale, k);
                p.sr.transform.localScale = new Vector3(s * p.size.x, s * p.size.y, 1f);
                if (p.frames == 0)
                {
                    var c = p.sr.color;
                    c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                    p.sr.color = c;
                }
            }
        }

        void Release(SpriteRenderer sr)
        {
            sr.enabled = false;
            pool.Push(sr);
        }

        public void Clear()
        {
            foreach (var p in live) Release(p.sr);
            live.Clear();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Minimal pop effect: dashed cream ring that grows and fades, plus 6 droplets.
    /// Just enough to read the pop; the full game-feel pass is step 3.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        class Particle
        {
            public SpriteRenderer sr;
            public Vector2 velocity;
            public float age, life, startScale, endScale;
            public bool ring;
        }

        readonly List<Particle> live = new List<Particle>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        public static Effects Create(Transform parent)
        {
            var go = new GameObject("Effects");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Effects>();
        }

        SpriteRenderer Take(Sprite sprite)
        {
            SpriteRenderer sr;
            if (pool.Count > 0) sr = pool.Pop();
            else
            {
                var go = new GameObject("FX");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                Sorting.Apply(sr, Layer.FX);
                Game.ApplySpriteMaterial(sr);
            }
            sr.sprite = sprite;
            sr.enabled = true;
            sr.color = Color.white;
            return sr;
        }

        public void Pop(Vector2 at, int tier, Color color)
        {
            var art = PlaceholderArt.I;
            var ring = Take(art.PopRings[tier]);
            ring.transform.position = at;
            live.Add(new Particle { sr = ring, life = 0.22f, startScale = 0.8f, endScale = 1.25f, ring = true });

            float speed = 3f + (3 - tier) * 0.8f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + 0.3f;
                var d = Take(art.Droplet);
                d.color = i % 2 == 0 ? Color.white : color;
                d.transform.position = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f;
                live.Add(new Particle
                {
                    sr = d,
                    velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed,
                    life = 0.3f,
                    startScale = 1f,
                    endScale = 1f,
                });
            }
        }

        public void Tick(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.age += dt;
                float k = p.age / p.life;
                if (k >= 1f)
                {
                    p.sr.enabled = false;
                    pool.Push(p.sr);
                    live.RemoveAt(i);
                    continue;
                }
                if (!p.ring)
                {
                    p.velocity.y -= 12f * dt;
                    p.sr.transform.position += (Vector3)(p.velocity * dt);
                }
                float s = Mathf.Lerp(p.startScale, p.endScale, k);
                p.sr.transform.localScale = new Vector3(s, s, 1f);
                var c = p.sr.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                p.sr.color = c;
            }
        }

        public void Clear()
        {
            foreach (var p in live)
            {
                p.sr.enabled = false;
                pool.Push(p.sr);
            }
            live.Clear();
        }
    }
}

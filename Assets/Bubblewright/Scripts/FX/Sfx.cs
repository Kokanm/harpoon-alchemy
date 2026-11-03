using UnityEngine;

namespace Bubblewright
{
    public enum Sound { Fire, Pop, Bounce, Hurt, Skid, Turn, Clear, Over }

    /// <summary>
    /// Placeholder sound: tiny chiptune blips synthesised in code at startup, so there are no audio files yet.
    /// Real sounds replace these later by swapping the clips; callers only ever say which Sound and a pitch.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        const int Voices = 10;

        static Sfx instance;
        AudioClip[] clips;
        AudioSource[] sources;
        int next;

        public static Sfx Create(Transform parent)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Sfx>();
            instance.Build();
            return instance;
        }

        void Build()
        {
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }

            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }

            clips = new AudioClip[System.Enum.GetValues(typeof(Sound)).Length];
            clips[(int)Sound.Fire] = Make("Fire", 0.09f, (t, k) => Square(Mathf.Lerp(380f, 1100f, k), t) * 0.35f * (1f - k));
            clips[(int)Sound.Pop] = Make("Pop", 0.12f, (t, k) =>
                (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 300f, k) * t) * 0.7f + Noise() * 0.5f * (1f - k) * (1f - k)) * (1f - k));
            clips[(int)Sound.Bounce] = Make("Bounce", 0.07f, (t, k) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 70f, k) * t) * 0.6f * (1f - k));
            clips[(int)Sound.Hurt] = Make("Hurt", 0.25f, (t, k) => Square(Mathf.Lerp(620f, 140f, k), t) * 0.4f * (1f - k * 0.7f) * (Mathf.FloorToInt(t * 40f) % 2 == 0 ? 1f : 0.5f));
            clips[(int)Sound.Skid] = Make("Skid", 0.08f, (t, k) => Noise() * 0.18f * (1f - k));
            clips[(int)Sound.Turn] = Make("Turn", 0.1f, (t, k) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 700f, k) * t) * 0.25f * Mathf.Sin(k * Mathf.PI));
            clips[(int)Sound.Clear] = Arpeggio("Clear", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, 0.3f);
            clips[(int)Sound.Over] = Arpeggio("Over", new[] { 392f, 329.63f, 261.63f, 196f }, 0.14f, 0.35f);
        }

        /// <summary>Plays a sound. Pitch shifts it (smaller bubbles pop higher), volume scales it.</summary>
        public static void Play(Sound s, float pitch = 1f, float volume = 1f)
        {
            var t = Tuning.I;
            if (instance == null || t.mute || t.volume <= 0f) return;
            var src = instance.sources[instance.next];
            instance.next = (instance.next + 1) % Voices;
            src.pitch = pitch * Random.Range(0.97f, 1.03f); // a little variety so repeats do not drone
            src.volume = volume * t.volume;
            src.clip = instance.clips[(int)s];
            src.Play();
        }

        // ------------------------------------------------------------------ synthesis

        static System.Random rng = new System.Random(3);
        static float Noise() => (float)rng.NextDouble() * 2f - 1f;
        static float Square(float freq, float t) => Mathf.Repeat(freq * t, 1f) < 0.5f ? 1f : -1f;

        /// <summary>Builds a mono clip from f(time, progress 0..1), with a 3 ms fade in so it never clicks.</summary>
        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                data[i] = Mathf.Clamp(f(t, (float)i / n) * Mathf.Clamp01(t / 0.003f), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Arpeggio(string name, float[] notes, float noteTime, float amp)
        {
            float total = notes.Length * noteTime + 0.15f; // last note rings a little longer
            return Make(name, total, (t, k) =>
            {
                int i = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / noteTime));
                float local = t - i * noteTime;
                float len = i == notes.Length - 1 ? noteTime + 0.15f : noteTime;
                return Square(notes[i], t) * amp * (1f - local / len);
            });
        }
    }
}

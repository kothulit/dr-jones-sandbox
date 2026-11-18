using UnityEngine;

namespace DrJones
{
    public static class ChiptuneSynth
    {
        const int Rate = 22050;

        public static AudioClip Melody(string name, int[] midi, float beatSeconds, bool loop)
        {
            int total = 0;
            var lengths = new int[midi.Length];
            for (int i = 0; i < midi.Length; i++)
            {
                lengths[i] = Mathf.Max(1, Mathf.RoundToInt(Rate * beatSeconds));
                total += lengths[i];
            }

            var data = new float[total];
            int cursor = 0;
            for (int i = 0; i < midi.Length; i++)
            {
                float freq = MidiToFreq(midi[i]);
                float bass = MidiToFreq(midi[i] - 12);
                int n = lengths[i];
                for (int s = 0; s < n; s++)
                {
                    float t = s / (float)Rate;
                    float env = Envelope(s, n, 0.01f, 0.08f);
                    float lead = Square(freq, t) * 0.28f;
                    float low = Triangle(bass, t) * 0.16f;
                    data[cursor++] = (lead + low) * env;
                }
            }

            return Clip(name, data, loop);
        }

        public static AudioClip Arrange(string name, int[] lead, int[] bass, float beatSeconds, bool loop)
        {
            int count = lead.Length;
            int step = Mathf.Max(1, Mathf.RoundToInt(Rate * beatSeconds));
            var data = new float[step * count];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                int note = lead[i];
                int bassNote = bass[i % bass.Length];
                float leadF = note < 0 ? 0f : MidiToFreq(note);
                float harmF = note < 0 ? 0f : MidiToFreq(note - 3);
                float bassF = bassNote < 0 ? 0f : MidiToFreq(bassNote);
                bool hat = (i & 1) == 0;
                for (int s = 0; s < step; s++)
                {
                    float t = s / (float)Rate;
                    float env = note < 0 ? 0f : Envelope(s, step, 0.008f, 0.045f);
                    float low = bassNote < 0 ? 0f : Envelope(s, step, 0.015f, 0.07f);
                    float noise = hat && s < step * 0.05f ? ((float)rng.NextDouble() * 2f - 1f) * 0.06f : 0f;
                    float sample = 0f;
                    if (note >= 0)
                        sample += Square(leadF, t) * 0.2f * env + Triangle(harmF, t) * 0.08f * env;
                    if (bassNote >= 0)
                        sample += Triangle(bassF, t) * 0.16f * low;
                    data[i * step + s] = Mathf.Clamp(sample + noise, -0.95f, 0.95f);
                }
            }

            return Clip(name, data, loop);
        }

        public static AudioClip Rumble(string name, float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * seconds));
            var data = new float[n];
            var rng = new System.Random(19);
            float brown = 0f;
            float gravel = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                brown = Mathf.Clamp(brown + white * 0.012f, -1f, 1f) * 0.996f;
                gravel += (white - gravel) * 0.12f;
                float t = i / (float)Rate;
                float roll = Mathf.Sin(t * Mathf.PI * 2f * 18f) * 0.08f
                    + Mathf.Sin(t * Mathf.PI * 2f * 27.5f + 0.4f) * 0.05f
                    + Mathf.Sin(t * Mathf.PI * 2f * 9f) * 0.07f;
                float grit = gravel * 0.07f * (0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f * 3.1f));
                data[i] = Mathf.Clamp(brown * 0.55f + roll + grit, -0.7f, 0.7f) * 0.55f;
            }

            int fade = Mathf.Min(n / 4, Rate / 4);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] = data[i] * w + data[n - fade + i] * (1f - w);
            }

            var trimmed = new float[n - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            return Clip(name, trimmed, true);
        }

        public static AudioClip StoneGrind(string name, float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * seconds));
            var data = new float[n];
            var rng = new System.Random(73);
            float brown = 0f;
            float grit = 0f;
            float scrape = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                brown = Mathf.Clamp(brown + white * 0.018f, -1f, 1f) * 0.994f;
                grit += (white - grit) * 0.28f;
                scrape += (white - scrape) * 0.045f;
                float t = i / (float)Rate;
                float drag = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 1.7f);
                float grain = 0.7f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f * 7.4f + 0.6f);
                float rock = Mathf.Sin(t * Mathf.PI * 2f * 38f) * 0.07f
                    + Mathf.Sin(t * Mathf.PI * 2f * 52f + 1.1f) * 0.05f
                    + Mathf.Sin(t * Mathf.PI * 2f * 19f) * 0.08f;
                float crackle = 0f;
                if (rng.NextDouble() < 0.012f)
                    crackle = ((float)rng.NextDouble() * 2f - 1f) * 0.22f;
                float sample = brown * 0.42f * drag
                    + scrape * 0.28f * grain
                    + grit * 0.16f
                    + rock
                    + crackle;
                data[i] = Mathf.Clamp(sample, -0.75f, 0.75f) * 0.7f;
            }

            int fade = Mathf.Min(n / 5, Rate / 5);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] *= w;
                data[n - fade + i] *= 1f - w;
            }

            return Clip(name, data, true);
        }

        public static AudioClip Blip(string name, float freq, float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Square(freq, t) * Envelope(i, n, 0.005f, 0.04f) * 0.35f;
            }

            return Clip(name, data, false);
        }

        public static AudioClip Sweep(string name, float from, float to, float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * seconds));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float freq = Mathf.Lerp(from, to, k);
                phase += freq / Rate;
                data[i] = (phase - Mathf.Floor(phase) < 0.5f ? 1f : -1f) * Envelope(i, n, 0.01f, 0.05f) * 0.32f;
            }

            return Clip(name, data, false);
        }

        public static AudioClip Noise(string name, float seconds, float volume, bool loop, float low)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * seconds));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                prev = Mathf.Lerp(prev, white, 1f - low);
                float env = loop ? 1f : Envelope(i, n, 0.005f, 0.05f);
                data[i] = prev * volume * env;
            }

            return Clip(name, data, loop);
        }

        public static AudioClip SoftStep(string name)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * 0.14f));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float brown = 0f;
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.985f;
                low += (brown - low) * 0.08f;
                float t = i / (float)Rate;
                float thump = Mathf.Sin(t * Mathf.PI * 2f * 78f) * Mathf.Exp(-t * 28f);
                float env = Envelope(i, n, 0.006f, 0.09f);
                data[i] = Mathf.Clamp((thump * 0.22f + low * 0.18f) * env, -0.45f, 0.45f);
            }

            return Clip(name, data, false);
        }

        public static AudioClip Arpeggio(string name, int[] midi)
        {
            int step = Mathf.RoundToInt(Rate * 0.07f);
            var data = new float[step * midi.Length];
            for (int i = 0; i < midi.Length; i++)
            {
                float freq = MidiToFreq(midi[i]);
                for (int s = 0; s < step; s++)
                {
                    float t = s / (float)Rate;
                    data[i * step + s] = Square(freq, t) * Envelope(s, step, 0.004f, 0.02f) * 0.32f;
                }
            }

            return Clip(name, data, false);
        }

        static float MidiToFreq(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        static float Square(float freq, float time)
        {
            float p = freq * time;
            return (p - Mathf.Floor(p) < 0.5f) ? 1f : -1f;
        }

        static float Triangle(float freq, float time)
        {
            float p = freq * time;
            float f = p - Mathf.Floor(p);
            return 4f * Mathf.Abs(f - 0.5f) - 1f;
        }

        static float Envelope(int sample, int total, float attack, float release)
        {
            float t = sample / (float)Rate;
            float dur = total / (float)Rate;
            float a = Mathf.Clamp01(t / Mathf.Max(0.001f, attack));
            float r = Mathf.Clamp01((dur - t) / Mathf.Max(0.001f, release));
            return Mathf.Min(a, r);
        }

        static AudioClip Clip(string name, float[] data, bool loop)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            clip.name = name;
            return clip;
        }
    }
}

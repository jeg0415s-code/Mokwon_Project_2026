using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HappyBalloon
{
    /// <summary>
    /// 오디오 파일 없이 모든 효과음을 코드로 합성하는 사운드 뱅크 + 재생 도우미.
    /// </summary>
    public class HB_Audio : MonoBehaviour
    {
        public static HB_Audio I;
        const int SR = 22050;

        readonly Dictionary<string, AudioClip> bank = new Dictionary<string, AudioClip>();
        System.Random rng = new System.Random(20261111);
        AudioSource ui, ambient, music, heart, breath;
        float ambientTarget, musicTarget, heartTarget, breathTarget, heartPitch = 1f;

        static readonly string[] Prewarm =
        {
            "step", "step_heavy", "squeak", "hiss_in", "hiss_out", "pop", "crash", "scream", "creak", "clunk",
            "hum_on", "drone", "wind", "chase", "heartbeat", "breath", "musicbox", "slam", "bang", "scrape",
            "giggle", "windup", "pickup", "page", "door", "sting", "puff", "buzz", "deflate_end", "stretch", "click", "chain"
        };

        void Awake()
        {
            I = this;
            ui = NewSource("UI", false);
            ambient = NewSource("Ambient", true);
            music = NewSource("Music", true);
            heart = NewSource("Heartbeat", true);
            breath = NewSource("Breath", true);
            heart.clip = Get("heartbeat"); heart.volume = 0f; heart.Play();
            breath.clip = Get("breath"); breath.volume = 0f; breath.Play();
        }

        IEnumerator Start()
        {
            foreach (var id in Prewarm) { Get(id); yield return null; }
        }

        AudioSource NewSource(string n, bool loop)
        {
            var g = new GameObject("Audio_" + n);
            g.transform.SetParent(transform, false);
            var s = g.AddComponent<AudioSource>();
            s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            ambient.volume = Mathf.MoveTowards(ambient.volume, ambientTarget, dt * 0.4f);
            music.volume = Mathf.MoveTowards(music.volume, musicTarget, dt * 0.5f);
            heart.volume = Mathf.MoveTowards(heart.volume, heartTarget, dt * 1.2f);
            heart.pitch = Mathf.MoveTowards(heart.pitch, heartPitch, dt * 0.8f);
            breath.volume = Mathf.MoveTowards(breath.volume, breathTarget, dt * 0.8f);
        }

        // ------------------------------------------------------------------ public API
        public void SetAmbient(string id, float vol)
        {
            var c = Get(id);
            if (ambient.clip != c) { ambient.clip = c; ambient.volume = 0f; ambient.Play(); }
            ambientTarget = vol;
        }

        public void SetMusic(string id, float vol)
        {
            if (string.IsNullOrEmpty(id)) { musicTarget = 0f; return; }
            var c = Get(id);
            if (music.clip != c) { music.clip = c; music.volume = 0f; music.Play(); }
            musicTarget = vol;
        }

        public void StopMusicNow() { musicTarget = 0f; music.volume = 0f; }
        public void SetHeartbeat(float k) { heartTarget = Mathf.Clamp01(k) * 0.9f; heartPitch = Mathf.Lerp(0.85f, 1.75f, k); }
        public void SetBreath(float k) { breathTarget = Mathf.Clamp01(k) * 0.6f; }

        public void Play2D(string id, float vol = 1f, float pitch = 1f)
        {
            PlayAt(id, Vector3.zero, vol, pitch, 10f, false);
        }

        public AudioSource PlayAt(string id, Vector3 pos, float vol = 1f, float pitch = 1f, float maxDist = 35f, bool spatial = true)
        {
            var clip = Get(id);
            var g = new GameObject("SFX_" + id);
            g.transform.position = pos;
            var s = g.AddComponent<AudioSource>();
            s.clip = clip; s.volume = vol; s.pitch = pitch;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = maxDist; s.dopplerLevel = 0f;
            s.Play();
            Destroy(g, clip.length / Mathf.Max(0.1f, pitch) + 0.25f);
            return s;
        }

        public AudioSource Attach(string id, Transform parent, float vol, float maxDist = 25f, bool loop = true)
        {
            var g = new GameObject("Loop_" + id);
            g.transform.SetParent(parent, false);
            var s = g.AddComponent<AudioSource>();
            s.clip = Get(id); s.loop = loop; s.volume = vol;
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = maxDist; s.dopplerLevel = 0f;
            if (loop) s.Play();
            return s;
        }

        public AudioClip Get(string id)
        {
            if (bank.TryGetValue(id, out var c)) return c;
            float[] d = Synth(id);
            c = AudioClip.Create("HB_" + id, d.Length, 1, SR, false);
            c.SetData(d, 0);
            bank[id] = c;
            return c;
        }

        // ------------------------------------------------------------------ synthesis
        float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float LPk(float fc) => 1f - Mathf.Exp(-2f * Mathf.PI * fc / SR);
        static float[] Buf(float sec) => new float[Mathf.Max(1, (int)(sec * SR))];
        static float Saw(float ph) => 2f * (ph - Mathf.Floor(ph)) - 1f;

        static float[] Norm(float[] d, float peak)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            if (m > 1e-5f) { float k = peak / m; for (int i = 0; i < d.Length; i++) d[i] *= k; }
            return d;
        }

        static void EdgeFade(float[] d, float sec)
        {
            int n = Mathf.Min(d.Length / 2, (int)(sec * SR));
            for (int i = 0; i < n; i++) { float k = i / (float)n; d[i] *= k; d[d.Length - 1 - i] *= k; }
        }

        float[] Synth(string id)
        {
            switch (id)
            {
                case "step": return Step();
                case "step_heavy": return Heavy();
                case "squeak": return Squeak(0.35f, 700f, 1250f);
                case "stretch": return Squeak(0.7f, 180f, 360f);
                case "hiss_in": return Hiss(5200f, 700f, 6f, 0f);
                case "hiss_out": return Hiss(2600f, 260f, 12f, 0.35f);
                case "pop": return Pop();
                case "crash": return Crash();
                case "scream": return Scream();
                case "creak": return Creak();
                case "clunk": return Clunk();
                case "hum_on": return HumOn();
                case "drone": return Drone();
                case "wind": return Wind();
                case "chase": return Chase();
                case "heartbeat": return Heartbeat();
                case "breath": return Breath();
                case "musicbox": return MusicBox();
                case "slam": return Slam(1.3f, 1f);
                case "bang": return Slam(0.7f, 0.6f);
                case "scrape": return Scrape();
                case "giggle": return Giggle();
                case "windup": return Windup();
                case "pickup": return Chime();
                case "page": return Page();
                case "door": return DoorRumble();
                case "sting": return Sting();
                case "puff": return Puff();
                case "buzz": return Buzz();
                case "deflate_end": return DeflateEnd();
                case "click": return Click();
                case "chain": return Chain();
                default: return Buf(0.1f);
            }
        }

        float[] Step()
        {
            var d = Buf(0.14f); float k = LPk(1100f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; y += k * (N() - y);
                d[i] = y * Mathf.Exp(-t * 38f) + 0.6f * Sin(95f, t) * Mathf.Exp(-t * 45f);
            }
            return Norm(d, 0.5f);
        }

        float[] Heavy()
        {
            var d = Buf(0.5f); float k = LPk(320f), y = 0f, ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; y += k * (N() - y);
                float f = Mathf.Lerp(75f, 38f, t / 0.5f); ph += f / SR;
                float squish = 0.15f * Sin(240f + 60f * Sin(9f, t), t) * Mathf.Exp(-t * 10f);
                d[i] = Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Exp(-t * 8f) + 0.7f * y * Mathf.Exp(-t * 13f) + squish;
            }
            return Norm(d, 0.95f);
        }

        float[] Squeak(float len, float f0, float f1)
        {
            var d = Buf(len); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, u = t / len;
                float f = Mathf.Lerp(f0, f1, Mathf.Sin(u * Mathf.PI * 0.8f)) + 35f * Sin(27f, t);
                ph += f / SR;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Pow(1f - u, 1.4f);
                d[i] = env * (Mathf.Sin(2f * Mathf.PI * ph) + 0.35f * Mathf.Sin(4f * Mathf.PI * ph) + 0.2f * N() * 0.2f);
            }
            return Norm(d, 0.6f);
        }

        float[] Hiss(float hi, float lo, float flutter, float buzz)
        {
            var d = Buf(1f); float kh = LPk(hi), kl = LPk(lo), a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float n = N(); a += kh * (n - a); b += kl * (n - b);
                d[i] = (a - b) * (0.8f + 0.2f * Sin(flutter, t)) + buzz * 0.25f * Saw(90f * t) * (0.5f + 0.5f * Sin(flutter, t));
            }
            EdgeFade(d, 0.01f);
            return Norm(d, 0.5f);
        }

        float[] Pop()
        {
            var d = Buf(0.3f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = N() * Mathf.Exp(-t * 55f) + 0.8f * Sin(130f, t) * Mathf.Exp(-t * 28f);
            }
            return Norm(d, 1f);
        }

        float[] Crash()
        {
            var d = Buf(1.8f); float k1 = LPk(1600f), k2 = LPk(180f), a = 0f, b = 0f;
            var hits = new float[12]; for (int h = 0; h < hits.Length; h++) hits[h] = 0.1f + (float)rng.NextDouble() * 1.3f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float n = N(); a += k1 * (n - a); b += k2 * (n - b);
                float v = a * Mathf.Exp(-t * 2.4f) + 2.2f * b * Mathf.Exp(-t * 1.4f);
                foreach (var h in hits) if (t > h) v += 0.5f * N() * Mathf.Exp(-(t - h) * 60f);
                d[i] = v;
            }
            return Norm(d, 1f);
        }

        float[] Scream()
        {
            float len = 1.7f; var d = Buf(len); var ph = new float[5]; float k = LPk(3500f), y = 0f;
            float[] det = { 1f, 1.013f, 0.987f, 1.5f, 2.02f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, u = t / len;
                float f = Mathf.Lerp(380f, 820f, Mathf.Sqrt(u)) * (1f + 0.05f * Sin(7.5f, t));
                float v = 0f;
                for (int j = 0; j < 5; j++) { ph[j] += f * det[j] / SR; v += Saw(ph[j]) * (j < 3 ? 0.5f : 0.25f); }
                y += k * (v - y);
                float env = Mathf.Min(1f, t / 0.03f) * (u > 0.8f ? (1f - u) / 0.2f : 1f);
                d[i] = (y + 0.25f * N()) * env;
            }
            return Norm(d, 1f);
        }

        float[] Creak()
        {
            float len = 2.2f; var d = Buf(len); float ph = 0f, k = LPk(1700f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, u = t / len;
                float f = 38f + 22f * Sin(0.6f, t) + 6f * N();
                ph += f / SR;
                float pulse = Mathf.Pow(Mathf.Max(0f, Saw(ph)), 6f) * (0.6f + 0.4f * N());
                y += k * (pulse - y);
                d[i] = y * Mathf.Sin(Mathf.PI * u);
            }
            return Norm(d, 0.75f);
        }

        float[] Clunk()
        {
            var d = Buf(0.9f); float k = LPk(800f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; y += k * (N() - y);
                d[i] = 1.4f * y * Mathf.Exp(-t * 30f) + Sin(170f, t) * Mathf.Exp(-t * 11f) + 0.35f * Sin(523f, t) * Mathf.Exp(-t * 5f) + 0.15f * Sin(1093f, t) * Mathf.Exp(-t * 7f);
            }
            return Norm(d, 0.9f);
        }

        float[] HumOn()
        {
            float len = 2.6f; var d = Buf(len); float ph = 0f, k = LPk(700f), y = 0f;
            float[] relays = { 0.15f, 0.55f, 0.9f, 1.25f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, u = t / len;
                ph += Mathf.Lerp(35f, 60f, Mathf.Min(1f, u * 1.6f)) / SR;
                y += k * (Saw(ph) - y);
                float v = y * Mathf.Min(1f, u * 1.4f) * (u > 0.85f ? (1f - u) / 0.15f : 1f);
                foreach (var r in relays) if (t > r) v += 0.8f * N() * Mathf.Exp(-(t - r) * 90f);
                d[i] = v;
            }
            return Norm(d, 0.8f);
        }

        float[] Drone()
        {
            float len = 8f; var d = Buf(len); float k = LPk(180f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float am = 0.65f + 0.35f * Sin(0.25f, t);
                y += k * (N() - y);
                d[i] = am * (Sin(55f, t) + 0.6f * Sin(82.5f, t) + 0.3f * Sin(110.25f, t) + 0.15f * Sin(146.75f, t)) + 0.9f * y;
            }
            EdgeFade(d, 0.05f);
            return Norm(d, 0.45f);
        }

        float[] Wind()
        {
            float len = 6f; var d = Buf(len); float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float fc = 450f + 300f * Sin(1f / 3f, t);
                y += LPk(fc) * (N() - y);
                d[i] = y * (0.6f + 0.4f * Sin(0.5f, t));
            }
            EdgeFade(d, 0.08f);
            return Norm(d, 0.5f);
        }

        float[] Chase()
        {
            float len = 4f; var d = Buf(len); float k = LPk(1300f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float beat = t % 0.5f;
                float kick = Sin(Mathf.Lerp(70f, 40f, beat / 0.5f), beat) * Mathf.Exp(-beat * 10f);
                float strings = Saw(233f * t) + Saw(247f * t) + 0.6f * Saw(311.25f * t);
                y += k * (strings - y);
                float trem = 0.6f + 0.4f * Sin(8f, t);
                d[i] = 1.2f * kick + 0.3f * y * trem + 0.06f * Sin(1661.5f, t) * (0.5f + 0.5f * Sin(0.25f, t));
            }
            return Norm(d, 0.8f);
        }

        float[] Heartbeat()
        {
            var d = Buf(1f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float v = Sin(55f, t) * Mathf.Exp(-t * 18f) * (t < 0.25f ? 1f : 0f);
                float t2 = t - 0.28f;
                if (t2 > 0f) v += 0.75f * Sin(47f, t2) * Mathf.Exp(-t2 * 16f);
                d[i] = v;
            }
            return Norm(d, 0.9f);
        }

        float[] Breath()
        {
            float len = 2.4f; var d = Buf(len); float kh = LPk(1600f), kl = LPk(300f), a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float n = N(); a += kh * (n - a); b += kl * (n - b);
                float env = 0f;
                if (t < 0.9f) env = 0.6f * Mathf.Sin(Mathf.PI * t / 0.9f);
                else if (t > 1.1f && t < 2.2f) env = Mathf.Sin(Mathf.PI * (t - 1.1f) / 1.1f);
                d[i] = (a - b) * env;
            }
            return Norm(d, 0.4f);
        }

        float[] MusicBox()
        {
            // 직접 작곡한 단조 자장가 (24음, 0.4초 간격)
            int[] notes = { 81, 84, 88, 84, 79, 83, 86, 83, 77, 81, 84, 81, 76, 80, 83, 86, 81, 84, 88, 89, 88, 84, 80, 81 };
            float step = 0.4f, len = notes.Length * step; var d = Buf(len);
            for (int n = 0; n < notes.Length; n++)
            {
                float f = 440f * Mathf.Pow(2f, (notes[n] - 69) / 12f) * (1f + ((n * 7) % 5 - 2) * 0.004f);
                int start = (int)(n * step * SR);
                int dur = (int)(1.6f * SR);
                for (int i = 0; i < dur; i++)
                {
                    int idx = (start + i) % d.Length;
                    float t = i / (float)SR;
                    float wow = 1f + 0.003f * Sin(0.7f, (start + i) / (float)SR);
                    d[idx] += Mathf.Exp(-t * 3.2f) * (Sin(f * wow, t) + 0.3f * Sin(f * 2.01f, t) * Mathf.Exp(-t * 6f) + 0.12f * Sin(f * 3.98f, t) * Mathf.Exp(-t * 9f));
                }
            }
            return Norm(d, 0.5f);
        }

        float[] Slam(float len, float body)
        {
            var d = Buf(len); float k = LPk(3000f), kb = LPk(120f), y = 0f, b = 0f;
            float[] partials = { 211f, 377f, 563f, 841f, 1187f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float n = N(); y += k * (n - y); b += kb * (n - b);
                float v = y * Mathf.Exp(-t * 10f) + body * 2f * b * Mathf.Exp(-t * 4f);
                for (int p = 0; p < partials.Length; p++) v += 0.18f * Sin(partials[p], t) * Mathf.Exp(-t * (2.5f + p));
                d[i] = v;
            }
            return Norm(d, 1f);
        }

        float[] Scrape()
        {
            float len = 1.6f; var d = Buf(len); float k = LPk(4200f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                y += k * (N() - y);
                float g = Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 6.5f * t + 3f * Sin(1.3f, t))), 4f);
                d[i] = y * g * Mathf.Sin(Mathf.PI * t / len);
            }
            return Norm(d, 0.6f);
        }

        float[] Giggle()
        {
            float len = 1.5f; var d = Buf(len); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                int burst = (int)(t / 0.19f); float bt = t - burst * 0.19f;
                float env = burst < 6 && bt < 0.13f ? Mathf.Sin(Mathf.PI * bt / 0.13f) : 0f;
                float f = (560f - burst * 28f) * (1f + 0.04f * Sin(11f, t));
                ph += f / SR;
                float s = Mathf.Sin(2f * Mathf.PI * ph);
                d[i] = env * (s + 0.5f * Mathf.Sin(4f * Mathf.PI * ph) + 0.25f * Mathf.Sin(6f * Mathf.PI * ph) + 0.15f * N()) * (1f - t / len * 0.5f);
            }
            return Norm(d, 0.55f);
        }

        float[] Windup()
        {
            float len = 1.3f; var d = Buf(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float ct = t % 0.065f;
                d[i] = N() * Mathf.Exp(-ct * 420f) * 0.8f + 0.4f * Sin(2600f, ct) * Mathf.Exp(-ct * 160f);
            }
            return Norm(d, 0.6f);
        }

        float[] Chime()
        {
            var d = Buf(0.9f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float t2 = Mathf.Max(0f, t - 0.09f);
                d[i] = Sin(660f, t) * Mathf.Exp(-t * 5f) + (t > 0.09f ? Sin(990f, t2) * Mathf.Exp(-t2 * 4f) : 0f);
            }
            return Norm(d, 0.45f);
        }

        float[] Page()
        {
            var d = Buf(0.4f); float kh = LPk(6000f), kl = LPk(900f), a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float n = N(); a += kh * (n - a); b += kl * (n - b);
                float g = 0.4f + 0.6f * Mathf.Abs(Sin(23f, t + 0.01f * N()));
                d[i] = (a - b) * g * Mathf.Sin(Mathf.PI * t / 0.4f);
            }
            return Norm(d, 0.45f);
        }

        float[] DoorRumble()
        {
            float len = 1.5f; var d = Buf(len); float k = LPk(260f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; y += k * (N() - y);
                d[i] = 2f * y * Mathf.Sin(Mathf.PI * t / len) + 0.08f * Sin(620f + 40f * Sin(3f, t), t) * Mathf.Sin(Mathf.PI * t / len);
            }
            return Norm(d, 0.7f);
        }

        float[] Sting()
        {
            float len = 3f; var d = Buf(len);
            float[] fr = { 311.1f, 329.6f, 349.2f, 466.2f, 58.3f, 622.3f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float v = 0f;
                for (int j = 0; j < fr.Length; j++) v += Saw(fr[j] * t * (1f + 0.002f * j)) * (j == 4 ? 1.2f : 0.35f);
                d[i] = (v + 0.6f * N() * Mathf.Exp(-t * 6f)) * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 1.1f);
            }
            return Norm(d, 1f);
        }

        float[] Puff()
        {
            var d = Buf(0.25f); float k = LPk(2000f), y = 0f;
            for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y += k * (N() - y); d[i] = y * Mathf.Sin(Mathf.PI * t / 0.25f); }
            return Norm(d, 0.3f);
        }

        float[] Buzz()
        {
            var d = Buf(1f); float k = LPk(2200f), y = 0f;
            for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y += k * (Saw(120f * t) - y); d[i] = y * (0.8f + 0.2f * Sin(3f, t)); }
            return Norm(d, 0.3f);
        }

        float[] DeflateEnd()
        {
            float len = 0.9f; var d = Buf(len); float ph = 0f, k = LPk(1800f), y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, u = t / len;
                float f = Mathf.Lerp(190f, 70f, u) * (1f + 0.25f * Sin(31f, t));
                ph += f / SR;
                y += k * (Saw(ph) + 0.4f * N() - y);
                d[i] = y * Mathf.Min(1f, t / 0.02f) * (1f - u);
            }
            return Norm(d, 0.7f);
        }

        float[] Click()
        {
            var d = Buf(0.06f);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = N() * Mathf.Exp(-t * 300f) + 0.5f * Sin(1800f, t) * Mathf.Exp(-t * 200f); }
            return Norm(d, 0.5f);
        }

        float[] Chain()
        {
            float len = 1.1f; var d = Buf(len);
            float[] hits = { 0f, 0.12f, 0.2f, 0.34f, 0.5f, 0.62f, 0.8f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR; float v = 0f;
                for (int h = 0; h < hits.Length; h++)
                {
                    float ht = t - hits[h]; if (ht < 0f) continue;
                    v += (Sin(2300f + h * 210f, ht) + 0.5f * Sin(3700f + h * 90f, ht)) * Mathf.Exp(-ht * 35f) * (1f - h * 0.1f);
                }
                d[i] = v;
            }
            return Norm(d, 0.6f);
        }
    }
}

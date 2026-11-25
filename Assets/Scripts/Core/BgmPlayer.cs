using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 商场背景音乐。
    /// - 如果 Resources/Audio/bgm 存在音频文件，就播放它；
    /// - 否则程序合成一段原创的"商场轻音乐"循环（Bossa 风电钢琴 + 贝斯 + 颤音琴旋律 + 沙锤），
    ///   并模拟吊顶喇叭的音色和混响；另叠一层商场环境底噪（人声嘈杂 + 远处扫码声）。
    /// M 静音，- / = 调音量。
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer I { get; private set; }

        const int SR = 32000;
        const float Bpm = 92f;

        AudioSource _music, _ambience;
        Task<float[]> _musicTask, _ambTask;
        float _volume = 0.55f;
        bool _muted;

        public float Volume => _volume;
        public bool Muted => _muted;
        public bool Ready => _music != null && _music.clip != null;

        void Awake()
        {
            I = this;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0;
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true;
            _ambience.playOnAwake = false;
            _ambience.spatialBlend = 0;

            var custom = Resources.Load<AudioClip>("Audio/bgm");
            if (custom != null)
            {
                _music.clip = custom;
                _music.Play();
            }
            else
            {
                _musicTask = Task.Run(() => RenderMusic(20251124));
            }
            _ambTask = Task.Run(() => RenderAmbience(7));
            ApplyVolume();
        }

        void Update()
        {
            if (_musicTask != null && _musicTask.IsCompleted)
            {
                if (_musicTask.Status == TaskStatus.RanToCompletion)
                {
                    var data = _musicTask.Result;
                    var clip = AudioClip.Create("CatMart BGM", data.Length, 1, SR, false);
                    clip.SetData(data, 0);
                    _music.clip = clip;
                    _music.Play();
                }
                else Debug.LogWarning("BGM 合成失败: " + _musicTask.Exception);
                _musicTask = null;
            }
            if (_ambTask != null && _ambTask.IsCompleted)
            {
                if (_ambTask.Status == TaskStatus.RanToCompletion)
                {
                    var data = _ambTask.Result;
                    var clip = AudioClip.Create("Mall Ambience", data.Length, 1, SR, false);
                    clip.SetData(data, 0);
                    _ambience.clip = clip;
                    _ambience.Play();
                }
                _ambTask = null;
            }

            if (Input.GetKeyDown(KeyCode.M)) { _muted = !_muted; ApplyVolume(); }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) { _volume = Mathf.Clamp01(_volume - 0.1f); ApplyVolume(); }
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) { _volume = Mathf.Clamp01(_volume + 0.1f); ApplyVolume(); }
        }

        void ApplyVolume()
        {
            _music.volume = _muted ? 0 : _volume;
            _ambience.volume = _muted ? 0 : _volume * 0.6f;
        }

        // =====================================================================
        //  音乐合成（在后台线程运行，不能调用 Unity API；只用 System.Math）
        // =====================================================================

        static float Midi(int n) => 440f * (float)Math.Pow(2, (n - 69) / 12.0);

        struct Chord
        {
            public int Bass;
            public int[] Notes;
            public Chord(int bass, params int[] notes) { Bass = bass; Notes = notes; }
        }

        static float[] RenderMusic(int seed)
        {
            var rng = new System.Random(seed);
            Chord Fmaj7 = new Chord(41, 53, 57, 60, 64);
            Chord Em7 = new Chord(40, 52, 55, 59, 62);
            Chord Dm7 = new Chord(38, 53, 57, 60, 62);
            Chord G7 = new Chord(43, 53, 59, 62, 65);
            Chord Cmaj7 = new Chord(36, 52, 55, 59, 60);
            Chord Am7 = new Chord(45, 52, 55, 57, 60);
            Chord E7 = new Chord(40, 52, 56, 59, 62);
            Chord[] prog =
            {
                Fmaj7, Em7, Dm7, G7, Cmaj7, Am7, Dm7, G7,
                Fmaj7, Em7, Am7, E7, Dm7, G7, Cmaj7, G7
            };

            float beat = 60f / Bpm;
            float bar = beat * 4;
            int bars = prog.Length;
            int len = (int)(bars * bar * SR);
            var buf = new float[len];

            // 电钢琴和弦：Bossa 节奏（以八分音符为单位）
            int[][] comp = { new[] { 0, 3, 6 }, new[] { 2, 5 } };
            for (int b = 0; b < bars; b++)
            {
                var ch = prog[b];
                var pattern = comp[b % 2];
                foreach (int e in pattern)
                {
                    float t0 = b * bar + e * beat / 2;
                    float dur = beat * (e == 6 ? 1.2f : 0.9f);
                    foreach (int n in ch.Notes)
                        AddEPiano(buf, t0 + (float)rng.NextDouble() * 0.012f, dur, Midi(n), 0.055f);
                }

                // 贝斯：根音 - 五度，带一点切分
                AddBass(buf, b * bar, beat * 1.4f, Midi(ch.Bass), 0.22f);
                AddBass(buf, b * bar + beat * 1.5f, beat * 0.45f, Midi(ch.Bass + 7), 0.14f);
                AddBass(buf, b * bar + beat * 2f, beat * 1.4f, Midi(ch.Bass + 7), 0.18f);
                AddBass(buf, b * bar + beat * 3.5f, beat * 0.45f, Midi(ch.Bass + 12), 0.12f);

                // 打击乐：沙锤八分、边击 clave、轻底鼓
                for (int e = 0; e < 8; e++)
                    AddShaker(buf, b * bar + e * beat / 2, e % 2 == 0 ? 0.035f : 0.055f, rng);
                int[] clave = b % 2 == 0 ? new[] { 0, 3, 6 } : new[] { 2, 4 };
                foreach (int e in clave) AddRim(buf, b * bar + e * beat / 2, 0.07f);
                AddKick(buf, b * bar, 0.25f);
                AddKick(buf, b * bar + beat * 2, 0.18f);
            }

            // 颤音琴旋律：在和弦音/C 大调五声上平滑游走
            int[] scale = { 0, 2, 4, 7, 9 };
            int cur = 76;
            float[][] rhythms =
            {
                new[] { 0f, 1.5f, 2.5f }, new[] { 0.5f, 1f, 2f, 3f }, new[] { 0f, 2f }, new[] { 1f, 1.5f, 2.5f, 3.5f }, new[] { 0f, 0.75f, 1.5f }
            };
            for (int b = 0; b < bars; b++)
            {
                if (b % 8 == 7) continue; // 每 8 小节留一口气
                var rhythm = rhythms[rng.Next(rhythms.Length)];
                var ch = prog[b];
                for (int i = 0; i < rhythm.Length; i++)
                {
                    int target;
                    if (i == 0)
                    {
                        // 强拍落在和弦音上
                        int tone = ch.Notes[rng.Next(ch.Notes.Length)] % 12;
                        target = Nearest(cur, tone);
                    }
                    else
                    {
                        int step = rng.Next(-2, 3);
                        target = StepScale(cur, step, scale);
                    }
                    target = Math.Max(69, Math.Min(86, target));
                    cur = target;
                    float next = i + 1 < rhythm.Length ? rhythm[i + 1] : 4f;
                    float dur = (next - rhythm[i]) * beat * 0.95f;
                    AddVibes(buf, b * bar + rhythm[i] * beat, dur, Midi(cur), 0.09f);
                }
            }

            // "吊顶喇叭"：低通 + 去掉超低频 + 多抽头混响（循环回绕，保证无缝）
            OnePoleLowpass(buf, 4200f);
            OnePoleHighpass(buf, 90f);
            ReverbWrap(buf, new[] { 0.031f, 0.047f, 0.071f, 0.113f, 0.167f }, new[] { 0.32f, 0.27f, 0.22f, 0.17f, 0.12f });
            Normalize(buf, 0.6f);
            return buf;
        }

        static int Nearest(int from, int pitchClass)
        {
            int best = from, bestD = 99;
            for (int n = from - 6; n <= from + 6; n++)
                if (((n % 12) + 12) % 12 == pitchClass && Math.Abs(n - from) < bestD) { best = n; bestD = Math.Abs(n - from); }
            return best;
        }

        static int StepScale(int from, int steps, int[] scale)
        {
            int n = from;
            int dir = Math.Sign(steps);
            for (int s = 0; s < Math.Abs(steps); s++)
            {
                do n += dir; while (Array.IndexOf(scale, ((n % 12) + 12) % 12) < 0);
            }
            return n;
        }

        // ---------- 乐器 ----------
        const double TAU = Math.PI * 2;

        static void Add(float[] buf, float t0, float dur, Func<double, double> f)
        {
            int start = (int)(t0 * SR);
            int n = (int)(dur * SR);
            for (int i = 0; i < n; i++)
            {
                int idx = (start + i) % buf.Length;
                if (idx < 0) idx += buf.Length;
                buf[idx] += (float)f(i / (double)SR);
            }
        }

        static void AddEPiano(float[] buf, float t0, float dur, float freq, float amp)
        {
            float tail = dur + 0.6f;
            Add(buf, t0, tail, t =>
            {
                double env = Math.Exp(-t * 2.2) * Math.Min(1, t * 400);
                if (t > dur) env *= Math.Exp(-(t - dur) * 9);
                double mod = Math.Sin(TAU * freq * 1.0 * t) * 1.6 * Math.Exp(-t * 6);
                double tine = Math.Sin(TAU * freq * 14.0 * t) * 0.05 * Math.Exp(-t * 30);
                return (Math.Sin(TAU * freq * t + mod) + tine) * env * amp * (1 + 0.08 * Math.Sin(TAU * 5.0 * t));
            });
        }

        static void AddBass(float[] buf, float t0, float dur, float freq, float amp)
        {
            Add(buf, t0, dur + 0.05f, t =>
            {
                double env = Math.Min(1, t * 200) * Math.Exp(-t * 1.6);
                if (t > dur) env *= Math.Max(0, 1 - (t - dur) / 0.05);
                return (Math.Sin(TAU * freq * t) + 0.25 * Math.Sin(TAU * freq * 2 * t) + 0.08 * Math.Sin(TAU * freq * 3 * t)) * env * amp;
            });
        }

        static void AddVibes(float[] buf, float t0, float dur, float freq, float amp)
        {
            Add(buf, t0, dur + 0.8f, t =>
            {
                double env = Math.Min(1, t * 500) * Math.Exp(-t * 1.8);
                if (t > dur) env *= Math.Exp(-(t - dur) * 5);
                double trem = 0.85 + 0.15 * Math.Sin(TAU * 5.5 * t);
                return (Math.Sin(TAU * freq * t) + 0.18 * Math.Sin(TAU * freq * 4 * t) * Math.Exp(-t * 8)) * env * trem * amp;
            });
        }

        static void AddShaker(float[] buf, float t0, float amp, System.Random rng)
        {
            int start = (int)(t0 * SR);
            int n = (int)(0.06f * SR);
            float prev = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                float white = (float)(rng.NextDouble() * 2 - 1);
                float hp = white - prev; // 简单高通
                prev = white;
                double env = Math.Min(1, t * 300) * Math.Exp(-t * 60);
                buf[(start + i) % buf.Length] += (float)(hp * env * amp);
            }
        }

        static void AddRim(float[] buf, float t0, float amp)
        {
            Add(buf, t0, 0.05f, t => (Math.Sin(TAU * 1700 * t) * 0.6 + Math.Sin(TAU * 820 * t) * 0.4) * Math.Exp(-t * 120) * amp);
        }

        static void AddKick(float[] buf, float t0, float amp)
        {
            Add(buf, t0, 0.25f, t =>
            {
                double f = 50 + 70 * Math.Exp(-t * 30);
                return Math.Sin(TAU * f * t) * Math.Exp(-t * 14) * amp;
            });
        }

        // ---------- 效果 ----------
        static void OnePoleLowpass(float[] buf, float cutoff)
        {
            float a = (float)(1 - Math.Exp(-TAU * cutoff / SR));
            float y = 0;
            for (int pass = 0; pass < 2; pass++) // 两遍，让循环首尾状态衔接
                for (int i = 0; i < buf.Length; i++) { y += a * (buf[i] - y); if (pass == 1) buf[i] = y; }
        }

        static void OnePoleHighpass(float[] buf, float cutoff)
        {
            float a = (float)(1 - Math.Exp(-TAU * cutoff / SR));
            float low = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < buf.Length; i++) { low += a * (buf[i] - low); if (pass == 1) buf[i] -= low; }
        }

        static void ReverbWrap(float[] buf, float[] delays, float[] gains)
        {
            var dry = (float[])buf.Clone();
            for (int d = 0; d < delays.Length; d++)
            {
                int off = (int)(delays[d] * SR);
                for (int i = 0; i < buf.Length; i++) buf[(i + off) % buf.Length] += dry[i] * gains[d];
            }
            // 第二级，扩散尾音
            var wet = (float[])buf.Clone();
            int off2 = (int)(0.211f * SR), off3 = (int)(0.337f * SR);
            for (int i = 0; i < buf.Length; i++)
            {
                buf[(i + off2) % buf.Length] += wet[i] * 0.18f;
                buf[(i + off3) % buf.Length] += wet[i] * 0.1f;
            }
        }

        static void Normalize(float[] buf, float peak)
        {
            float m = 0;
            foreach (var v in buf) m = Math.Max(m, Math.Abs(v));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        // =====================================================================
        //  商场环境声
        // =====================================================================
        static float[] RenderAmbience(int seed)
        {
            var rng = new System.Random(seed);
            int len = SR * 16;
            var buf = new float[len];
            // 人声嘈杂：棕噪声 + 慢速起伏，再带通出"说话"的频段
            float brown = 0;
            for (int i = 0; i < len; i++)
            {
                brown = brown * 0.985f + (float)(rng.NextDouble() * 2 - 1) * 0.06f;
                double t = i / (double)SR;
                double swell = 0.6 + 0.25 * Math.Sin(TAU * t / 16.0 * 3) + 0.15 * Math.Sin(TAU * t / 16.0 * 7 + 1);
                buf[i] = (float)(brown * swell);
            }
            // 加入若干"含糊人声"：低通的元音式共振
            for (int k = 0; k < 40; k++)
            {
                float t0 = (float)(rng.NextDouble() * 16);
                float dur = 0.3f + (float)rng.NextDouble() * 0.8f;
                float f0 = 140 + (float)rng.NextDouble() * 120;
                float amp = 0.008f + (float)rng.NextDouble() * 0.01f;
                Add(buf, t0, dur, t =>
                {
                    double env = Math.Sin(Math.PI * Math.Min(1, t / dur));
                    double pitch = f0 * (1 + 0.1 * Math.Sin(TAU * 3 * t));
                    return (Math.Sin(TAU * pitch * t) + 0.5 * Math.Sin(TAU * pitch * 2 * t) + 0.3 * Math.Sin(TAU * pitch * 3 * t)) * env * amp;
                });
            }
            // 远处其他收银台的扫码声
            for (int k = 0; k < 9; k++)
            {
                float t0 = (float)(rng.NextDouble() * 16);
                float f = 1700 + (float)rng.NextDouble() * 400;
                Add(buf, t0, 0.08f, t => Math.Sin(TAU * f * t) * 0.012 * Math.Min(1, t * 300) * Math.Min(1, (0.08 - t) * 300));
            }
            OnePoleLowpass(buf, 1800f);
            Normalize(buf, 0.35f);
            return buf;
        }
    }
}

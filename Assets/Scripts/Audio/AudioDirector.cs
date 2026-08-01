using System.Collections.Generic;
using UnityEngine;

namespace MonsterMart.Core
{
    /// <summary>
    /// 音频 — 设计文档 §15。
    /// 所有音效和背景音都在运行时用波形合成生成，工程里没有音频资源。
    /// 替换成正式音频时，把 Clip(...) 换成资源加载即可。
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        const int SampleRate = 44100;

        AudioSource _sfx;
        AudioSource _music;

        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public float SfxVolume { get; private set; } = 0.55f;
        public float MusicVolume { get; private set; } = 0.22f;

        public void Build()
        {
            var sfxGo = new GameObject("SFX");
            sfxGo.transform.SetParent(transform, false);
            _sfx = sfxGo.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.volume = SfxVolume;

            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform, false);
            _music = musicGo.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = MusicVolume;
        }

        public void SetVolumes(float sfx, float music)
        {
            SfxVolume = Mathf.Clamp01(sfx);
            MusicVolume = Mathf.Clamp01(music);
            if (_sfx != null) _sfx.volume = SfxVolume;
            if (_music != null) _music.volume = MusicVolume;
        }

        // ------------------------------------------------------------------
        // 音效清单 — 设计文档 §15「必要音效」
        // ------------------------------------------------------------------
        public void PlayDoorBell() => PlayArpeggio("doorbell", new[] { 880f, 1174f, 1568f }, 0.09f, 0.30f);
        public void PlayPickup() => PlayBlip("pickup", 660f, 0.07f, 0.28f);
        public void PlayRestock() => PlayBlip("restock", 420f, 0.11f, 0.30f);
        public void PlayScan() => PlayBlip("scan", 1480f, 0.06f, 0.34f);
        public void PlayCash() => PlayArpeggio("cash", new[] { 784f, 1046f, 1318f, 1568f }, 0.06f, 0.34f);
        public void PlayHappy() => PlayArpeggio("happy", new[] { 523f, 659f, 784f }, 0.10f, 0.32f);
        public void PlayAngry() => PlayFall("angry", 400f, 150f, 0.32f, 0.32f);
        public void PlayError() => PlayFall("error", 300f, 120f, 0.20f, 0.30f);
        public void PlayCrash() => PlayNoise("crash", 0.42f, 0.30f);
        public void PlayClean() => PlayNoise("clean", 0.30f, 0.16f);
        public void PlayUiClick() => PlayBlip("click", 980f, 0.045f, 0.22f);
        public void PlaySpirit() => PlayArpeggio("spirit", new[] { 622f, 831f, 1108f, 1245f }, 0.09f, 0.26f);
        public void PlayBlackout() => PlayFall("blackout", 520f, 90f, 0.7f, 0.30f);

        // ------------------------------------------------------------------
        // 背景音 — 设计文档 §15「背景音乐」
        // ------------------------------------------------------------------
        public void PlayPreparationTheme()
            => PlayMusic("theme_prep", new[] { 130.8f, 196f, 246.9f }, 6.4f, 0.16f);

        public void PlayBusinessTheme()
            => PlayMusic("theme_open", new[] { 146.8f, 220f, 293.7f, 349.2f }, 4.8f, 0.19f);

        public void PlaySettlementTheme()
            => PlayMusic("theme_settle", new[] { 174.6f, 261.6f, 329.6f }, 7.2f, 0.15f);

        void PlayMusic(string key, float[] chord, float loopSeconds, float amplitude)
        {
            if (_music == null) return;

            var clip = GetOrCreate(key, () => BuildPad(key, chord, loopSeconds, amplitude));
            if (clip == null) return;

            if (_music.clip == clip && _music.isPlaying) return;

            _music.clip = clip;
            _music.Play();
        }

        // ------------------------------------------------------------------
        // 合成
        // ------------------------------------------------------------------
        void PlayBlip(string key, float frequency, float duration, float amplitude)
        {
            var clip = GetOrCreate(key, () => BuildTone(key, frequency, duration, amplitude, 0f));
            PlaySfx(clip);
        }

        void PlayFall(string key, float from, float to, float duration, float amplitude)
        {
            var clip = GetOrCreate(key, () => BuildSweep(key, from, to, duration, amplitude));
            PlaySfx(clip);
        }

        void PlayArpeggio(string key, float[] notes, float noteDuration, float amplitude)
        {
            var clip = GetOrCreate(key, () => BuildArpeggio(key, notes, noteDuration, amplitude));
            PlaySfx(clip);
        }

        void PlayNoise(string key, float duration, float amplitude)
        {
            var clip = GetOrCreate(key, () => BuildNoise(key, duration, amplitude));
            PlaySfx(clip);
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip == null || _sfx == null) return;
            _sfx.PlayOneShot(clip, SfxVolume);
        }

        AudioClip GetOrCreate(string key, System.Func<AudioClip> factory)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var clip = factory();
            _cache[key] = clip;
            return clip;
        }

        static AudioClip BuildTone(string name, float frequency, float duration, float amplitude, float detune)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Envelope(i / (float)samples);
                float value = Mathf.Sin(2f * Mathf.PI * frequency * t);

                if (detune > 0f) value += 0.4f * Mathf.Sin(2f * Mathf.PI * (frequency + detune) * t);

                data[i] = value * amplitude * envelope;
            }

            return Finish(name, data);
        }

        static AudioClip BuildSweep(string name, float from, float to, float duration, float amplitude)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)samples;
                float frequency = Mathf.Lerp(from, to, progress);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                data[i] = Mathf.Sin(phase) * amplitude * Envelope(progress);
            }

            return Finish(name, data);
        }

        static AudioClip BuildArpeggio(string name, float[] notes, float noteDuration, float amplitude)
        {
            int perNote = Mathf.Max(1, Mathf.RoundToInt(SampleRate * noteDuration));
            var data = new float[perNote * notes.Length];

            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)SampleRate;
                    float progress = i / (float)perNote;
                    data[n * perNote + i] =
                        Mathf.Sin(2f * Mathf.PI * notes[n] * t) * amplitude * Envelope(progress);
                }
            }

            return Finish(name, data);
        }

        static AudioClip BuildNoise(string name, float duration, float amplitude)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());
            float low = 0f;

            for (int i = 0; i < samples; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = Mathf.Lerp(low, white, 0.22f);      // 简单低通，听起来更闷
                data[i] = low * amplitude * Envelope(i / (float)samples);
            }

            return Finish(name, data);
        }

        /// <summary>无缝循环的和弦垫音。</summary>
        static AudioClip BuildPad(string name, float[] chord, float loopSeconds, float amplitude)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * loopSeconds));
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float value = 0f;

                for (int n = 0; n < chord.Length; n++)
                {
                    // 每个音的频率取整到循环长度的整数倍，保证首尾相接不爆音
                    float cycles = Mathf.Round(chord[n] * loopSeconds);
                    float frequency = cycles / loopSeconds;
                    value += Mathf.Sin(2f * Mathf.PI * frequency * t) / chord.Length;
                }

                // 缓慢的音量起伏
                float breathe = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / loopSeconds);
                data[i] = value * amplitude * breathe;
            }

            return Finish(name, data);
        }

        static float Envelope(float progress)
        {
            const float attack = 0.04f;
            if (progress < attack) return progress / attack;
            return Mathf.Pow(1f - (progress - attack) / (1f - attack), 1.6f);
        }

        static AudioClip Finish(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            if (clip == null) return null;

            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}

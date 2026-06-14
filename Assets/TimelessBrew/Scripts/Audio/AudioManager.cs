using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew.Audio
{
    /// <summary>
    /// Единый звук игры. Сам себя создаёт до загрузки сцены (без ручной расстановки) и живёт между
    /// сценами. Умеет:
    ///   • разовые звуки — <see cref="Play"/> (PlayOneShot, 2D);
    ///   • длинные/зацикленные — <see cref="Loop"/>: зовётся КАЖДЫЙ кадр, пока действие идёт; как
    ///     только перестали звать — голос сам затухает и останавливается (привязка к состоянию, а не
    ///     к старту/стопу вручную). Поэтому хуки в механиках — это одна строка в Update/Hold/готовке;
    ///   • музыку — <see cref="PlayMusic"/> (один зацикленный трек с кроссфейдом по громкости).
    /// Громкости (мастер/эффекты/музыка) хранятся в PlayerPrefs — их будет крутить шестерёнка настроек.
    /// Клипов нет в проекте? <see cref="SoundLibrary"/> вернёт null, звук молча не сыграет.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private static bool _quitting;   // не воскрешать синглтон во время выхода (иначе утечка + warning)

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying && !_quitting)
                {
                    var go = new GameObject("— AudioManager —");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _quitting = false;
            Application.quitting += () => _quitting = true;
            _ = Instance;
        }

        // --- Громкости (0..1), сохраняются между запусками ---
        public float Master { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 0.9f;
        public float MusicVolume { get; private set; } = 0.55f;

        private float EffSfx => Master * SfxVolume;
        private float EffMusic => Master * MusicVolume;

        private AudioSource _sfx;     // 2D разовые
        private AudioSource _capped;  // разовый с обрезкой по времени (длинные клипы вроде прихода гостя)
        private float _cappedStopAt = -1f;
        private AudioSource _music;   // зацикленный трек
        private Music _musicKey = Music.None;
        private AudioSource _ambient; // фоновый эмбиент (природа) — отдельным слоем под музыкой
        private Music _ambientKey = Music.None;
        private float _ambientBaseVol = 0.55f;
        private bool _musicDucked;    // приглушить музыку (напр. на паузе)
        private float Duck => _musicDucked ? 0.3f : 1f;

        /// <summary>Приглушить/вернуть громкость музыки и эмбиента (для паузы).</summary>
        public void SetMusicDucked(bool ducked) => _musicDucked = ducked;

        // Зацикленные голоса по строковому id (его «пингуют» каждый кадр).
        private const float LoopGrace = 0.18f;   // без пинга дольше этого — затухаем
        private const float LoopReap = 5f;       // после стольких секунд простоя голос удаляется совсем
        private class LoopVoice { public AudioSource src; public float lastPing; public float target; }
        private readonly Dictionary<string, LoopVoice> _loops = new();
        private readonly List<string> _deadLoops = new();   // буфер на удаление (не трогаем словарь в foreach)

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            Master = PlayerPrefs.GetFloat("tb_vol_master", 1f);
            SfxVolume = PlayerPrefs.GetFloat("tb_vol_sfx", 0.9f);
            MusicVolume = PlayerPrefs.GetFloat("tb_vol_music", 0.55f);

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false; _sfx.spatialBlend = 0f;

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false; _music.loop = true; _music.spatialBlend = 0f;

            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.playOnAwake = false; _ambient.loop = true; _ambient.spatialBlend = 0f;
        }

        // ===================== РАЗОВЫЕ =====================

        /// <summary>Сыграть короткий звук один раз. volume — относительный (0..1).</summary>
        public void Play(Sfx key, float volume = 1f)
        {
            if (key == Sfx.None) return;
            var clip = SoundLibrary.Get(key);
            if (clip != null) _sfx.PlayOneShot(clip, Mathf.Clamp01(volume) * EffSfx);
        }

        /// <summary>Сыграть клип, оборвав его через maxSeconds (для длинных дорожек, напр. приход гостя).</summary>
        public void PlayCapped(Sfx key, float maxSeconds, float volume = 1f)
        {
            if (key == Sfx.None) return;
            var clip = SoundLibrary.Get(key);
            if (clip == null) return;
            if (_capped == null)
            {
                _capped = gameObject.AddComponent<AudioSource>();
                _capped.playOnAwake = false; _capped.spatialBlend = 0f;
            }
            _capped.clip = clip;
            _capped.volume = Mathf.Clamp01(volume) * EffSfx;
            _capped.Play();
            _cappedStopAt = Time.unscaledTime + maxSeconds;
        }

        // ===================== ЗАЦИКЛЕННЫЕ =====================

        /// <summary>
        /// Поддержать зацикленный звук «живым» в этом кадре. Зови каждый кадр, пока действие идёт
        /// (наливание, помол, варка…). Перестал звать — голос сам затухнет. id — уникальный канал
        /// (напр. "pour", "grind", "brew{instanceId}").
        /// </summary>
        public void Loop(string id, Sfx key, float volume = 1f)
        {
            if (key == Sfx.None || string.IsNullOrEmpty(id)) return;
            var clip = SoundLibrary.Get(key);
            if (clip == null) return;

            if (!_loops.TryGetValue(id, out var v))
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false; src.loop = true; src.spatialBlend = 0f; src.volume = 0f;
                v = new LoopVoice { src = src };
                _loops[id] = v;
            }
            if (v.src.clip != clip) { v.src.clip = clip; }
            if (!v.src.isPlaying) v.src.Play();
            v.target = Mathf.Clamp01(volume);
            v.lastPing = Time.unscaledTime;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            _deadLoops.Clear();
            foreach (var kv in _loops)
            {
                var v = kv.Value;
                bool alive = now - v.lastPing < LoopGrace;
                float goal = (alive ? v.target : 0f) * EffSfx;
                v.src.volume = Mathf.MoveTowards(v.src.volume, goal, 4f * dt);
                if (!alive && v.src.volume <= 0.001f)
                {
                    if (v.src.isPlaying) v.src.Stop();
                    // Долго молчит — не копим источники (важно при динамическом спавне сосудов).
                    if (now - v.lastPing > LoopReap) { Destroy(v.src); _deadLoops.Add(kv.Key); }
                }
            }
            for (int i = 0; i < _deadLoops.Count; i++) _loops.Remove(_deadLoops[i]);

            if (_music.isPlaying) _music.volume = Mathf.MoveTowards(_music.volume, EffMusic * Duck, 0.9f * dt);
            if (_ambient.isPlaying) _ambient.volume = Mathf.MoveTowards(_ambient.volume, EffMusic * _ambientBaseVol * Duck, 0.9f * dt);

            if (_cappedStopAt > 0f && now >= _cappedStopAt)
            {
                if (_capped != null && _capped.isPlaying) _capped.Stop();
                _cappedStopAt = -1f;
            }
        }

        // ===================== МУЗЫКА =====================

        /// <summary>Поставить зацикленный трек (повторный вызов того же — ничего не делает).</summary>
        public void PlayMusic(Music key)
        {
            if (key == _musicKey && _music.isPlaying) return;
            var clip = SoundLibrary.Get(key);
            _musicKey = key;
            if (clip == null) { _music.Stop(); return; }
            _music.clip = clip;
            _music.volume = 0f;     // плавно поднимется в Update до EffMusic
            _music.Play();
        }

        public void StopMusic() { _musicKey = Music.None; _music.Stop(); }

        /// <summary>Фоновый эмбиент (природа) отдельным зацикленным слоем под музыкой.</summary>
        public void PlayAmbient(Music key, float volume = 0.55f)
        {
            if (key == _ambientKey && _ambient.isPlaying) return;
            var clip = SoundLibrary.Get(key);
            _ambientKey = key;
            _ambientBaseVol = Mathf.Clamp01(volume);
            if (clip == null) { _ambient.Stop(); return; }
            _ambient.clip = clip; _ambient.volume = 0f; _ambient.Play();   // плавно поднимется в Update
        }

        // ===================== ГРОМКОСТИ (для настроек) =====================

        public void SetMaster(float v) { Master = Mathf.Clamp01(v); PlayerPrefs.SetFloat("tb_vol_master", Master); ApplyMusicVolume(); }
        public void SetSfx(float v) { SfxVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("tb_vol_sfx", SfxVolume); }
        public void SetMusic(float v) { MusicVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("tb_vol_music", MusicVolume); ApplyMusicVolume(); }

        private void ApplyMusicVolume()
        {
            if (_music != null && _music.isPlaying) _music.volume = EffMusic * Duck;
            if (_ambient != null && _ambient.isPlaying) _ambient.volume = EffMusic * _ambientBaseVol * Duck;
        }
    }
}

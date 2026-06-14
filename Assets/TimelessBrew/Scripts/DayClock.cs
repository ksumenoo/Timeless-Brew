using System.Collections.Generic;
using UnityEngine;
using TimelessBrew.Audio;
using TimelessBrew.UI;

namespace TimelessBrew
{
    /// <summary>Фаза дня (§4.4).</summary>
    public enum DayPhase { Morning, Day, Evening }

    /// <summary>
    /// Часы рабочего дня (§4.4): время идёт само (утро→день→вечер), игрок может УСКОРИТЬ или
    /// ОТМОТАТЬ его (чтобы быстрее приходили гости). По расписанию гости приходят сами; меняется
    /// освещение (солнце) и музыка (вечером — вечерний эмбиент). UI — <see cref="UI.DayClockUI"/>.
    /// </summary>
    public class DayClock : MonoBehaviour
    {
        public static DayClock Instance { get; private set; }

        [SerializeField] private float dayLengthSeconds = 180f;   // полный день при обычной скорости
        [SerializeField] private float fastMultiplier = 12f;
        [SerializeField] private float rewindMultiplier = -8f;

        private float _t;            // 0..dayLengthSeconds
        private float _speed = 1f;
        private bool _guardPushed;   // занят ли слот ModalGuard под паузу (чтобы блокировать руку)
        private bool _dayEnded;      // итоги смены уже показаны
        private bool _closing;       // время вышло — кофейня закрывается, ждём ухода последнего гостя
        private GuestService _service;
        private GuestLibrary _library;
        private Light _sun;
        private Hand _hand;
        private DayPhase _lastPhase = DayPhase.Morning;

        private struct Arrival { public float frac; public GuestSO guest; public bool morning; public bool fired; }
        private readonly List<Arrival> _schedule = new();

        public float DayFraction => Mathf.Clamp01(_t / Mathf.Max(1f, dayLengthSeconds));
        public DayPhase Phase => DayFraction < 1f / 3f ? DayPhase.Morning : DayFraction < 2f / 3f ? DayPhase.Day : DayPhase.Evening;
        public float Speed => _speed;
        public bool IsPaused { get; private set; }

        /// <summary>На время обучения: день не идёт, гости по расписанию не приходят, итоги не подводятся.</summary>
        public bool Suspended { get; set; }

        public void SetNormal() { IsPaused = false; _speed = 1f; ApplyTimeScale(); }
        public void SetFast() { IsPaused = false; _speed = fastMultiplier; ApplyTimeScale(); }
        public void SetRewind() { IsPaused = false; _speed = rewindMultiplier; ApplyTimeScale(); }

        /// <summary>
        /// Кнопка ▶: из ускорения/отмотки — вернуть обычный ход; из обычного — ПАУЗА (стоп игры);
        /// из паузы — снять с паузы. (У игры теперь есть пауза — через эту кнопку.)
        /// </summary>
        public void TogglePlayPause()
        {
            if (IsPaused) { IsPaused = false; _speed = 1f; }
            else if (!Mathf.Approximately(_speed, 1f)) _speed = 1f;   // ускорение/отмотка → норма
            else IsPaused = true;                                    // норма → пауза
            ApplyTimeScale();
        }

        /// <summary>
        /// Синхронизирует эффекты паузы: стоп времени, БЛОКИРОВКА руки через ModalGuard (нельзя брать
        /// предметы под затемнением), приглушение музыки. Слой часов (DayClockUI) не зависит от ModalGuard,
        /// поэтому снять паузу кнопкой ▶ можно.
        /// </summary>
        private void ApplyTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
            if (IsPaused && !_guardPushed) { ModalGuard.Push(); _guardPushed = true; }
            else if (!IsPaused && _guardPushed) { ModalGuard.Pop(); _guardPushed = false; }
            // Рука включена ровно когда нет открытых модалок/паузы (та же логика, что у других окон).
            if (_hand != null) _hand.enabled = !ModalGuard.AnyOpen;
            var am = AudioManager.Instance;
            if (am != null) am.SetMusicDucked(IsPaused);
        }

        private void Awake() => Instance = this;
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;                       // не оставить мир замороженным при выходе
            if (_guardPushed) { ModalGuard.Pop(); _guardPushed = false; }
            var am = AudioManager.Instance;
            if (am != null) am.SetMusicDucked(false);   // музыка не остаётся приглушённой в другой сцене
        }

        private void Start()
        {
            _service = FindFirstObjectByType<GuestService>();
            _library = FindFirstObjectByType<GuestLibrary>();
            _sun = FindSun();
            _hand = FindFirstObjectByType<Hand>();
            BuildSchedule();
            ApplyLighting();
            _lastPhase = Phase;
        }

        private void Update()
        {
            if (Suspended) return;   // обучение заморозило день: время стоит, расписание молчит, итоги не подводятся
            _t = Mathf.Clamp(_t + _speed * Time.deltaTime, 0f, dayLengthSeconds);

            // Расписание: гость приходит, когда время ПРОШЛО его момент (вперёд). Отмотка не возвращает.
            float f = DayFraction;
            for (int i = 0; i < _schedule.Count; i++)
            {
                var a = _schedule[i];
                // Помечаем «пришёл» ТОЛЬКО если реально добавился: при заполненных местах попытка
                // повторится в следующем кадре, когда освободится слот — гость не теряется.
                if (!a.fired && f >= a.frac && f < 1f && _service != null && _service.Call(a.guest, a.morning))
                {
                    a.fired = true; _schedule[i] = a;
                }
            }

            ApplyLighting();
            if (Phase != _lastPhase) { _lastPhase = Phase; OnPhaseChanged(_lastPhase); }

            // Время вышло — кофейня закрывается: новых гостей не зовём (см. условие расписания выше),
            // а итоги подводим только когда ушёл последний гость (его обслужили).
            if (!_closing && DayFraction >= 1f)
            {
                _closing = true;
                int left = _service != null ? _service.GuestCount : 0;
                ToastUI.Toast(left > 0 ? "Рабочий день окончен — обслужите оставшихся гостей"
                                       : "Рабочий день окончен", ToastUI.Neutral, 4f);
            }
            // Итоги — когда время вышло И ушёл последний гость (а не раньше; при отмотке времени ниже конца дня ждём снова).
            if (!_dayEnded && DayFraction >= 1f && (_service == null || _service.GuestCount == 0))
            {
                _dayEnded = true;
                if (IsPaused) { IsPaused = false; ApplyTimeScale(); }   // снять свою паузу: единственным владельцем стопа времени/ModalGuard будет окно итогов
                EndOfDayUI.Show(_service != null ? _service.Served : 0, _service != null ? _service.TotalStars : 0);
            }
        }

        private void BuildSchedule()
        {
            if (_library == null || _library.guests == null) return;
            var g = _library.guests;
            float[] fracs = { 0.06f, 0.15f, 0.24f, 0.40f, 0.52f, 0.72f, 0.85f };
            for (int i = 0; i < g.Length && i < fracs.Length; i++)
            {
                if (g[i] == null) continue;
                _schedule.Add(new Arrival { frac = fracs[i], guest = g[i], morning = fracs[i] < 0.66f });
            }
        }

        private void OnPhaseChanged(DayPhase p)
        {
            var ka = FindFirstObjectByType<KitchenAudio>();
            if (ka != null) ka.SetTrack(p == DayPhase.Evening ? Music.EveningAmbience : Music.Gameplay);
        }

        private Light FindSun()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        private void ApplyLighting()
        {
            if (_sun == null) return;
            float f = DayFraction;
            Color cMorning = new(1f, 0.93f, 0.82f), cNoon = new(1f, 0.98f, 0.95f), cEve = new(1f, 0.62f, 0.42f);
            const float iMorning = 0.95f, iNoon = 1.25f, iEve = 0.6f;

            Color c; float inten;
            if (f < 0.5f) { float t = f / 0.5f; c = Color.Lerp(cMorning, cNoon, t); inten = Mathf.Lerp(iMorning, iNoon, t); }
            else { float t = (f - 0.5f) / 0.5f; c = Color.Lerp(cNoon, cEve, t); inten = Mathf.Lerp(iNoon, iEve, t); }
            _sun.color = c;
            _sun.intensity = inten;
        }
    }
}

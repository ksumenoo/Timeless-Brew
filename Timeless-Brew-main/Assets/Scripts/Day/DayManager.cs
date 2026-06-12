using System;
using System.Collections.Generic;
using UnityEngine;
using TimelessBrew.Data;

namespace TimelessBrew.Day
{
    // Менеджер одного игрового дня (§4.4). Хранит текущую фазу (утро -> день -> вечер -> закрытие)
    // и очередь гостей. Спавнер берёт следующего гостя через TryGetNextVisit; когда гости фазы
    // кончились, менеджер сам переходит к следующей фазе, пока день не закроется.
    public class DayManager : MonoBehaviour
    {
        public static DayManager Instance;

        [Header("Состав дня")]
        [SerializeField] private List<GuestSO> guests = new List<GuestSO>();
        [SerializeField] private bool shuffleWithinPhase = false;   // перемешать порядок гостей в фазе
        [SerializeField] private bool autoStartOnPlay = true;       // стартовать день в Start()

        public DayPhase CurrentPhase { get; private set; } = DayPhase.Morning;
        public bool DayRunning { get; private set; }

        public int RemainingInPhase
        {
            get
            {
                if (_phaseQueue == null) return 0;
                return _phaseQueue.Count;
            }
        }

        private Queue<GuestVisit> _phaseQueue;

        // События для других систем (спавнер слушает их).
        public event Action OnDayStarted;
        public event Action<DayPhase, DayPhase> OnPhaseChanged;   // (старая фаза, новая фаза)
        public event Action OnDayEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (autoStartOnPlay) StartDay();
        }

        // Запустить новый день: построить очередь утра и войти в утреннюю фазу.
        [ContextMenu("Start Day")]
        public void StartDay()
        {
            if (DayRunning)
            {
                Debug.LogWarning("[DayManager] День уже идёт.", this);
                return;
            }

            DayRunning = true;
            CurrentPhase = DayPhase.Morning;
            BuildPhaseQueue(CurrentPhase);

            if (OnDayStarted != null) OnDayStarted();
            if (OnPhaseChanged != null) OnPhaseChanged(DayPhase.Morning, CurrentPhase);

            Debug.Log("[DayManager] День начат. Гостей утром: " + RemainingInPhase, this);
        }

        // Выдать следующий визит. Если гости фазы кончились — переходим к следующей фазе.
        // Возвращает false, когда день закрыт.
        public bool TryGetNextVisit(out GuestVisit visit)
        {
            visit = null;
            if (!DayRunning) return false;

            // Пропускаем пустые фазы, пока не найдём гостя или не закроем день.
            while (true)
            {
                if (_phaseQueue != null && _phaseQueue.Count > 0)
                {
                    visit = _phaseQueue.Dequeue();
                    return true;
                }

                if (!AdvancePhase()) return false;   // дошли до Closed
            }
        }

        // Перейти к следующей фазе. Возвращает false, если день закрылся.
        public bool AdvancePhase()
        {
            if (CurrentPhase == DayPhase.Closed)
            {
                EndDay();
                return false;
            }

            DayPhase prev = CurrentPhase;
            DayPhase next = NextPhase(CurrentPhase);
            CurrentPhase = next;

            if (next == DayPhase.Closed)
            {
                if (OnPhaseChanged != null) OnPhaseChanged(prev, next);
                EndDay();
                return false;
            }

            BuildPhaseQueue(next);
            if (OnPhaseChanged != null) OnPhaseChanged(prev, next);
            Debug.Log("[DayManager] Фаза: " + prev + " -> " + next + ", гостей: " + RemainingInPhase, this);
            return true;
        }

        // Какая фаза идёт после текущей.
        private DayPhase NextPhase(DayPhase phase)
        {
            if (phase == DayPhase.Morning) return DayPhase.Day;
            if (phase == DayPhase.Day) return DayPhase.Evening;
            if (phase == DayPhase.Evening) return DayPhase.Closed;
            return DayPhase.Closed;
        }

        private void EndDay()
        {
            if (!DayRunning) return;
            DayRunning = false;
            CurrentPhase = DayPhase.Closed;
            _phaseQueue = null;
            if (OnDayEnded != null) OnDayEnded();
            Debug.Log("[DayManager] День закрыт.", this);
        }

        // Собрать очередь гостей, которые приходят в эту фазу.
        private void BuildPhaseQueue(DayPhase phase)
        {
            List<GuestVisit> visits = new List<GuestVisit>();
            foreach (GuestSO g in guests)
            {
                if (g != null && g.VisitsIn(phase))
                    visits.Add(new GuestVisit(g, phase));
            }

            if (shuffleWithinPhase) ShuffleVisits(visits);

            _phaseQueue = new Queue<GuestVisit>(visits);
        }

        // Перемешать список (простой проход с конца).
        private void ShuffleVisits(List<GuestVisit> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                GuestVisit tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        [ContextMenu("DEBUG: Advance Phase")]
        private void DebugAdvancePhase() => AdvancePhase();
    }
}

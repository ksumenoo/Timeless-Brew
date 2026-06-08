using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Печка с мехами (§6.1, v4.3). Источник тепла для обжарки и варки.
    ///
    /// Раздув стал ДИСКРЕТНЫМ (§4.2 v4.3): каждое качание мехов добавляет жара по делениям —
    /// два качка выводят на первое деление, три — на второе, четыре — в красную зону. Без поддува
    /// огонь слабеет до тления. Перебор раскаляет печь до предела: датчик полностью красный,
    /// и тогда готовить нельзя около десяти секунд (CanCook=false), пока печь не остынет.
    ///
    /// Качок засчитывается по «нажатию» на ручку мехов — фронту удержания ЛКМ на bellowsHandle
    /// (DirectHoldTarget). Тап по мехам = один качок.
    ///
    /// Публичный контракт (Temperature 0..1, ArrowValue -1..+1, IsOnBurner) сохранён для RoastingPan.
    /// </summary>
    public class Stove : InteractableItem
    {
        [Header("Печка — ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [Tooltip("Дочерний интерактив 'ручка мехов'. Тап ЛКМ по нему = один качок.")]
        [SerializeField] private InteractableItem bellowsHandle;

        [Header("Раздув (дискретно)")]
        [Tooltip("Сколько жара (0..1) добавляет один качок мехов.")]
        [SerializeField, Range(0.05f, 0.5f)] private float heatPerPump = 0.24f;
        [Tooltip("Скорость остывания без поддува (ед./сек).")]
        [SerializeField] private float decayRate = 0.18f;
        [Tooltip("Минимальная температура — печка тлеет, не гаснет полностью.")]
        [SerializeField, Range(0f, 0.3f)] private float smolderFloor = 0.08f;

        [Header("Перегрев")]
        [Tooltip("Порог температуры, выше которого печь раскаляется до предела (блокировка готовки).")]
        [SerializeField, Range(0.8f, 1f)] private float overheatAt = 0.95f;
        [Tooltip("Сколько секунд нельзя готовить после перегрева.")]
        [SerializeField] private float overheatLockSeconds = 10f;

        [Header("Оптимальный режим (для стрелки и обжарки)")]
        [SerializeField, Range(0f, 1f)] private float optimalMin = 0.55f;
        [SerializeField, Range(0f, 1f)] private float optimalMax = 0.75f;

        [Header("Состояния (по яркости свечения)")]
        [SerializeField] private ItemStateSO stateCold;        // холодная
        [SerializeField] private ItemStateSO stateSmolder;     // тлеет
        [SerializeField] private ItemStateSO stateEven;        // горит ровно
        [SerializeField] private ItemStateSO stateStrong;      // горит сильно
        [SerializeField] private ItemStateSO stateOverheated;  // раскалена до предела (опц.; иначе stateStrong)

        [Header("Зона конфорки")]
        [SerializeField] private Transform burnerPoint;
        [SerializeField] private float burnerRadius = 0.12f;

        /// <summary>Текущая температура 0..1.</summary>
        public float Temperature { get; private set; }

        /// <summary>True, если печь перегрета и сейчас на ней нельзя готовить.</summary>
        public bool IsOverheated { get; private set; }

        /// <summary>Можно ли сейчас готовить на печке (false во время перегрева-блокировки).</summary>
        public bool CanCook => !IsOverheated;

        /// <summary>True, если температура в оптимальной зоне.</summary>
        public bool IsOptimal => !IsOverheated && Temperature >= optimalMin && Temperature <= optimalMax;

        /// <summary>Деление жара: 0 — тлеет/мало, 1 — первое деление, 2 — второе, 3 — красная зона.</summary>
        public int HeatDivision
        {
            get
            {
                if (Temperature < optimalMin) return 0;
                if (Temperature <= optimalMax) return 1;
                if (Temperature < overheatAt) return 2;
                return 3;
            }
        }

        /// <summary>Значение для стрелки-индикатора: -1 (мало огня) .. 0 (оптимум) .. +1 (перегрев).</summary>
        public float ArrowValue
        {
            get
            {
                float mid = (optimalMin + optimalMax) * 0.5f;
                if (Temperature < mid)
                    return -Mathf.InverseLerp(optimalMin, 0f, Mathf.Min(Temperature, optimalMin));
                else
                    return Mathf.InverseLerp(optimalMax, 1f, Mathf.Max(Temperature, optimalMax));
            }
        }

        private float _overheatTimer;
        private bool _handleHeldPrev;

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            Temperature = smolderFloor;
        }

        private void Update()
        {
            if (IsOverheated)
            {
                // Печь раскалена до предела: готовить нельзя, ждём окончания блокировки.
                Temperature = 1f;
                _overheatTimer -= Time.deltaTime;
                if (_overheatTimer <= 0f)
                {
                    IsOverheated = false;
                    Temperature = smolderFloor; // сбросило жар, печь остыла до тления
                }
                UpdateGlowState();
                _handleHeldPrev = HandleHeldNow(); // не копим качок, сделанный во время блокировки
                return;
            }

            // Дискретный качок: фронт удержания ЛКМ на ручке мехов.
            bool handleHeld = HandleHeldNow();
            if (handleHeld && !_handleHeldPrev)
                Pump();
            _handleHeldPrev = handleHeld;

            // Остывание без поддува.
            Temperature = Mathf.Max(smolderFloor, Temperature - decayRate * Time.deltaTime);

            UpdateGlowState();
        }

        private bool HandleHeldNow()
            => cursorHandler != null && bellowsHandle != null
               && cursorHandler.DirectHoldTarget == bellowsHandle;

        /// <summary>Один качок мехов — добавить жар; при переборе — перегрев.</summary>
        private void Pump()
        {
            Temperature = Mathf.Min(1f, Temperature + heatPerPump);
            if (Temperature >= overheatAt)
                TriggerOverheat();
        }

        private void TriggerOverheat()
        {
            IsOverheated = true;
            _overheatTimer = overheatLockSeconds;
            Temperature = 1f;
            Debug.Log($"[Stove] Перегрев! Готовить нельзя {overheatLockSeconds:0} сек.", this);
        }

        private void UpdateGlowState()
        {
            ItemStateSO target;
            if (IsOverheated)                          target = stateOverheated != null ? stateOverheated : stateStrong;
            else if (Temperature <= smolderFloor + 0.001f) target = stateSmolder != null ? stateSmolder : stateCold;
            else if (Temperature < optimalMin)         target = stateSmolder;
            else if (Temperature <= optimalMax)        target = stateEven;
            else                                        target = stateStrong;

            if (target != null && target != CurrentState)
                SetState(target);
        }

        /// <summary>Стоит ли указанный предмет на конфорке (по расстоянию до burnerPoint).</summary>
        public bool IsOnBurner(Transform t)
        {
            if (burnerPoint == null || t == null) return false;
            Vector3 a = t.position; a.y = 0f;
            Vector3 b = burnerPoint.position; b.y = 0f;
            return Vector3.Distance(a, b) <= burnerRadius;
        }

        private void OnDrawGizmosSelected()
        {
            if (burnerPoint == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(burnerPoint.position, burnerRadius);
        }
    }
}

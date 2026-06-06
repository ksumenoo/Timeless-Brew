using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Печка с мехами (§6.1). Источник тепла для обжарки и варки.
    /// Температура — нормализованная величина 0..1:
    ///   - качание мехов (удержание ЛКМ пустой рукой на ручке мехов) поднимает температуру;
    ///   - без поддува температура плавно падает до тления.
    /// Стрелка-индикатор над печкой (§4.2) читает ArrowValue: -1 (мало огня) .. +1 (слишком горячо),
    /// центр (~0) — оптимальный режим.
    ///
    /// Сама печка непереносима (pickable=false в её состояниях). Ручка мехов — дочерний
    /// InteractableItem с itemType="BellowsHandle"; на неё игрок наводит курсор и удерживает ЛКМ.
    /// </summary>
    public class Stove : InteractableItem
    {
        [Header("Печка — ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [Tooltip("Дочерний интерактив 'ручка мехов'. Удержание ЛКМ на нём качает мехи.")]
        [SerializeField] private InteractableItem bellowsHandle;

        [Header("Температура")]
        [Tooltip("Скорость роста температуры при качании мехов (ед./сек).")]
        [SerializeField] private float pumpRate = 0.6f;
        [Tooltip("Скорость падения температуры без поддува (ед./сек).")]
        [SerializeField] private float decayRate = 0.2f;
        [Tooltip("Минимальная температура — печка тлеет, не гаснет полностью.")]
        [SerializeField, Range(0f, 0.3f)] private float smolderFloor = 0.08f;

        [Header("Оптимальный режим (для стрелки и обжарки)")]
        [Tooltip("Нижняя граница оптимальной зоны температуры.")]
        [SerializeField, Range(0f, 1f)] private float optimalMin = 0.55f;
        [Tooltip("Верхняя граница оптимальной зоны температуры.")]
        [SerializeField, Range(0f, 1f)] private float optimalMax = 0.75f;

        [Header("Состояния (по яркости свечения)")]
        [SerializeField] private ItemStateSO stateCold;     // холодная
        [SerializeField] private ItemStateSO stateSmolder;  // тлеет
        [SerializeField] private ItemStateSO stateEven;     // горит ровно
        [SerializeField] private ItemStateSO stateStrong;   // горит сильно

        [Header("Зона конфорки")]
        [Tooltip("Точка центра конфорки — над ней стоит сковорода/турка.")]
        [SerializeField] private Transform burnerPoint;
        [Tooltip("Радиус, в пределах которого предмет считается стоящим на конфорке.")]
        [SerializeField] private float burnerRadius = 0.12f;

        /// <summary>Текущая температура 0..1.</summary>
        public float Temperature { get; private set; }

        /// <summary>True, если температура в оптимальной зоне.</summary>
        public bool IsOptimal => Temperature >= optimalMin && Temperature <= optimalMax;

        /// <summary>
        /// Значение для стрелки-индикатора: -1 (слишком мало огня) .. 0 (оптимум) .. +1 (слишком горячо).
        /// </summary>
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

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            Temperature = smolderFloor;
        }

        private void Update()
        {
            bool pumping = cursorHandler != null
                           && bellowsHandle != null
                           && cursorHandler.DirectHoldTarget == bellowsHandle;

            if (pumping)
                Temperature += pumpRate * Time.deltaTime;
            else
                Temperature -= decayRate * Time.deltaTime;

            Temperature = Mathf.Clamp(Temperature, smolderFloor, 1f);

            UpdateGlowState();
        }

        private void UpdateGlowState()
        {
            ItemStateSO target;
            if (Temperature <= smolderFloor + 0.001f) target = stateSmolder != null ? stateSmolder : stateCold;
            else if (Temperature < optimalMin)        target = stateSmolder;
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

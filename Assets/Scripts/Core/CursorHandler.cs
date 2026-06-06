using UnityEngine;
using TimelessBrew.Items;

namespace TimelessBrew.Core
{
    /// <summary>
    /// Единый обработчик курсорного взаимодействия (§8.1: один CursorHandler на сцену).
    /// Реализует модель §4.1:
    ///   - НЕТ предмета на курсоре + клик по предмету  → взять предмет ("прицепить" к курсору);
    ///   - ЕСТЬ предмет на курсоре + наведение на цель + УДЕРЖАНИЕ ЛКМ → действие "источник льёт в цель";
    ///   - ЕСТЬ предмет на курсоре + клик в свободное место → поставить предмет.
    ///
    /// "Свободное место" определяется как точка на слое стола, где нет другого предмета.
    /// Взятый предмет визуально следует за курсором на фиксированной высоте над столом.
    ///
    /// Зависит от GameInput (ввод) и физических коллайдеров на предметах и столе.
    /// </summary>
    public class CursorHandler : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private Camera gameCamera;

        [Header("Слои")]
        [Tooltip("Слой(и), на которых лежат интерактивные предметы.")]
        [SerializeField] private LayerMask itemLayer;
        [Tooltip("Слой поверхности стола (для постановки предметов в свободное место).")]
        [SerializeField] private LayerMask tableLayer;

        [Header("Удержание предмета")]
        [Tooltip("Высота, на которой предмет 'висит' над точкой стола под курсором.")]
        [SerializeField] private float carryHeight = 0.15f;
        [Tooltip("Сглаживание следования предмета за курсором (0 = мгновенно).")]
        [SerializeField] private float followSmoothing = 12f;

        [Header("Действие удержанием")]
        [Tooltip("Сколько секунд нужно удерживать ЛКМ на цели, чтобы действие 'засчиталось' для дискретных переходов. Непрерывные процессы (варка) читают HeldOnTarget напрямую.")]
        [SerializeField] private float holdActionTime = 0.0f;

        /// <summary>Предмет, который сейчас "на курсоре". null, если рука пуста.</summary>
        public InteractableItem Carried { get; private set; }

        /// <summary>Цель, на которую сейчас наведён курсор с предметом в руке (для подсветки/процессов).</summary>
        public InteractableItem HoverTarget { get; private set; }

        /// <summary>True, пока игрок удерживает ЛКМ, наведя источник на валидную цель.</summary>
        public bool HeldOnTarget { get; private set; }

        /// <summary>
        /// Предмет, который игрок удерживает ЛКМ ПУСТОЙ рукой (без предмета на курсоре).
        /// Нужно для прямых действий-удержаний: качание мехов печки (§6.1), вращение ручки
        /// кофемолки (§6.5), потряхивание дуршлага (§6.3). null, если такого нет.
        /// Компоненты вроде Stove читают это свойство сами.
        /// </summary>
        public InteractableItem DirectHoldTarget { get; private set; }

        private float _holdTimer;
        private Vector3 _carryVelocity; // для SmoothDamp, если понадобится

        private bool _subscribed;

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        // Подписка в Start, а не в OnEnable: Start гарантированно выполняется после
        // всех Awake, поэтому GameInput.Instance к этому моменту уже присвоен.
        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            // Если объект выключали/включали уже после Start — переподписываемся.
            if (didStartOnce) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private bool didStartOnce;

        private void Subscribe()
        {
            didStartOnce = true;
            if (_subscribed || GameInput.Instance == null) return;
            GameInput.Instance.OnInteractPressed += HandleClick;
            GameInput.Instance.OnInteractReleased += HandleRelease;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || GameInput.Instance == null) return;
            GameInput.Instance.OnInteractPressed -= HandleClick;
            GameInput.Instance.OnInteractReleased -= HandleRelease;
            _subscribed = false;
        }

        private void Update()
        {
            if (Carried != null)
            {
                MoveCarriedToCursor();
                UpdateHoverTarget();
                UpdateHoldAction();
                DirectHoldTarget = null;
            }
            else
            {
                UpdateDirectHold();
            }
        }

        // --- Прямое удержание пустой рукой (мехи, ручка кофемолки, дуршлаг) ---
        private void UpdateDirectHold()
        {
            DirectHoldTarget = null;
            HeldOnTarget = false;

            bool held = GameInput.Instance != null && GameInput.Instance.InteractHeld;
            if (!held) return;

            if (RaycastFromCursor(itemLayer, out var hit))
            {
                var item = hit.collider.GetComponentInParent<InteractableItem>();
                if (item != null)
                    DirectHoldTarget = item;
            }
        }

        // --- Клик: либо взять, либо поставить ---
        private void HandleClick()
        {
            
            if (Carried == null)
            {
                TryPickUp();
            }
            else
            {
                // Если кликнули, наведя на валидную цель приёма — это не "постановка",
                // а начало действия (высыпать/налить). Постановка — только в свободное место.
                if (HoverTarget != null && HoverTarget.CanReceive && Carried.CanPour)
                {
                    // Действие начнётся через удержание (см. UpdateHoldAction). Ничего не ставим.
                    return;
                }
                TryPlaceInFreeSpace();
            }
        }

        private void HandleRelease()
        {
            // Отпускание ЛКМ мгновенно прекращает действие (§4.4, §6.6: поток кофе обрывается сразу).
            HeldOnTarget = false;
            _holdTimer = 0f;
        }

        private void TryPickUp()
        {
            if (!RaycastFromCursor(itemLayer, out var hit)) return;
            var item = hit.collider.GetComponentInParent<InteractableItem>();
            if (item == null || !item.IsPickable) return;

            Carried = item;
            // Здесь можно отключить коллайдер взятого предмета, чтобы он не мешал raycast'у цели.
            SetCarriedPhysics(false);
        }

        private void TryPlaceInFreeSpace()
        {
            if (!RaycastFromCursor(tableLayer, out var hit)) return;

            // Проверка, что точка действительно свободна (нет другого предмета прямо под курсором).
            if (RaycastFromCursor(itemLayer, out var itemHit))
            {
                var other = itemHit.collider.GetComponentInParent<InteractableItem>();
                if (other != null && other != Carried) return; // занято — не ставим
            }

            Carried.transform.position = hit.point;
            SetCarriedPhysics(true);
            Carried = null;
            HoverTarget = null;
            HeldOnTarget = false;
            _holdTimer = 0f;
        }

        private void MoveCarriedToCursor()
        {
            Vector3 target;
            if (RaycastFromCursor(tableLayer, out var hit))
                target = hit.point + Vector3.up * carryHeight;
            else
                target = ScreenPointToWorldFallback();

            if (followSmoothing <= 0f)
                Carried.transform.position = target;
            else
                Carried.transform.position = Vector3.Lerp(
                    Carried.transform.position, target, followSmoothing * Time.deltaTime);
        }

        private void UpdateHoverTarget()
        {
            HoverTarget = null;
            if (RaycastFromCursor(itemLayer, out var hit))
            {
                var item = hit.collider.GetComponentInParent<InteractableItem>();
                if (item != null && item != Carried)
                    HoverTarget = item;
            }
        }

        private void UpdateHoldAction()
        {
            bool valid = HoverTarget != null
                         && HoverTarget.CanReceive
                         && Carried.CanPour
                         && GameInput.Instance != null
                         && GameInput.Instance.InteractHeld;

            HeldOnTarget = valid;

            if (!valid)
            {
                _holdTimer = 0f;
                return;
            }

            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdActionTime)
            {
                // Дискретный приём (напр. высыпать порцию зёрен). Непрерывные процессы
                // (варка, наполнение) пусть читают HeldOnTarget сами и накапливают по времени.
                bool received = HoverTarget.TryReceive(Carried);
                if (received && holdActionTime > 0f)
                    _holdTimer = 0f; // готов к следующей порции
            }
        }

        // --- Утилиты ---
        private bool RaycastFromCursor(LayerMask mask, out RaycastHit hit)
        {
            Vector2 screen = GameInput.Instance != null
                ? GameInput.Instance.PointerScreenPosition
                : (Vector2)Input.mousePosition;
            Ray ray = gameCamera.ScreenPointToRay(screen);
            return Physics.Raycast(ray, out hit, 100f, mask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 ScreenPointToWorldFallback()
        {
            Vector2 screen = GameInput.Instance != null
                ? GameInput.Instance.PointerScreenPosition
                : (Vector2)Input.mousePosition;
            Ray ray = gameCamera.ScreenPointToRay(screen);
            return ray.GetPoint(2f);
        }

        private void SetCarriedPhysics(bool enabled)
        {
            if (Carried == null) return;
            // Коллайдер взятого предмета выключаем, чтобы он не перекрывал raycast по цели и столу.
            foreach (var col in Carried.GetComponentsInChildren<Collider>())
                col.enabled = enabled;
            if (Carried.TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = !enabled;
        }
    }
}
using UnityEngine;
using TimelessBrew.Items;

namespace TimelessBrew.Core
{
    // Обработчик курсора (§4.1): один на сцену.
    //   - рука пустая + клик по предмету        -> взять предмет на курсор;
    //   - предмет на курсоре + удержание над целью -> действие (источник льёт в цель);
    //   - предмет на курсоре + клик в свободное место -> поставить предмет.
    // Зависит от GameInput (ввод) и коллайдеров на предметах и столе.
    public class CursorHandler : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private Camera gameCamera;

        [Header("Слои")]
        [SerializeField] private LayerMask itemLayer;    // слой предметов
        [SerializeField] private LayerMask tableLayer;   // слой стола (куда ставим)

        [Header("Удержание предмета")]
        [SerializeField] private float carryHeight = 0.15f;     // высота предмета над столом
        [SerializeField] private float followSmoothing = 12f;   // сглаживание (0 = мгновенно)

        [Header("Действие удержанием")]
        [SerializeField] private float holdActionTime = 0f;     // сек удержания для дискретного приёма

        public InteractableItem Carried { get; private set; }       // предмет на курсоре (null = рука пуста)
        public InteractableItem HoverTarget { get; private set; }   // на что наведён предмет в руке
        public bool HeldOnTarget { get; private set; }              // держим ЛКМ, наведя источник на цель

        // Предмет, который держат ЛКМ ПУСТОЙ рукой (мехи печки, ручка кофемолки, кран и т.п.).
        public InteractableItem DirectHoldTarget { get; private set; }

        private float _holdTimer;
        private bool _subscribed;
        private bool _didStartOnce;

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        // Подписка в Start (а не OnEnable): к Start GameInput.Instance уже создан.
        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            if (_didStartOnce) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            _didStartOnce = true;
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

        // Прямое удержание пустой рукой (мехи, ручка кофемолки, дуршлаг, кран).
        private void UpdateDirectHold()
        {
            DirectHoldTarget = null;
            HeldOnTarget = false;

            bool held = GameInput.Instance != null && GameInput.Instance.InteractHeld;
            if (!held) return;

            RaycastHit hit;
            if (RaycastFromCursor(itemLayer, out hit))
            {
                InteractableItem item = hit.collider.GetComponentInParent<InteractableItem>();
                if (item != null) DirectHoldTarget = item;
            }
        }

        // Клик: взять либо поставить.
        private void HandleClick()
        {
            if (Carried == null)
            {
                TryPickUp();
                return;
            }

            // Если навели на цель приёма — это не постановка, а начало действия (через удержание).
            if (HoverTarget != null && HoverTarget.CanReceive && Carried.CanPour)
                return;

            TryPlaceInFreeSpace();
        }

        private void HandleRelease()
        {
            HeldOnTarget = false;
            _holdTimer = 0f;
        }

        private void TryPickUp()
        {
            RaycastHit hit;
            if (!RaycastFromCursor(itemLayer, out hit)) return;

            InteractableItem item = hit.collider.GetComponentInParent<InteractableItem>();
            if (item == null || !item.IsPickable) return;

            Carried = item;
            SetCarriedPhysics(false);   // выключаем коллайдер, чтобы не мешал рейкасту цели
        }

        private void TryPlaceInFreeSpace()
        {
            RaycastHit tableHit;
            if (!RaycastFromCursor(tableLayer, out tableHit)) return;

            // Точка занята, если под курсором есть другой предмет.
            RaycastHit itemHit;
            if (RaycastFromCursor(itemLayer, out itemHit))
            {
                InteractableItem other = itemHit.collider.GetComponentInParent<InteractableItem>();
                if (other != null && other != Carried) return;
            }

            Carried.transform.position = tableHit.point;
            SetCarriedPhysics(true);
            Carried = null;
            HoverTarget = null;
            HeldOnTarget = false;
            _holdTimer = 0f;
        }

        private void MoveCarriedToCursor()
        {
            Vector3 target;
            RaycastHit hit;
            if (RaycastFromCursor(tableLayer, out hit))
                target = hit.point + Vector3.up * carryHeight;
            else
                target = ScreenPointToWorldFallback();

            if (followSmoothing <= 0f)
                Carried.transform.position = target;
            else
                Carried.transform.position = Vector3.Lerp(Carried.transform.position, target, followSmoothing * Time.deltaTime);
        }

        private void UpdateHoverTarget()
        {
            HoverTarget = null;
            RaycastHit hit;
            if (RaycastFromCursor(itemLayer, out hit))
            {
                InteractableItem item = hit.collider.GetComponentInParent<InteractableItem>();
                if (item != null && item != Carried) HoverTarget = item;
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
                bool received = HoverTarget.TryReceive(Carried);
                if (received && holdActionTime > 0f)
                    _holdTimer = 0f;   // готов к следующей порции
            }
        }

        // Позиция курсора на экране (из GameInput, иначе старый Input).
        private Vector2 GetPointerScreen()
        {
            if (GameInput.Instance != null) return GameInput.Instance.PointerScreenPosition;
            return Input.mousePosition;
        }

        private bool RaycastFromCursor(LayerMask mask, out RaycastHit hit)
        {
            Ray ray = gameCamera.ScreenPointToRay(GetPointerScreen());
            return Physics.Raycast(ray, out hit, 100f, mask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 ScreenPointToWorldFallback()
        {
            Ray ray = gameCamera.ScreenPointToRay(GetPointerScreen());
            return ray.GetPoint(2f);
        }

        private void SetCarriedPhysics(bool enabled)
        {
            if (Carried == null) return;

            Collider[] cols = Carried.GetComponentsInChildren<Collider>();
            foreach (Collider col in cols) col.enabled = enabled;

            Rigidbody rb = Carried.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = !enabled;
        }
    }
}

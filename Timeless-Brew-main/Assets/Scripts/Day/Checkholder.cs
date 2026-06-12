using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Чекхолдер на навесе над столом (§3.3, §6 v4.3). Поток v4.3:
    ///   1. Гость дошёл до стойки → бумажка появляется ПЕРЕД Нари (точка newTicketPoint), «не взята».
    ///   2. Игрок берёт её и вешает на чекхолдер (клик по бумажке → OrderTicket.Claim) →
    ///      бумажка перемещается на свободный крючок.
    ///   3. Гость ушёл → бумажка снимается.
    ///
    /// Шаг 5 (оценка) спрашивает заказ текущего гостя через <see cref="GetTicketFor"/>.
    /// Флаг autoClaimOnArrival сразу «вешает» бумажку при появлении — удобно для быстрого
    /// теста цикла дня без ручного клика.
    /// </summary>
    public class Checkholder : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Спавнер гостей. Если пусто — найдётся в сцене автоматически.")]
        [SerializeField] private GuestSpawner spawner;

        [Tooltip("Префаб тикета (с компонентом OrderTicket). Пусто — создаётся заглушка-кубик.")]
        [SerializeField] private GameObject ticketPrefab;

        [Header("Точки")]
        [Tooltip("Где появляется новая бумажка (перед Нари). Пусто — у позиции чекхолдера.")]
        [SerializeField] private Transform newTicketPoint;

        [Tooltip("Крючки на планке. Взятая в работу бумажка вешается на первый свободный.")]
        [SerializeField] private Transform[] hooks;

        [Header("Поведение")]
        [Tooltip("Сразу вешать бумажку при появлении (без ручного клика) — для быстрого теста цикла дня.")]
        [SerializeField] private bool autoClaimOnArrival = false;

        private readonly List<OrderTicket> _active = new();
        private bool _subscribed;

        /// <summary>Все висящие/появившиеся бумажки.</summary>
        public IReadOnlyList<OrderTicket> ActiveTickets => _active;

        /// <summary>Самая свежая бумажка. null — пусто.</summary>
        public OrderTicket ActiveTicket => _active.Count > 0 ? _active[_active.Count - 1] : null;

        private void Start()
        {
            if (spawner == null) spawner = FindObjectOfType<GuestSpawner>();
            Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || spawner == null) return;
            spawner.OnGuestArrived += HandleGuestArrived;
            spawner.OnGuestLeft += HandleGuestLeft;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || spawner == null) return;
            spawner.OnGuestArrived -= HandleGuestArrived;
            spawner.OnGuestLeft -= HandleGuestLeft;
            _subscribed = false;
        }

        /// <summary>Найти бумажку, привязанную к конкретному гостю (для оценки в шаге 5).</summary>
        public OrderTicket GetTicketFor(GuestController guest)
        {
            foreach (var t in _active)
                if (t != null && t.Guest == guest)
                    return t;
            return null;
        }

        private void HandleGuestArrived(GuestController guest)
        {
            if (guest == null || guest.Visit == null) return;

            Vector3 pos = newTicketPoint != null ? newTicketPoint.position : transform.position;
            GameObject go = CreateTicketObject(pos);
            var ticket = go.GetComponent<OrderTicket>();
            if (ticket == null) ticket = go.AddComponent<OrderTicket>();

            ticket.Bind(guest, guest.Visit);
            ticket.OnClaimed += HandleTicketClaimed;
            go.name = $"Ticket_{guest.Visit.DisplayName}";
            _active.Add(ticket);

            if (autoClaimOnArrival) ticket.Claim();
        }

        private void HandleTicketClaimed(OrderTicket ticket)
        {
            // Бумажка взята в работу — вешаем на свободный крючок.
            Transform hook = NextFreeHook();
            if (hook != null)
            {
                ticket.transform.SetParent(hook, false);
                ticket.transform.position = hook.position;
            }
        }

        private void HandleGuestLeft(GuestController guest)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] != null && _active[i].Guest == guest)
                {
                    _active[i].OnClaimed -= HandleTicketClaimed;
                    Destroy(_active[i].gameObject);
                    _active.RemoveAt(i);
                }
            }
        }

        private Transform NextFreeHook()
        {
            if (hooks == null || hooks.Length == 0) return null;
            // Считаем, сколько бумажек уже взято в работу — на тот крючок и вешаем.
            int claimedCount = 0;
            foreach (var t in _active)
                if (t != null && t.Claimed) claimedCount++;
            int idx = Mathf.Clamp(claimedCount - 1, 0, hooks.Length - 1);
            return hooks[idx];
        }

        private GameObject CreateTicketObject(Vector3 pos)
        {
            if (ticketPrefab != null)
                return Instantiate(ticketPrefab, pos, Quaternion.identity);

            // Заглушка: маленький плоский кубик-«бумажка» с коллайдером (для клика-взятия).
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.2f, 0.28f, 0.01f);
            return go;
        }
    }
}

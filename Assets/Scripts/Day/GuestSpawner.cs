using System;
using UnityEngine;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Спавнер гостей (§4.4). Оркестрирует тайминг: берёт следующий визит у <see cref="DayManager"/>,
    /// создаёт модель гостя в точке входа, ведёт его к стойке, а когда гость ушёл — выпускает
    /// следующего. В прототипе у стойки одновременно ровно один гость.
    ///
    /// Если у гостя не задан visitorPrefab, спавнится капсула-заглушка — чтобы цикл можно было
    /// гонять без артов (см. наш план: тестовая сцена на примитивах).
    /// </summary>
    public class GuestSpawner : MonoBehaviour
    {
        [Header("Точки маршрута (если пусто — берётся позиция спавнера)")]
        [Tooltip("Точка входа (дверь). Здесь появляется гость.")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Точка у стойки, куда гость подходит для заказа.")]
        [SerializeField] private Transform counterPoint;
        [Tooltip("Точка выхода. Если пусто — используется точка входа.")]
        [SerializeField] private Transform exitPoint;

        [Header("Параметры")]
        [Tooltip("Скорость ходьбы гостя (ед./сек). Большое значение = почти мгновенно.")]
        [SerializeField] private float walkSpeed = 2f;
        [Tooltip("Пауза перед приходом следующего гостя, сек.")]
        [SerializeField] private float delayBetweenGuests = 0.5f;

        [Header("Заглушка визуала")]
        [Tooltip("Префаб гостя по умолчанию, если у GuestSO не задан свой. Пусто — спавнится капсула.")]
        [SerializeField] private GameObject fallbackVisitorPrefab;

        /// <summary>Гость, который сейчас у стойки (или идёт к ней). null — никого нет.</summary>
        public GuestController ActiveGuest { get; private set; }

        public event Action<GuestController> OnGuestArrived;
        public event Action<GuestController> OnGuestLeft;

        private bool _subscribed;

        private void Start()
        {
            Subscribe();

            // Подстраховка от порядка инициализации: если DayManager уже стартовал день
            // в своём Start() до того, как мы подписались — стартуем выдачу вручную.
            if (DayManager.Instance != null && DayManager.Instance.DayRunning && ActiveGuest == null)
                SpawnNext();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || DayManager.Instance == null) return;
            DayManager.Instance.OnDayStarted += HandleDayStarted;
            DayManager.Instance.OnDayEnded += HandleDayEnded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || DayManager.Instance == null) return;
            DayManager.Instance.OnDayStarted -= HandleDayStarted;
            DayManager.Instance.OnDayEnded -= HandleDayEnded;
            _subscribed = false;
        }

        private void HandleDayStarted() => SpawnNext();

        private void HandleDayEnded()
        {
            // Гостей больше не будет. Активный (если есть) доиграет свой уход сам.
            Debug.Log("[GuestSpawner] День закрыт — спавн остановлен.", this);
        }

        /// <summary>Выпустить следующего гостя, если у стойки свободно и в дне ещё есть визиты.</summary>
        public void SpawnNext()
        {
            if (ActiveGuest != null) return;
            if (DayManager.Instance == null) return;
            if (!DayManager.Instance.TryGetNextVisit(out var visit)) return; // день окончен

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            Vector3 counterPos = counterPoint != null ? counterPoint.position : transform.position;
            Vector3 exitPos = exitPoint != null ? exitPoint.position : spawnPos;

            GameObject go = CreateVisitorObject(visit, spawnPos);
            go.name = $"Guest_{visit.DisplayName}";

            var gc = go.GetComponent<GuestController>();
            if (gc == null) gc = go.AddComponent<GuestController>();

            gc.OnArrivedAtCounter += HandleArrived;
            gc.OnLeft += HandleLeft;
            gc.Init(visit, counterPos, exitPos, walkSpeed);

            ActiveGuest = gc;
            Debug.Log($"[GuestSpawner] Пришёл гость: {visit.DisplayName} ({visit.phase}).", this);
        }

        private GameObject CreateVisitorObject(GuestVisit visit, Vector3 pos)
        {
            GameObject prefab = visit.guest != null && visit.guest.visitorPrefab != null
                ? visit.guest.visitorPrefab
                : fallbackVisitorPrefab;

            if (prefab != null)
                return Instantiate(prefab, pos, Quaternion.identity);

            // Заглушка: капсула, чтобы видеть гостя в сцене без моделей.
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.position = pos;
            return capsule;
        }

        private void HandleArrived(GuestController gc)
        {
            OnGuestArrived?.Invoke(gc);
        }

        private void HandleLeft(GuestController gc)
        {
            gc.OnArrivedAtCounter -= HandleArrived;
            gc.OnLeft -= HandleLeft;

            if (ActiveGuest == gc) ActiveGuest = null;
            OnGuestLeft?.Invoke(gc);

            // Следующий гость — после небольшой паузы.
            if (delayBetweenGuests > 0f) Invoke(nameof(SpawnNext), delayBetweenGuests);
            else SpawnNext();
        }
    }
}

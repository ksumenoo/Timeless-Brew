using System;
using UnityEngine;
using TimelessBrew.Data;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Поведение одного гостя за визит (§4.4: гость заходит → подходит к стойке →
    /// озвучивает заказ → ждёт → получает напиток → уходит).
    ///
    /// Скрипт тонкий и самодостаточный: простая стейт-машина с движением к точке у стойки
    /// и обратно к выходу через MoveTowards. Модель может быть любой (или примитив-заглушка) —
    /// контроллер только двигает Transform и шлёт события. Оценку и подачу делает шаг 5,
    /// он вызывает <see cref="CompleteVisit"/>.
    /// </summary>
    public class GuestController : MonoBehaviour
    {
        public enum State { Arriving, AtCounter, Leaving, Gone }

        public GuestVisit Visit { get; private set; }
        public State CurrentState { get; private set; } = State.Arriving;

        /// <summary>Итог визита (выставляется при подаче напитка). До подачи — 0 звёзд.</summary>
        public DrinkRating Rating { get; private set; }

        // --- События для спавнера / чекхолдера / камеры ---
        /// <summary>Гость дошёл до стойки и готов сделать заказ (чекхолдер вешает тикет).</summary>
        public event Action<GuestController> OnArrivedAtCounter;
        /// <summary>Гостю подали напиток — визит обслужен (до ухода).</summary>
        public event Action<GuestController> OnServed;
        /// <summary>Гость ушёл; объект будет уничтожен сразу после события.</summary>
        public event Action<GuestController> OnLeft;

        private Vector3 _counterPos;
        private Vector3 _exitPos;
        private float _speed;
        private const float ReachEpsilon = 0.05f;

        /// <summary>
        /// Инициализация визита спавнером. Объект уже размещён в точке входа;
        /// контроллер сам доведёт гостя до стойки и (после подачи) до выхода.
        /// </summary>
        public void Init(GuestVisit visit, Vector3 counterPos, Vector3 exitPos, float speed)
        {
            Visit = visit;
            _counterPos = counterPos;
            _exitPos = exitPos;
            _speed = Mathf.Max(0.01f, speed);
            CurrentState = State.Arriving;
        }

        private void Update()
        {
            switch (CurrentState)
            {
                case State.Arriving:
                    if (MoveToward(_counterPos))
                    {
                        CurrentState = State.AtCounter;
                        OnArrivedAtCounter?.Invoke(this);
                    }
                    break;

                case State.Leaving:
                    if (MoveToward(_exitPos))
                    {
                        CurrentState = State.Gone;
                        OnLeft?.Invoke(this);
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        /// <summary>Двигает объект к точке. Возвращает true, когда дошёл (или скорость мгновенная).</summary>
        private bool MoveToward(Vector3 target)
        {
            Vector3 pos = transform.position;
            if ((pos - target).sqrMagnitude <= ReachEpsilon * ReachEpsilon)
            {
                transform.position = target;
                return true;
            }
            transform.position = Vector3.MoveTowards(pos, target, _speed * Time.deltaTime);
            return false;
        }

        /// <summary>
        /// Завершить обслуживание: гостю подан напиток с посчитанной оценкой (вызывает шаг 5).
        /// Гость реагирует и уходит. Повторные вызовы игнорируются.
        /// </summary>
        public void CompleteVisit(DrinkRating rating)
        {
            if (CurrentState != State.AtCounter) return;

            Rating = rating;
            OnServed?.Invoke(this);
            CurrentState = State.Leaving;
        }

        // Для проверки цепочки до готовности шага 5: подать «идеальный» напиток и отпустить гостя.
        [ContextMenu("DEBUG: Serve (perfect) & leave")]
        private void DebugServe()
        {
            CompleteVisit(DrinkRating.Perfect);
        }
    }
}

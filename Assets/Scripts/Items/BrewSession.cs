using System;
using UnityEngine;
using TimelessBrew.Data;

namespace TimelessBrew.Items
{
    /// <summary>
    /// Накопитель одного готовящегося напитка. Данные о напитке собираются на протяжении
    /// всех пяти этапов (§4.2) задолго до того, как появляется физическая чашка, поэтому
    /// они живут здесь, а не на объекте чашки.
    ///
    /// Этапы готовки пишут результат сюда (RecordRoast, RecordGrind, RecordBrew, …),
    /// а точка подачи (ServingStation) забирает итог через <see cref="TakeResult"/>,
    /// сравнивает с заказом и начинает новый напиток.
    ///
    /// Один экземпляр на сцену (Instance). Между гостями вызывается StartNew().
    /// </summary>
    public class BrewSession : MonoBehaviour
    {
        public static BrewSession Instance { get; private set; }

        private BrewResult _current = new();

        /// <summary>Текущий собираемый напиток (никогда не null).</summary>
        public BrewResult Current => _current;

        /// <summary>Что-то записали в напиток. Для UI/подсказок.</summary>
        public event Action<BrewResult> OnChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Начать новый напиток с чистого листа (новый гость / выкинули брак).</summary>
        public void StartNew()
        {
            _current = new BrewResult();
            OnChanged?.Invoke(_current);
        }

        /// <summary>Забрать готовый напиток и сразу начать следующий. Вызывает подача.</summary>
        public BrewResult TakeResult()
        {
            BrewResult done = _current;
            _current = new BrewResult();
            OnChanged?.Invoke(_current);
            return done;
        }

        // --- Запись этапов (§4.2) ---

        /// <summary>Этап 1: обжарка зафиксирована — степень + качество выполнения (0..1).</summary>
        public void RecordRoast(RoastLevel level, float quality01)
        {
            _current.spec.roast = level;
            _current.roastQuality = Mathf.Clamp01(quality01);
            Changed();
        }

        /// <summary>Этап 2: просеивание шелухи — чистота 0..1.</summary>
        public void RecordSift(float quality01)
        {
            _current.siftQuality = Mathf.Clamp01(quality01);
            Changed();
        }

        /// <summary>Этап 3a: помол — выбранная степень.</summary>
        public void RecordGrind(GrindSize size)
        {
            _current.spec.grind = size;
            Changed();
        }

        /// <summary>Этап 3b: варка — тип воды + качество (пенка не перелита и т.п.), 0..1.</summary>
        public void RecordBrew(WaterType water, float quality01)
        {
            _current.spec.water = water;
            _current.brewQuality = Mathf.Clamp01(quality01);
            Changed();
        }

        /// <summary>Сахар: добавить кубики (§6.9).</summary>
        public void AddSugar(int cubes = 1)
        {
            _current.spec.sugarCubes = Mathf.Max(0, _current.spec.sugarCubes + cubes);
            Changed();
        }

        /// <summary>Сироп (§6.11).</summary>
        public void SetSyrup(SyrupType syrup)
        {
            _current.spec.syrup = syrup;
            Changed();
        }

        /// <summary>Специя добавлена (§6.10). id — напр. 'cardamom', 'cocoa'.</summary>
        public void AddSpice(string spiceId)
        {
            if (string.IsNullOrEmpty(spiceId)) return;
            if (!_current.spec.spices.Contains(spiceId))
                _current.spec.spices.Add(spiceId);
            Changed();
        }

        /// <summary>Этап 5: молоко добавлено; latteArtQuality 0..1 (-1 если без арта).</summary>
        public void SetMilk(bool on, float latteArtQuality = -1f)
        {
            _current.spec.withMilk = on;
            _current.latteArtQuality = on ? latteArtQuality : -1f;
            Changed();
        }

        /// <summary>Имя напитка для тикета/лога (опционально).</summary>
        public void SetDrinkName(string name)
        {
            _current.spec.drinkName = name;
            Changed();
        }

        private void Changed() => OnChanged?.Invoke(_current);
    }
}

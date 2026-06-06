using System;
using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Сковорода + мини-игра обжарки (§4.2 этап 1, §6.2).
    ///
    /// Поток:
    ///   1. Игрок высыпает зелёные зёрна в сковороду (TryReceive от банки) → состояние "с зёрнами".
    ///   2. Сковорода ставится на конфорку печки → пока она на печке и печь греет,
    ///      progress обжарки растёт со скоростью, зависящей от температуры печки.
    ///   3. Цвет зерна в реальном времени:
    ///         зелёный → жёлтый → светло-коричневый → насыщенно-коричневый → чёрный(брак).
    ///   4. Игрок снимает сковороду (берёт на курсор) в нужный момент → обжарка фиксируется.
    ///
    /// Помешивание необязательно (упрощение по §4.2). Скорость обжарки максимальна в оптимальной
    /// зоне печки; при перегреве растёт быстрее и легче уйти в пережарку.
    ///
    /// Эталонный цвет/окно готовности зависит от сорта зерна; в прототипе один сорт,
    /// окно задаётся полями ideal*. Позже окно будет браться из RecipeData/блокнота (§4.3).
    /// </summary>
    public class RoastingPan : InteractableItem
    {
        public enum RoastStage { Green, Yellow, LightBrown, RichBrown, Burnt }

        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [SerializeField] private Stove stove;

        [Header("Состояния сковороды (§6.2)")]
        [SerializeField] private ItemStateSO stateEmpty;   // пустая
        [SerializeField] private ItemStateSO stateBeans;   // с зёрнами
        [SerializeField] private ItemStateSO stateHot;     // горячая (после готовки)

        [Header("Обжарка")]
        [Tooltip("Базовая скорость прогресса обжарки в оптимальной зоне печки (ед./сек). Прогресс идёт 0..1+.")]
        [SerializeField] private float baseRoastRate = 0.12f;
        [Tooltip("Множитель скорости при перегреве печки (ArrowValue>0). Делает пережарку быстрой.")]
        [SerializeField] private float overheatMultiplier = 2.2f;

        [Header("Пороги стадий (progress)")]
        [SerializeField, Range(0f, 1.5f)] private float yellowAt     = 0.30f;
        [SerializeField, Range(0f, 1.5f)] private float lightBrownAt = 0.55f;
        [SerializeField, Range(0f, 1.5f)] private float richBrownAt  = 0.80f;
        [SerializeField, Range(0f, 1.5f)] private float burntAt      = 1.10f;

        [Header("Окно идеальной обжарки (для оценки)")]
        [Tooltip("Нижняя граница 'правильной' прожарки по progress.")]
        [SerializeField, Range(0f, 1.5f)] private float idealMin = 0.80f;
        [Tooltip("Верхняя граница 'правильной' прожарки по progress.")]
        [SerializeField, Range(0f, 1.5f)] private float idealMax = 1.00f;

        [Header("Визуал зерна")]
        [Tooltip("Renderer зёрен в сковороде. Цвет интерполируется по стадиям. Опционально.")]
        [SerializeField] private Renderer beanRenderer;
        [SerializeField] private Color colorGreen      = new(0.45f, 0.60f, 0.30f);
        [SerializeField] private Color colorYellow      = new(0.80f, 0.70f, 0.35f);
        [SerializeField] private Color colorLightBrown = new(0.65f, 0.45f, 0.25f);
        [SerializeField] private Color colorRichBrown  = new(0.35f, 0.20f, 0.10f);
        [SerializeField] private Color colorBurnt      = new(0.08f, 0.06f, 0.05f);

        /// <summary>0 = зелёные, ~1 = насыщенно-коричневые (идеал), >burntAt = пережар.</summary>
        public float RoastProgress { get; private set; }
        public bool HasBeans { get; private set; }
        public RoastStage Stage { get; private set; } = RoastStage.Green;

        /// <summary>Срабатывает при переходе на новую стадию (для звука/подсказок).</summary>
        public event Action<RoastStage> OnStageChanged;

        private MaterialPropertyBlock _mpb;

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (stove == null) stove = FindObjectOfType<Stove>();
            _mpb = new MaterialPropertyBlock();
            if (stateEmpty != null) SetState(stateEmpty);
        }

        /// <summary>Банка с зёрнами высыпает в сковороду.</summary>
        public override bool TryReceive(InteractableItem source)
        {
            if (HasBeans) return false;                 // уже занята
            if (source == null) return false;
            if (source.itemType != "BeanJar") return false;
            if (!base.TryReceive(source)) return false; // проверка флагов состояния

            HasBeans = true;
            RoastProgress = 0f;
            SetStage(RoastStage.Green, force: true);
            if (stateBeans != null) SetState(stateBeans);
            return true;
        }

        private void Update()
        {
            if (!HasBeans) return;

            bool onBurner = stove != null
                            && cursorHandler != null
                            && cursorHandler.Carried != this   // не на курсоре = стоит на столе/печке
                            && stove.IsOnBurner(transform);

            if (onBurner)
            {
                float rate = baseRoastRate * Mathf.Max(0f, stove.Temperature);
                if (stove.ArrowValue > 0f)
                    rate *= Mathf.Lerp(1f, overheatMultiplier, stove.ArrowValue);

                RoastProgress += rate * Time.deltaTime;
                RoastProgress = Mathf.Min(RoastProgress, burntAt + 0.2f);
            }

            UpdateStage();
            UpdateBeanColor();
        }

        private void UpdateStage()
        {
            RoastStage s =
                RoastProgress >= burntAt      ? RoastStage.Burnt :
                RoastProgress >= richBrownAt  ? RoastStage.RichBrown :
                RoastProgress >= lightBrownAt ? RoastStage.LightBrown :
                RoastProgress >= yellowAt     ? RoastStage.Yellow :
                                                RoastStage.Green;
            if (s != Stage) SetStage(s);
        }

        private void SetStage(RoastStage s, bool force = false)
        {
            if (!force && s == Stage) return;
            Stage = s;
            OnStageChanged?.Invoke(s);
        }

        private void UpdateBeanColor()
        {
            if (beanRenderer == null) return;

            Color c =
                RoastProgress >= burntAt      ? colorBurnt :
                RoastProgress >= richBrownAt  ? Color.Lerp(colorRichBrown, colorBurnt, Inv(richBrownAt, burntAt)) :
                RoastProgress >= lightBrownAt ? Color.Lerp(colorLightBrown, colorRichBrown, Inv(lightBrownAt, richBrownAt)) :
                RoastProgress >= yellowAt     ? Color.Lerp(colorYellow, colorLightBrown, Inv(yellowAt, lightBrownAt)) :
                                                Color.Lerp(colorGreen, colorYellow, Inv(0f, yellowAt));

            beanRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c); // URP Lit; для встроенного — "_Color"
            beanRenderer.SetPropertyBlock(_mpb);
        }

        private float Inv(float a, float b) => Mathf.InverseLerp(a, b, RoastProgress);

        /// <summary>
        /// Качество обжарки 0..1 для системы оценки (§4.3, параметр "Обжарка").
        /// 1.0 — точно в окне; за пределами окна линейно падает; пережар = 0.
        /// </summary>
        public float RoastQuality01()
        {
            if (Stage == RoastStage.Burnt) return 0f;
            if (RoastProgress >= idealMin && RoastProgress <= idealMax) return 1f;

            float dist = RoastProgress < idealMin
                ? idealMin - RoastProgress
                : RoastProgress - idealMax;
            // ширина окна как масштаб допуска
            float tolerance = Mathf.Max(0.15f, (idealMax - idealMin));
            return Mathf.Clamp01(1f - dist / tolerance);
        }

        /// <summary>Зафиксировать обжарку (вызывается, когда сковорода снята и зёрна уходят в дуршлаг).</summary>
        public void EmptyToDurshlag()
        {
            HasBeans = false;
            if (stateHot != null) SetState(stateHot);
        }
    }
}

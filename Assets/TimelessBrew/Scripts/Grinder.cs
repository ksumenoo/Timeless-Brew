using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Кофемолка (§6.5). Принимает зёрна (банка/сковорода кликаются по ней — через Vessel.Beans),
    /// мелет удержанием ручки пустой рукой (чем дольше — тем мельче, до «в пыль»). Готовый молотый
    /// игрок забирает рукой (см. Hand) и несёт в турку. Нужен компонент <see cref="Vessel"/> kind=Grinder.
    /// </summary>
    [RequireComponent(typeof(Vessel))]
    public class Grinder : MonoBehaviour
    {
        [SerializeField] private float grindPerSecond = 0.4f;

        private Vessel _vessel;
        private float _grind;   // 0..1, тонкость помола

        private void Awake() => _vessel = GetComponent<Vessel>();

        public bool HasGrounds => _vessel.mix.beans > 0.001f;
        /// <summary>Тонкость помола 0..1 (для шкалы).</summary>
        public float GrindAmount => _grind;
        public GrindSize CurrentGrind =>
            _grind >= 0.75f ? GrindSize.Powder :
            _grind >= 0.5f ? GrindSize.Fine :
            _grind >= 0.25f ? GrindSize.Medium : GrindSize.Coarse;

        /// <summary>Крутить ручку (вызывает Crank, пока держим действие пустой рукой).</summary>
        public void Grind(float dt)
        {
            if (!HasGrounds) return;
            _grind = Mathf.Min(1f, _grind + grindPerSecond * dt);
        }

        /// <summary>Зачерпнуть молотый рукой (профиль партии уезжает вместе с горстью).</summary>
        public bool TakeGrounds(out GrindSize grind, out RoastLevel roast, out bool sifted)
        {
            grind = CurrentGrind;
            roast = _vessel.mix.roast;
            sifted = _vessel.mix.sifted;
            if (!HasGrounds) return false;
            _vessel.mix.beans = 0f;
            _grind = 0f;
            return true;
        }

        public string StatusLine()
        {
            if (!HasGrounds) return "пусто — насыпь зёрна";
            return $"Помол: {Mixture.GrindRu(CurrentGrind)} (крути ручку)";
        }
    }
}

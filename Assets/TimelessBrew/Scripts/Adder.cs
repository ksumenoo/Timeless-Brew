using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Источник добавки (§6.9–6.11): сахарница, сиропница, банка специи. Берётся рукой, клик над
    /// целью добавляет одну порцию: сахар — в турку/чашку, сироп — в чашку, специя — в турку/чашку.
    /// </summary>
    public class Adder : MonoBehaviour
    {
        public enum Kind { Sugar, Syrup, Spice }

        [SerializeField] private Kind kind = Kind.Sugar;
        [Tooltip("Id сиропа/специи, напр. 'caramel', 'cardamom'.")]
        [SerializeField] private string id = "";

        /// <summary>Тип добавки (нужен утилитам настройки сцены).</summary>
        public Kind AdderKind => kind;

        /// <summary>Порция успешно добавлена в сосуд (для щипцов сахарницы и т.п.).</summary>
        public event System.Action OnApplied;

        public void Configure(Kind k, string ingredientId) { kind = k; id = ingredientId; }

        /// <summary>Добавить одну порцию в сосуд, если это уместно.</summary>
        public void Apply(Vessel v)
        {
            if (v == null) return;
            bool applied = false;
            switch (kind)
            {
                case Kind.Sugar:
                    if (v.kind == Vessel.Kind.Cup || v.kind == Vessel.Kind.Cezve) { v.mix.sugar++; applied = true; }
                    break;
                case Kind.Syrup:
                    if (v.kind == Vessel.Kind.Cup && !v.mix.syrups.Contains(id)) { v.mix.syrups.Add(id); applied = true; }
                    break;
                case Kind.Spice:
                    if ((v.kind == Vessel.Kind.Cezve || v.kind == Vessel.Kind.Cup) && !v.mix.spices.Contains(id))
                    { v.mix.spices.Add(id); applied = true; }
                    break;
            }
            if (applied) OnApplied?.Invoke();
        }
    }
}

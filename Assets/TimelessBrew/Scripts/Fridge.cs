using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Холодильная камера (morozilka, §3.3): внутри стоят баночки молока. Клик пустой рукой
    /// по холодильнику — ближайшая баночка из его объёма сразу берётся в руку (логика в Hand).
    /// </summary>
    public class Fridge : MonoBehaviour
    {
        [Tooltip("Насколько расширить объём поиска баночек за пределы коллайдера холодильника.")]
        [SerializeField] private float searchPadding = 0.15f;

        /// <summary>Найти баночку молока внутри холодильника (null — пусто).</summary>
        public Grabbable TakeJar()
        {
            Bounds b = ColliderBounds();
            b.Expand(searchPadding * 2f);

            Grabbable best = null;
            float bestDist = float.MaxValue;
            foreach (var v in Vessel.All)
            {
                if (v.kind != Vessel.Kind.MilkJar) continue;
                if (!b.Contains(v.transform.position)) continue;

                var g = v.GetComponent<Grabbable>();
                if (g == null || !g.pickable) continue;

                float d = (v.transform.position - b.center).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = g; }
            }
            return best;
        }

        private Bounds ColliderBounds()
        {
            var cols = GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return new Bounds(transform.position, Vector3.one);
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }
    }
}

using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Печь с мехами (§6.1). Сама РАЗДАЁТ тепло сосудам на конфорке (выставляет им Heat). Жар
    /// нагнетается мехами и теперь остывает МЕДЛЕННО (держит нагрев). Для UI отдаёт значение
    /// стрелки -1..+1 (нехватка/оптимум/перегрев) — круглый датчик красный-жёлтый-белый-жёлтый-красный.
    /// </summary>
    public class Stove : MonoBehaviour
    {
        [Header("Конфорка")]
        [SerializeField] private Transform burnerPoint;
        [SerializeField] private float burnerRadius = 0.4f;
        [Tooltip("Бокс-зона конфорки (двигается/масштабируется в редакторе). Если задана — используется вместо точки с радиусом.")]
        [SerializeField] private BoxCollider burnerArea;

        [Header("Жар")]
        [SerializeField] private float pumpPerSecond = 0.5f;   // прирост жара в секунду при качании мехов
        [SerializeField] private float decay = 0.015f;      // очень медленное остывание (печь держит заряд)
        [SerializeField, Range(0f, 1f)] private float optimalMin = 0.45f;
        [SerializeField, Range(0f, 1f)] private float optimalMax = 0.78f;

        public float Heat { get; private set; }
        public float OptimalMin => optimalMin;
        public float OptimalMax => optimalMax;
        public bool IsOptimal => Heat >= optimalMin && Heat <= optimalMax;

        /// <summary>Стрелка датчика: -1 (мало огня) .. 0 (оптимум) .. +1 (перегрев).</summary>
        public float Arrow
        {
            get
            {
                float mid = (optimalMin + optimalMax) * 0.5f;
                if (Heat < mid) return -(1f - Mathf.InverseLerp(0f, mid, Heat));
                return Mathf.InverseLerp(mid, 1f, Heat);
            }
        }

        /// <summary>Качок мехов (вызывает Bellows, пока держим действие).</summary>
        public void Pump(float dt) => Heat = Mathf.Min(1f, Heat + pumpPerSecond * dt);

        private void Update()
        {
            Heat = Mathf.Max(0f, Heat - decay * Time.deltaTime);

            // Раздаём тепло сосудам на конфорке.
            for (int i = 0; i < Vessel.All.Count; i++)
            {
                var v = Vessel.All[i];
                if (OnBurner(v.transform)) v.Heat = Heat;
            }
        }

        /// <summary>Задать бокс-зону конфорки (вызывает утилита настройки сцены).</summary>
        public void SetBurnerArea(BoxCollider area) => burnerArea = area;

        public bool OnBurner(Transform t)
        {
            if (t == null) return false;

            // Бокс-зона: попадание по XZ в её границы (высота не важна).
            if (burnerArea != null)
            {
                Bounds bb = burnerArea.bounds;
                Vector3 p = t.position;
                return p.x >= bb.min.x && p.x <= bb.max.x && p.z >= bb.min.z && p.z <= bb.max.z;
            }

            if (burnerPoint == null) return false;
            Vector3 a = t.position; a.y = 0f;
            Vector3 b = burnerPoint.position; b.y = 0f;
            return Vector3.Distance(a, b) <= burnerRadius;
        }

        public Vector3 BurnerPosition =>
            burnerArea != null ? burnerArea.bounds.center :
            burnerPoint != null ? burnerPoint.position : transform.position;

        private void OnDrawGizmosSelected()
        {
            if (burnerPoint == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(burnerPoint.position, burnerRadius);
        }
    }
}

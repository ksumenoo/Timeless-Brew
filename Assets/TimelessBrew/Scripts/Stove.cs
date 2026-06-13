using UnityEngine;
using TimelessBrew.Audio;

namespace TimelessBrew
{
    /// <summary>
    /// Печь с мехами (§6.1). Сама РАЗДАЁТ тепло сосудам на конфорке (выставляет им Heat). Жар
    /// нагнетается мехами и теперь остывает МЕДЛЕННО (держит нагрев). Для UI отдаёт значение
    /// стрелки -1..+1 (нехватка/оптимум/перегрев) — круглый датчик красный-жёлтый-белый-жёлтый-красный.
    /// Перебор с раздувом (датчик полностью красный) раскаляет печь до предела: мехи перестают
    /// действовать, пока печь не остынет до рабочей зоны — порядка десяти секунд (§6.1, §4.2).
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

        [Header("Перегрев (§6.1)")]
        [Tooltip("За сколько секунд раскалённая печь остывает до верха рабочей зоны. Пока остывает — мехи не действуют.")]
        [SerializeField] private float overheatCooldown = 10f;

        public float Heat { get; private set; }
        public float OptimalMin => optimalMin;
        public float OptimalMax => optimalMax;
        public bool IsOptimal => Heat >= optimalMin && Heat <= optimalMax;

        /// <summary>Печь раскалена до предела: раздув заблокирован, идёт ускоренное остывание.</summary>
        public bool IsOverheated { get; private set; }

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

        /// <summary>Качок мехов (вызывает Bellows, пока держим действие). Раскалённая печь не раздувается.</summary>
        public void Pump(float dt)
        {
            if (IsOverheated) return;
            Heat = Mathf.Min(1f, Heat + pumpPerSecond * dt);
            if (Heat >= 1f) { IsOverheated = true; AudioManager.Instance.Play(Sfx.Overheat); }   // датчик полностью красный — печь раскалена (§6.1)
        }

        private void Update()
        {
            // Перегретая печь остывает заметно быстрее: от предела до рабочей зоны за overheatCooldown.
            float rate = IsOverheated ? (1f - optimalMax) / Mathf.Max(0.01f, overheatCooldown) : decay;
            Heat = Mathf.Max(0f, Heat - rate * Time.deltaTime);
            if (IsOverheated && Heat <= optimalMax) IsOverheated = false;

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

            // Бокс-зона: попадание по XZ + по ВЫСОТЕ — сосуд стоит на конфорке или чуть выше
            // (в руке над огнём), но не на полке под печкой (чайник снизу кипеть не должен).
            if (burnerArea != null)
            {
                Bounds bb = burnerArea.bounds;
                Vector3 p = t.position;
                float above = Mathf.Max(bb.size.x, bb.size.z);   // допуск вверх — порядка размера конфорки
                bool xz = p.x >= bb.min.x && p.x <= bb.max.x && p.z >= bb.min.z && p.z <= bb.max.z;
                bool y = p.y >= bb.min.y - 0.02f && p.y <= bb.max.y + above;
                return xz && y;
            }

            if (burnerPoint == null) return false;
            Vector3 a = t.position; a.y = 0f;
            Vector3 b = burnerPoint.position; b.y = 0f;
            return Vector3.Distance(a, b) <= burnerRadius
                && Mathf.Abs(t.position.y - burnerPoint.position.y) <= burnerRadius * 2f;
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

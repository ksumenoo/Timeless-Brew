using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Раковина с краном (§3.3, §6.7). Две функции:
    ///   • Просеивание: игрок держит дуршлаг с зёрнами над раковиной — шелуха улетает (Shake).
    ///   • Кран холодной воды: клик пустой рукой по раковине наливает холодную воду в турку,
    ///     стоящую под краном (в радиусе Fill Point), если у неё есть кофе и нет воды.
    /// Раковина непереносима. Кран читается через DirectHoldTarget (нажатие пустой рукой).
    /// </summary>
    public class Sink : InteractableItem
    {
        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [SerializeField] private ItemStateSO state; // SinkState: pickable 0, canReceive 1

        [Header("Кран холодной воды")]
        [SerializeField] private Transform fillPoint;     // где стоит турка под краном
        [SerializeField] private float fillRange = 0.5f;

        private bool _tapTargetPrev;

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (state != null) SetState(state);
        }

        // Дуршлаг потряхивается над раковиной — каждый кадр при удержании ЛКМ.
        public override bool TryReceive(InteractableItem source)
        {
            Colander colander = source as Colander;
            if (colander != null && colander.HasBeans)
            {
                colander.Shake(Time.deltaTime);
                return true;
            }
            return false;
        }

        private void Update()
        {
            // Кран: момент «нажали пустой рукой на раковину» → налить холодную в турку под краном.
            bool tapTarget = cursorHandler != null
                             && cursorHandler.Carried == null
                             && cursorHandler.DirectHoldTarget == this;

            if (tapTarget && !_tapTargetPrev)
            {
                Cezve cz = FindCezveUnderTap();
                if (cz != null) cz.AddColdWater();
            }
            _tapTargetPrev = tapTarget;
        }

        private Cezve FindCezveUnderTap()
        {
            Vector3 origin = fillPoint != null ? fillPoint.position : transform.position;
            origin.y = 0f;

            Cezve best = null;
            float bestDist = fillRange;
            Cezve[] all = FindObjectsOfType<Cezve>();
            for (int i = 0; i < all.Length; i++)
            {
                Vector3 p = all[i].transform.position;
                p.y = 0f;
                float d = Vector3.Distance(origin, p);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = all[i];
                }
            }
            return best;
        }
    }
}

using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Сахарница со щипцами (§6.9). Сама сахарница НЕ берётся в руку: клик по ней достаёт ЩИПЦЫ
    /// с кубиком (меш «Plane» внутри модели следует за курсором), клик щипцами по чашке/турке —
    /// кубик добавлен, щипцы возвращаются на место. Клик мимо — тоже возврат. Логика руки — в Hand.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class SugarBowl : MonoBehaviour
    {
        [Tooltip("Меш щипцов. Пусто — найдётся дочерний объект с именем, содержащим 'plane'.")]
        [SerializeField] private GameObject tongs;

        private Vector3 _tongsLocalPos;
        private Quaternion _tongsLocalRot;

        /// <summary>Щипцы сейчас в руке (следуют за курсором).</summary>
        public bool TongsOut { get; private set; }

        private void Start()
        {
            GetComponent<Grabbable>().pickable = false;   // сахарница стоит на месте

            if (tongs == null) tongs = FindTongsChild();
            if (tongs != null)
            {
                _tongsLocalPos = tongs.transform.localPosition;
                _tongsLocalRot = tongs.transform.localRotation;
                tongs.SetActive(false);
            }
        }

        /// <summary>Достать щипцы с кубиком (клик по сахарнице).</summary>
        public void TakeTongs()
        {
            TongsOut = true;
            if (tongs != null) tongs.SetActive(true);
        }

        /// <summary>Вести щипцы за курсором (зовёт Hand в LateUpdate).</summary>
        public void MoveTongs(Vector3 worldPos)
        {
            if (tongs != null) tongs.transform.position = worldPos;
        }

        /// <summary>Вернуть щипцы в сахарницу (кубик положен или действие отменено).</summary>
        public void ReturnTongs()
        {
            TongsOut = false;
            if (tongs == null) return;
            tongs.SetActive(false);
            tongs.transform.localPosition = _tongsLocalPos;
            tongs.transform.localRotation = _tongsLocalRot;
        }

        private GameObject FindTongsChild()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name.ToLowerInvariant().Contains("plane"))
                    return t.gameObject;
            return null;
        }
    }
}

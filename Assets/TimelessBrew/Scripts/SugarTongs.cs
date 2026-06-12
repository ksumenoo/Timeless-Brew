using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Щипцы сахарницы (§6.9). В модели сахарницы щипцы — дочерний меш «Plane». Поведение:
    /// взял сахарницу в руку → щипцы появляются (схватили кубик); положил сахар в чашку/турку →
    /// щипцы исчезают; взял снова / поставил сахарницу — состояние сбрасывается.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class SugarTongs : MonoBehaviour
    {
        [Tooltip("Меш щипцов. Пусто — найдётся дочерний объект с именем, содержащим 'plane'.")]
        [SerializeField] private GameObject tongs;

        private Grabbable _grab;
        private Adder _adder;
        private Hand _hand;
        private bool _appliedWhileCarried;   // сахар уже положен — щипцы спрятаны до перевзятия

        private void Start()
        {
            _grab = GetComponent<Grabbable>();
            _adder = GetComponent<Adder>();
            _hand = FindFirstObjectByType<Hand>();

            if (tongs == null) tongs = FindTongsChild();
            if (_adder != null) _adder.OnApplied += HandleApplied;

            SetTongs(false);
        }

        private void OnDestroy()
        {
            if (_adder != null) _adder.OnApplied -= HandleApplied;
        }

        private void HandleApplied() => _appliedWhileCarried = true;

        private void Update()
        {
            if (_hand == null) { _hand = FindFirstObjectByType<Hand>(); return; }

            bool carried = _hand.Carried == _grab;
            if (!carried) _appliedWhileCarried = false;   // поставили — следующее взятие снова покажет щипцы

            SetTongs(carried && !_appliedWhileCarried);
        }

        private void SetTongs(bool on)
        {
            if (tongs != null && tongs.activeSelf != on) tongs.SetActive(on);
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

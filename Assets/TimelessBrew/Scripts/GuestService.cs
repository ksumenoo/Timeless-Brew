using UnityEngine;
using UnityEngine.UI;
using TimelessBrew.UI;

namespace TimelessBrew
{
    /// <summary>
    /// Вызов гостей из книги (§4.4). Гость появляется ПЕРЕД столом (со стороны гостей), а его
    /// заказ показывается баннером сверху, чтобы было видно, что готовить. Один гость за раз.
    /// </summary>
    public class GuestService : MonoBehaviour
    {
        public Transform counterPoint;

        public GuestSO Current { get; private set; }
        public OrderSpec CurrentOrder { get; private set; }

        private GameObject _guest;
        private RectTransform _bannerRoot;
        private Text _banner;

        private void Start()
        {
            var canvas = UguiUtil.EnsureCanvas();
            var bg = UguiUtil.Rect(canvas.transform, "OrderBanner", new Color(0f, 0f, 0f, 0.6f));
            _bannerRoot = (RectTransform)bg.transform;
            // Растянуть по ширине (88% экрана), чтобы длинный заказ помещался целиком.
            _bannerRoot.Anchor(new Vector2(0.06f, 1f), new Vector2(0.94f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(0f, 56f));
            _banner = UguiUtil.Label(_bannerRoot, "Text", 18, TextAnchor.MiddleCenter);
            ((RectTransform)_banner.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 0f));
            _bannerRoot.gameObject.SetActive(false);
        }

        public void Call(GuestSO g, bool morning)
        {
            if (g == null) return;
            Current = g;
            CurrentOrder = morning ? g.morning : g.evening;

            if (_guest != null) Destroy(_guest);
            Vector3 pos = counterPoint != null ? counterPoint.position : transform.position;
            _guest = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _guest.name = "Guest_" + g.displayName;
            _guest.transform.position = pos;

            var r = _guest.GetComponent<Renderer>();
            if (r != null)
            {
                var mpb = new MaterialPropertyBlock();
                var col = new Color(0.82f, 0.7f, 0.52f);
                mpb.SetColor("_BaseColor", col); mpb.SetColor("_Color", col);
                r.SetPropertyBlock(mpb);
            }

            Message($"<b>{g.displayName}</b> ({(morning ? "утро" : "вечер")}):  " + CurrentOrder.Summary().Replace("\n", "  ·  "));
        }

        /// <summary>Показать строку в баннере (подсказка/результат).</summary>
        public void Message(string text)
        {
            if (_bannerRoot == null) return;
            _bannerRoot.gameObject.SetActive(true);
            _banner.text = text;
        }

        /// <summary>Напиток подан: убрать гостя и показать результат с оценкой.</summary>
        public void Served(string resultText)
        {
            if (_guest != null) Destroy(_guest);
            Current = null;
            CurrentOrder = null;
            Message(resultText);
        }
    }
}

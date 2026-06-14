using UnityEngine;
using UnityEngine.UI;
using TimelessBrew.UI;

namespace TimelessBrew
{
    /// <summary>
    /// Чек заказа (§3.3, §6.15): бумажка с именем гостя и составом напитка. Появляется на стойке,
    /// когда гость сделал заказ; берётся рукой и вешается на чекхолдер (zakazi), как в Galaxy Burger.
    /// Заменяет некрасивую надпись-баннер с заказом. Рисуется мировой Canvas-плашкой, повёрнут к камере.
    /// </summary>
    public class Check : MonoBehaviour
    {
        private static readonly Color Paper = new(0.95f, 0.91f, 0.78f, 1f);
        private static readonly Color Gold = new(0.7f, 0.5f, 0.18f, 1f);
        private static readonly Color Ink = new(0.15f, 0.1f, 0.05f, 1f);

        private const float PxW = 360f, PxH = 470f, WorldScale = 0.0007f;   // ~0.25 × 0.33 м

        public enum Status { New, InProgress, Ready }

        public bool Pinned { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public Status State { get; private set; } = Status.New;
        private Chekholder _holder;
        private Text _statusLabel;

        public static string StatusRu(Status s) => s switch
        { Status.InProgress => "В работе", Status.Ready => "Готов к выдаче", _ => "Новый" };
        public static Color StatusColor(Status s) => s switch
        { Status.InProgress => new Color(0.85f, 0.6f, 0.2f), Status.Ready => new Color(0.3f, 0.65f, 0.3f), _ => new Color(0.5f, 0.5f, 0.5f) };

        /// <summary>Чек повесили на чекхолдер (зовёт Chekholder.Pin).</summary>
        public void OnPinned(Chekholder h) { Pinned = true; _holder = h; }

        /// <summary>Клик по приколотому чеку — открыть/закрыть крупную читалку слева.</summary>
        public void ToggleReader() => UI.CheckReaderUI.Toggle(this);

        /// <summary>Переключить статус: Новый → В работе → Готов → Новый.</summary>
        public void CycleStatus()
        {
            State = (Status)(((int)State + 1) % 3);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null) return;
            _statusLabel.text = StatusRu(State);
            _statusLabel.color = StatusColor(State);
        }

        private void OnDestroy()
        {
            if (_holder != null) _holder.Unpin(this);
            UI.CheckReaderUI.HideIf(this);
        }

        /// <summary>Собрать чек целиком: плашка + текст + коллайдер для захвата + биллборд.</summary>
        public static GameObject Create(string title, string body, Vector3 pos, int itemLayer)
        {
            var root = new GameObject("Check (" + title + ")");
            root.transform.position = pos;
            root.layer = itemLayer;
            var check = root.AddComponent<Check>();
            check.Title = title; check.Body = body;
            root.AddComponent<Billboard>();

            var grab = root.AddComponent<Grabbable>();
            grab.displayName = "Чек: " + title;

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(PxW * WorldScale, PxH * WorldScale, 0.02f);

            // Мировой Canvas с бумажкой.
            var canvasGo = new GameObject("CheckCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.layer = itemLayer;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var crt = canvas.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(PxW, PxH);
            canvasGo.transform.localScale = Vector3.one * WorldScale;
            canvasGo.transform.localPosition = Vector3.zero;

            var paper = UguiUtil.Rect(crt, "Paper", Paper);
            ((RectTransform)paper.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var stripe = UguiUtil.Rect(crt, "Stripe", Gold);
            ((RectTransform)stripe.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 14f));

            var head = UguiUtil.Label(crt, "Title", 34, TextAnchor.UpperCenter);
            head.fontStyle = FontStyle.Bold; head.color = Ink;
            ((RectTransform)head.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-24f, 50f));
            head.text = title;

            var text = UguiUtil.Label(crt, "Body", 24, TextAnchor.UpperLeft);
            text.color = Ink; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)text.transform).Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(-36f, -118f));
            text.text = body;

            // Статус заказа — снизу чека, цветом.
            var status = UguiUtil.Label(crt, "Status", 26, TextAnchor.LowerCenter);
            status.fontStyle = FontStyle.Bold;
            ((RectTransform)status.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-20f, 36f));
            check._statusLabel = status;
            check.RefreshStatus();

            return root;
        }
    }
}

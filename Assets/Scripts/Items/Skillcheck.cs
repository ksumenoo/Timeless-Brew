using UnityEngine;
using TimelessBrew.Core;

namespace TimelessBrew.Items
{
    // Скилчек-полоска (§4.2 v4.3): по шкале бегает указатель, на шкале — зелёная зона.
    // Запускается во время удержания ЛКМ (засыпка зёрен); результат фиксируется в момент
    // ОТПУСКАНИЯ ЛКМ — где оказался указатель. Качество 0..1: 1 — в центре зоны, 0 — мимо.
    //
    // Кто запустил скилчек (RoastingPan), потом сам забирает результат: HasResult + Result.
    // Один на сцену (Instance). Если скилчека в сцене нет — система просто не используется.
    public class Skillcheck : MonoBehaviour
    {
        public static Skillcheck Instance;

        [Header("Параметры")]
        [SerializeField] private float sweepSpeed = 1.1f;            // циклов/сек туда-обратно
        [SerializeField, Range(0f, 1f)] private float zoneCenter = 0.7f;
        [SerializeField, Range(0.02f, 0.5f)] private float zoneHalfWidth = 0.12f;
        [SerializeField] private float timeout = 4f;                // фиксируем, если не отпустили

        public bool Active { get; private set; }
        public bool HasResult { get; private set; }   // готов ли свежий результат
        public float Result { get; private set; }     // 0..1
        public float Pointer { get; private set; }

        private float _dir = 1f;
        private float _elapsed;
        private bool _wasHeld;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        // Запустить скилчек. Когда игрок отпустит ЛКМ — будет HasResult = true и Result.
        public void Begin()
        {
            Active = true;
            HasResult = false;
            Pointer = 0f;
            _dir = 1f;
            _elapsed = 0f;
            _wasHeld = GameInput.Instance != null && GameInput.Instance.InteractHeld;
        }

        private void Update()
        {
            if (!Active) return;

            _elapsed += Time.deltaTime;
            Pointer += _dir * sweepSpeed * Time.deltaTime;
            if (Pointer >= 1f) { Pointer = 1f; _dir = -1f; }
            else if (Pointer <= 0f) { Pointer = 0f; _dir = 1f; }

            bool held = GameInput.Instance != null && GameInput.Instance.InteractHeld;
            if ((_wasHeld && !held) || _elapsed >= timeout)
                Resolve();
            _wasHeld = held;
        }

        private void Resolve()
        {
            Active = false;

            float dist = Mathf.Abs(Pointer - zoneCenter);
            float quality = 1f - dist / zoneHalfWidth;
            if (quality < 0f) quality = 0f;
            if (quality > 1f) quality = 1f;

            Result = quality;
            HasResult = true;
            Debug.Log("[Skillcheck] Результат: " + Result.ToString("0.00"), this);
        }

        private void OnGUI()
        {
            if (!Active) return;

            float w = 360f, h = 28f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.62f;

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.85f, 0.4f, 0.9f);   // зелёная зона
            float zx = x + (zoneCenter - zoneHalfWidth) * w;
            float zw = (2f * zoneHalfWidth) * w;
            GUI.DrawTexture(new Rect(zx, y, zw, h), Texture2D.whiteTexture);

            GUI.color = Color.white;                          // указатель
            GUI.DrawTexture(new Rect(x + Pointer * w - 2f, y - 4f, 4f, h + 8f), Texture2D.whiteTexture);
            GUI.Label(new Rect(x, y - 24f, w, 22f), "Скилчек: отпусти ЛКМ в зелёной зоне");
        }
    }
}

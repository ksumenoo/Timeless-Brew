using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>Нижняя подсказка управления + заголовок прототипа.</summary>
    public class HudUI : MonoBehaviour
    {
        private void Start()
        {
            var canvas = UguiUtil.EnsureCanvas();

            var hint = UguiUtil.Label(canvas.transform, "Controls", 16, TextAnchor.LowerLeft);
            ((RectTransform)hint.transform).Anchor(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14f, 12f), new Vector2(1200f, 26f));
            hint.text = "ЛКМ — взять / поставить / лить · удержание по печи и ручке — крутить · Q/E — поворот камеры · Tab — книга гостей";

            var title = UguiUtil.Label(canvas.transform, "Title", 18, TextAnchor.UpperRight);
            ((RectTransform)title.transform).Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14f, -12f), new Vector2(420f, 26f));
            title.text = "<b>Timeless Brew</b> — кухня (прототип)";
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>Фабрика простых UGUI-элементов (встроенный шрифт, белая текстура) — без TMP/спрайтов.</summary>
    public static class UguiUtil
    {
        private const string CanvasName = "Cafe HUD Canvas";
        private static Font _font;
        private static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas EnsureCanvas()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == CanvasName) return c;

            var go = new GameObject(CanvasName);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RawImage Rect(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.texture = Texture2D.whiteTexture;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }
    }
}

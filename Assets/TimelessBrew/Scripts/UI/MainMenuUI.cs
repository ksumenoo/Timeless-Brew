using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TimelessBrew.Audio;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Главное меню: фон — рисунок «НачальнаяЛокация» во весь экран, сверху деревянная табличка-заголовок
    /// и деревянные кнопки с золотой каймой (подсветка при наведении). Кнопки как в макете:
    /// Новая игра / Продолжить / Древо времени / Блокнот рецептов / Настройки / Выход.
    /// Клики — вручную через GameInput. Сцена строится <c>TimelessBrew → Build Main Menu</c>.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private string kitchenScene = "Kitchen";

        private class Btn { public RectTransform rect; public RawImage frame, wood; public Action act; }
        private readonly List<Btn> _buttons = new();
        private bool _subscribed;
        private SettingsGearUI _gear;
        private Text _note;
        private float _noteHideAt = -1f;

        private static readonly Color Frame = new(0.72f, 0.55f, 0.25f);
        private static readonly Color FrameHi = new(0.95f, 0.78f, 0.35f);
        private static readonly Color Wood = new(0.34f, 0.23f, 0.13f);
        private static readonly Color WoodHi = new(0.47f, 0.33f, 0.19f);
        private static readonly Color Bevel = new(0.46f, 0.31f, 0.18f);
        private static readonly Color Ink = new(0.97f, 0.91f, 0.75f);

        private void Start()
        {
            _gear = FindFirstObjectByType<SettingsGearUI>();
            BuildUi();
            AudioManager.Instance.PlayMusic(Music.Menu);
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                _subscribed = true;
            }

            // Подсветка кнопки под курсором.
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var b in _buttons)
            {
                bool over = RectTransformUtility.RectangleContainsScreenPoint(b.rect, p, null);
                b.frame.color = over ? FrameHi : Frame;
                b.wood.color = over ? WoodHi : Wood;
            }

            if (_noteHideAt > 0f && Time.unscaledTime >= _noteHideAt) { _noteHideAt = -1f; if (_note != null) _note.text = ""; }
        }

        private void OnDestroy()
        {
            if (GameInput.Instance != null) GameInput.Instance.OnActionPressed -= OnPress;
        }

        private void OnPress()
        {
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var b in _buttons)
                if (RectTransformUtility.RectangleContainsScreenPoint(b.rect, p, null)) { b.act(); return; }
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            var go = new GameObject("Main Menu Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Фон — рисунок локации во весь экран.
            var bg = UguiUtil.Rect(go.transform, "Background", Color.white);
            ((RectTransform)bg.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bgTex = Resources.Load<Texture2D>("Backgrounds/menu_bg");
            if (bgTex != null) bg.texture = bgTex;
            else bg.color = new Color(0.2f, 0.15f, 0.1f);   // фолбэк, если картинки нет

            BuildTitle(go.transform);

            // Кнопки — столбиком по центру-справа (как в макете, чтобы не закрывать бариста слева).
            string[,] items =
            {
                { "Новая игра", "new" }, { "Продолжить", "continue" }, { "Древо времени", "soon" },
                { "Блокнот рецептов", "soon" }, { "Настройки", "settings" }, { "Выход", "quit" },
            };
            float y = 90f;   // от центра вверх; пойдём вниз
            for (int i = 0; i < items.GetLength(0); i++)
            {
                string tag = items[i, 1];
                Action act = tag switch
                {
                    "new" => () => StartGame(true),         // новая игра — с обучением (если включено в настройках)
                    "continue" => () => StartGame(false),   // продолжить — без обучения
                    "settings" => OpenSettings,
                    "quit" => Quit,
                    _ => () => Note("В разработке"),
                };
                MakeButton(go.transform, items[i, 0], new Vector2(-540f, y - i * 76f), act);
            }

            _note = UguiUtil.Label(go.transform, "Note", 24, TextAnchor.LowerCenter);
            _note.color = Ink;
            ((RectTransform)_note.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 40f));
        }

        private void BuildTitle(Transform parent)
        {
            // Деревянная табличка с золотой каймой по центру сверху.
            var frame = UguiUtil.Rect(parent, "TitleFrame", Frame);
            var frt = (RectTransform)frame.transform;
            frt.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(640f, 150f));
            var wood = UguiUtil.Rect(frt, "Wood", Wood);
            ((RectTransform)wood.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            var t = UguiUtil.Label((RectTransform)wood.transform, "T", 72, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold; t.color = new Color(0.93f, 0.78f, 0.4f);
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.text = "TIMELESS BREW";
        }

        private void MakeButton(Transform parent, string label, Vector2 pos, Action act)
        {
            var frame = UguiUtil.Rect(parent, "Btn_" + label, Frame);
            var rt = (RectTransform)frame.transform;
            rt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(420f, 66f));

            var wood = UguiUtil.Rect(rt, "Wood", Wood);
            ((RectTransform)wood.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -8f));

            // Тонкая светлая полоска сверху — фактура «фаски».
            var bevel = UguiUtil.Rect((RectTransform)wood.transform, "Bevel", Bevel);
            ((RectTransform)bevel.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-10f, 14f));

            var t = UguiUtil.Label((RectTransform)wood.transform, "L", 30, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold; t.color = Ink;
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.text = label;

            _buttons.Add(new Btn { rect = rt, frame = frame, wood = wood, act = act });
        }

        private void Note(string text)
        {
            if (_note == null) return;
            _note.text = text;
            _noteHideAt = Time.unscaledTime + 2f;
        }

        // ===================== ДЕЙСТВИЯ =====================

        private void StartGame(bool newGame)
        {
            UiSettings.StartNewGame = newGame;   // обучение запустится только при «Новой игре» (и если включено)
            if (!string.IsNullOrEmpty(kitchenScene)) SceneManager.LoadScene(kitchenScene);
        }

        private void OpenSettings()
        {
            if (_gear == null) _gear = FindFirstObjectByType<SettingsGearUI>();
            if (_gear != null) _gear.Open();
        }

        private void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}

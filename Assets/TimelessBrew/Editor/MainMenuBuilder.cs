using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TimelessBrew;
using TimelessBrew.UI;

namespace TimelessBrew.EditorTools
{
    /// <summary>
    /// Сборка сцены ГЛАВНОГО МЕНЮ (заставка). Фон — рисунок «НачальнаяЛокация» (Resources/Backgrounds/
    /// menu_bg) во весь экран; поверх — табличка-заголовок и деревянные кнопки (см. <see cref="MainMenuUI"/>).
    /// Обе сцены добавляются в Build Settings (меню — первой). Меню: TimelessBrew → Build Main Menu.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string MenuScenePath = "Assets/TimelessBrew/Scenes/MainMenu.unity";
        private const string KitchenScenePath = "Assets/TimelessBrew/Scenes/Kitchen.unity";

        [MenuItem("TimelessBrew/Build Main Menu", priority = 3)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Чистый лист: сносим всё постороннее (чтобы в меню не утекли кухонные UI/камера/звук).
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

            // Камера (для AudioListener; UI рисуется в Overlay поверх). Фон даёт картинка, не камера.
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.06f);

            var sys = new GameObject("— Menu —");
            sys.AddComponent<GameInput>();
            sys.AddComponent<SettingsGearUI>();   // панель настроек (открывается кнопкой «Настройки»)
            sys.AddComponent<MainMenuUI>();

            Directory.CreateDirectory(Path.GetDirectoryName(MenuScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            AddScenesToBuild();

            Debug.Log("[TimelessBrew] Меню собрано: " + MenuScenePath + " (фон — НачальнаяЛокация, кнопки и заголовок поверх).");
        }

        private static void AddScenesToBuild()
        {
            var list = new List<EditorBuildSettingsScene>();
            list.Add(new EditorBuildSettingsScene(MenuScenePath, true));   // меню первой
            if (File.Exists(KitchenScenePath))
                list.Add(new EditorBuildSettingsScene(KitchenScenePath, true));
            else
                Debug.LogWarning("[TimelessBrew] Kitchen.unity не найдена — сначала собери кухню (Build Kitchen Scene), иначе «Начать игру» не загрузит сцену.");
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == MenuScenePath || s.path == KitchenScenePath) continue;
                list.Add(s);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}

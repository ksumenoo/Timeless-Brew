using System;
using UnityEngine;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Глобальные настройки интерфейса и сложности (сохраняются в PlayerPrefs). Шестерёнка
    /// (<see cref="SettingsGearUI"/>) их меняет, а UI-компоненты подписываются на <see cref="Changed"/>
    /// или сами сверяются с <see cref="UiMode"/>:
    ///   • Normal — «красивый» UI (шкалы у предметов, аккуратные подписи при наведении) — главный;
    ///   • Testing — прежний текстовый UI (панели состава, подсказка у курсора).
    /// Громкости живут в <see cref="Audio.AudioManager"/>; сложность пока — задел на будущее.
    /// </summary>
    public static class UiSettings
    {
        public enum Mode { Normal, Testing }            // Normal первым — значение по умолчанию
        public enum Difficulty { Easy, Normal, Hard }

        public static Mode UiMode { get; private set; }
        public static Difficulty Diff { get; private set; }

        public static bool TutorialEnabled { get; private set; }   // тумблер «обучение в новой игре»
        public static bool StartNewGame { get; set; } = true;      // в кухню зашли через «Новая игра» (не «Продолжить»)
        public static bool ShouldRunTutorial => TutorialEnabled && StartNewGame;

        /// <summary>Сработало изменение любой настройки UI/сложности.</summary>
        public static event Action Changed;

        static UiSettings()
        {
            UiMode = (Mode)PlayerPrefs.GetInt("tb_ui_mode", (int)Mode.Normal);
            Diff = (Difficulty)PlayerPrefs.GetInt("tb_difficulty", (int)Difficulty.Normal);
            TutorialEnabled = PlayerPrefs.GetInt("tb_tutorial_enabled", 1) == 1;
        }

        public static bool IsNormal => UiMode == Mode.Normal;
        public static bool IsTesting => UiMode == Mode.Testing;

        public static void SetMode(Mode m)
        {
            if (m == UiMode) return;
            UiMode = m;
            PlayerPrefs.SetInt("tb_ui_mode", (int)m);
            Changed?.Invoke();
        }

        public static void SetDifficulty(Difficulty d)
        {
            if (d == Diff) return;
            Diff = d;
            PlayerPrefs.SetInt("tb_difficulty", (int)d);
            Changed?.Invoke();
        }

        /// <summary>Тумблер обучения (показывать ли его при «Новой игре»). Сохраняется в PlayerPrefs.</summary>
        public static void SetTutorialEnabled(bool on)
        {
            TutorialEnabled = on;
            PlayerPrefs.SetInt("tb_tutorial_enabled", on ? 1 : 0);
            Changed?.Invoke();
        }
    }
}

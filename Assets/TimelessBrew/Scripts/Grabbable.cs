using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Предмет, который можно взять рукой (§4.1). Пивот — у основания (предмет ставится на стол
    /// дном, а не серединой). Визуал и коллайдер — на дочернем объекте; этот компонент на родителе.
    /// </summary>
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("Имя для подсказки у курсора, напр. 'Турка'.")]
        public string displayName;

        [Tooltip("Можно ли поднять (станции — печь, кофемолка, мусорка — false).")]
        public bool pickable = true;

        [Tooltip("Группа стопки: предметы одной группы можно ставить друг на друга (напр. 'spice', 'tray'). Пусто — не стопкуется.")]
        public string stackGroup = "";

        /// <summary>Сосуд на этом предмете (если есть): чашка, турка, чайник и т.п.</summary>
        public Vessel Vessel { get; private set; }

        private void Awake() => Vessel = GetComponent<Vessel>();
    }
}

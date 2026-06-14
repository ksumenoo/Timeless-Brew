using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Предмет, который можно взять рукой (§4.1). Пивот — у основания (предмет ставится на стол
    /// дном, а не серединой). Визуал и коллайдер — на дочернем объекте; этот компонент на родителе.
    /// Помнит «домашнее» место со старта сцены: туда предмет возвращается, если игрок попытается
    /// выкинуть его в мусорку целиком (защита от дураков, §6.18).
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

        // «Дом» предмета — родитель и положение на старте сцены.
        private Transform _homeParent;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        private void Awake()
        {
            Vessel = GetComponent<Vessel>();
            _homeParent = transform.parent;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        /// <summary>Вернуть предмет на место старта сцены (попытка выкинуть его в мусорку).</summary>
        public void ReturnHome()
        {
            transform.SetParent(_homeParent, true);
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Общий «привратник» модального UI. Решает две проблемы разом:
    ///   • несколько модалок (дневник + настройки) не дерутся за <c>Hand.enabled</c> — рука включена,
    ///     только когда открытых окон НОЛЬ (<see cref="AnyOpen"/>), через счётчик Push/Pop;
    ///   • клики по всегда-видимым интерактивным виджетам HUD (шестерёнка) не «протекают» в кухню:
    ///     рука первой строкой в OnPress спрашивает <see cref="BlocksHand"/> и выходит.
    /// </summary>
    public static class ModalGuard
    {
        public static int OpenCount { get; private set; }
        public static bool AnyOpen => OpenCount > 0;

        // Чистый старт на каждой загрузке сцены: счётчик и блокеры не тащатся из прошлой сцены.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneReset()
        {
            SceneManager.sceneLoaded += (_, __) => { OpenCount = 0; _blockers.Clear(); };
        }

        public static void Push() => OpenCount++;
        public static void Pop() => OpenCount = Mathf.Max(0, OpenCount - 1);

        // Зоны интерактивного HUD, по которым клик не должен уходить в руку (напр. кнопка-шестерёнка).
        private static readonly List<RectTransform> _blockers = new();

        public static void RegisterBlocker(RectTransform rt)
        {
            if (rt != null && !_blockers.Contains(rt)) _blockers.Add(rt);
        }

        public static bool PointerOverBlocker(Vector2 screenPoint)
        {
            for (int i = _blockers.Count - 1; i >= 0; i--)
            {
                var rt = _blockers[i];
                if (rt == null) { _blockers.RemoveAt(i); continue; }
                if (rt.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null))
                    return true;
            }
            return false;
        }

        /// <summary>Должен ли клик в этой точке быть «съеден» интерфейсом, а не рукой.</summary>
        public static bool BlocksHand(Vector2 screenPoint) => AnyOpen || PointerOverBlocker(screenPoint);
    }
}

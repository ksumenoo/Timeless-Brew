using UnityEngine;

namespace TimelessBrew.Data
{
    /// <summary>
    /// A single state of an interactable item, defined as data rather than code (см. §8.1).
    /// Example: банка зёрен может иметь состояния "Полная", "Наполовину", "Пустая".
    /// Add new states by creating new .asset instances in the editor — no recompilation.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemState", menuName = "TimelessBrew/Item State", order = 0)]
    public class ItemStateSO : ScriptableObject
    {
        [Tooltip("Человекочитаемый идентификатор состояния, напр. 'JarFull', 'PanRoasting'.")]
        public string stateId;

        [Tooltip("Подсказка для игрока при наведении на предмет в этом состоянии (опционально).")]
        [TextArea] public string hoverHint;

        [Header("Визуал")]
        [Tooltip("Материал/текстура для этого состояния. Может быть пустым, если визуал меняется иначе (shader, fill-level).")]
        public Material visualOverride;

        [Tooltip("Заполненность 0..1 для shader fill-level (банки, турка, чайник). -1 = не применять.")]
        [Range(-1f, 1f)] public float fillLevel = -1f;

        [Header("Поведение")]
        [Tooltip("Можно ли взять предмет на курсор в этом состоянии. Напр. горячую сковороду можно, пустую банку — может, нельзя.")]
        public bool pickable = true;

        [Tooltip("Может ли предмет ПРИНИМАТЬ содержимое в этом состоянии (сковорода принимает зёрна, турка — кофе).")]
        public bool canReceive = false;

        [Tooltip("Может ли предмет ОТДАВАТЬ содержимое при наведении на цель (банка высыпает зёрна, турка льёт кофе).")]
        public bool canPour = false;
    }
}

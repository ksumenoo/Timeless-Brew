using UnityEngine;
using TimelessBrew.Audio;

namespace TimelessBrew
{
    /// <summary>Ручка кофемолки: мелет удержанием пустой руки.</summary>
    public class GrinderCrank : MonoBehaviour, IHandHold
    {
        [SerializeField] private Grinder grinder;
        public void SetGrinder(Grinder g) => grinder = g;

        public void Hold(float dt)
        {
            if (grinder == null) return;
            grinder.Grind(dt);
            // Звук помола, пока крутим: с зёрнами — жернова, вхолостую — холостой ход.
            // Раздельные каналы: при переходе «есть зёрна ↔ пусто» один затухает, другой нарастает
            // (кроссфейд), а не подменяется клип у играющего источника — без щелчка/перезапуска.
            AudioManager.Instance.Loop(grinder.HasGrounds ? "grind" : "grind_empty",
                                       grinder.HasGrounds ? Sfx.Grind : Sfx.GrindEmpty);
        }
    }
}

using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Ручка кофемолки: мелет удержанием пустой руки.</summary>
    public class GrinderCrank : MonoBehaviour, IHandHold
    {
        [SerializeField] private Grinder grinder;
        public void SetGrinder(Grinder g) => grinder = g;
        public void Hold(float dt) { if (grinder != null) grinder.Grind(dt); }
    }
}

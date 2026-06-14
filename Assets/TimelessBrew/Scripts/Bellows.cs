using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Мехи: качаются удержанием пустой руки (§6.1). Поднимают жар печи.</summary>
    public class Bellows : MonoBehaviour, IHandHold
    {
        [SerializeField] private Stove stove;
        public void SetStove(Stove s) => stove = s;
        public void Hold(float dt) { if (stove != null) stove.Pump(dt); }
    }
}

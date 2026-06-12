using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Длинная ложка (§6.8): удержанием над туркой усаживает пенку (логика — в Vessel.SettleFoam).
    /// Ориентация: на столе ЛЕЖИТ (исходный поворот из сцены), в руке — ВЕРТИКАЛЬНО
    /// (самая длинная ось разворачивается вверх). Hand зовёт OnPicked/OnPlaced.
    /// </summary>
    public class Spoon : MonoBehaviour
    {
        private Quaternion _tableRot;   // как лежит на столе
        private Quaternion _handRot;    // как стоит в руке

        private void Start()
        {
            _tableRot = transform.rotation;
            _handRot = ComputeHandRotation();
        }

        /// <summary>Поворот, при котором самая длинная ось модели смотрит вверх.</summary>
        private Quaternion ComputeHandRotation()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return _tableRot;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 s = b.size;
            Vector3 longDir =
                (s.x >= s.y && s.x >= s.z) ? Vector3.right :
                (s.y >= s.z) ? Vector3.up : Vector3.forward;

            if (longDir == Vector3.up) return _tableRot;   // уже стоит
            return Quaternion.FromToRotation(longDir, Vector3.up) * _tableRot;
        }

        public void OnPicked() => transform.rotation = _handRot;
        public void OnPlaced() => transform.rotation = _tableRot;
    }
}

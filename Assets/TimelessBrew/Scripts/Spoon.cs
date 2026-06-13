using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Длинная ложка (§6.8): удержанием над туркой усаживает пенку (логика — в Vessel.SettleFoam).
    /// Ориентация: на столе ЛЕЖИТ (исходный поворот из сцены), в руке — ВЕРТИКАЛЬНО ручкой вверх.
    /// Пивот модели — в центре, поэтому после каждого поворота визуал-дети сдвигаются так, чтобы
    /// низ баундов совпал с пивотом: ложка не проваливается сквозь стол. Hand зовёт OnPicked/OnPlaced.
    /// </summary>
    public class Spoon : MonoBehaviour
    {
        private Quaternion _tableRot;   // как лежит на столе
        private Quaternion _handRot;    // как стоит в руке

        private void Start()
        {
            _tableRot = transform.rotation;
            _handRot = ComputeHandRotation();
            AlignBottomToPivot();
        }

        /// <summary>Поворот «в руке»: самая длинная ось — вверх, плюс разворот на 180° ручкой вверх.</summary>
        private Quaternion ComputeHandRotation()
        {
            Bounds b = RendererBounds();
            if (b.size == Vector3.zero) return _tableRot;

            Vector3 s = b.size;
            Vector3 longDir =
                (s.x >= s.y && s.x >= s.z) ? Vector3.right :
                (s.y >= s.z) ? Vector3.up : Vector3.forward;

            Quaternion upright = longDir == Vector3.up
                ? _tableRot
                : Quaternion.FromToRotation(longDir, Vector3.up) * _tableRot;

            // Модель экспортирована черпаком вверх — переворачиваем: ручка должна смотреть вверх.
            return Quaternion.AngleAxis(180f, Vector3.right) * upright;
        }

        public void OnPicked() { transform.rotation = _handRot; AlignBottomToPivot(); }
        public void OnPlaced() { transform.rotation = _tableRot; AlignBottomToPivot(); }

        /// <summary>Сдвигает визуал-детей так, чтобы низ баундов оказался в пивоте корня.</summary>
        private void AlignBottomToPivot()
        {
            Bounds b = RendererBounds();
            if (b.size == Vector3.zero) return;
            Vector3 delta = transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (Transform child in transform) child.position += delta;
        }

        private Bounds RendererBounds()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}

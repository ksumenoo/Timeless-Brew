using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Поворачивает объект плоскостью к игровой камере (для 2D-картинок гостей и чеков).</summary>
    public class Billboard : MonoBehaviour
    {
        private Camera _cam;

        private void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            transform.rotation = _cam.transform.rotation;   // плоскость параллельна экрану — всегда читаемо
        }
    }
}

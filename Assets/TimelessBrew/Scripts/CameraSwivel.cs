using UnityEngine;
using UnityEngine.InputSystem;

namespace TimelessBrew
{
    /// <summary>
    /// Поворот камеры вокруг своей оси, как во FNAF, но БЕЗ слежения за курсором (мешало брать
    /// предметы). Камера зафиксирована; поворот только по кнопкам: Q — влево, E — вправо
    /// (на геймпаде — бамперы LB/RB). Три фиксированных положения: -35° / 0° / +35°,
    /// переход плавный.
    /// </summary>
    public class CameraSwivel : MonoBehaviour
    {
        [Tooltip("Шаг поворота, градусов.")]
        [SerializeField] private float maxYaw = 35f;

        [Tooltip("Плавность доворота (больше = быстрее).")]
        [SerializeField] private float smoothing = 6f;

        private Quaternion _baseRotation;
        private float _yaw;
        private int _index;   // -1 (влево), 0 (центр), +1 (вправо)

        private void Start() => _baseRotation = transform.rotation;

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool left = (kb != null && kb.qKey.wasPressedThisFrame)
                        || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool right = (kb != null && kb.eKey.wasPressedThisFrame)
                         || (pad != null && pad.rightShoulder.wasPressedThisFrame);

            if (left) _index = Mathf.Max(-1, _index - 1);
            if (right) _index = Mathf.Min(1, _index + 1);

            float target = _index * maxYaw;
            _yaw = Mathf.Lerp(_yaw, target, smoothing * Time.deltaTime);
            transform.rotation = Quaternion.AngleAxis(_yaw, Vector3.up) * _baseRotation;
        }
    }
}

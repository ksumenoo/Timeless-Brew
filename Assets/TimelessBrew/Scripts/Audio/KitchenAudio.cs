using UnityEngine;

namespace TimelessBrew.Audio
{
    /// <summary>
    /// Привязка фоновой музыки к сцене кухни: на старте включает спокойный кофейный трек.
    /// Точка расширения для смены времени суток — вечером можно переключать на <see cref="Music.EveningAmbience"/>.
    /// Компонент добавляется утилитой настройки сцены на главную камеру.
    /// </summary>
    public class KitchenAudio : MonoBehaviour
    {
        [SerializeField] private Music track = Music.Gameplay;

        private void Start()
        {
            AudioManager.Instance.PlayMusic(track);
            AudioManager.Instance.PlayAmbient(Music.NatureLoop);   // звуки леса фоном под музыкой
        }

        /// <summary>Сменить фоновый трек (для системы времени суток).</summary>
        public void SetTrack(Music m) { track = m; AudioManager.Instance.PlayMusic(m); }
    }
}

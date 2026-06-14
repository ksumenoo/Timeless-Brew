using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Дуршлаг (§6.3, этап 2 цикла): принимает обжаренные зёрна со сковороды, удержанием над
    /// раковиной «вытряхивается» шелуха (прогресс), после чего зёрна чистые (mix.sifted) и их
    /// можно пересыпать в кофемолку или банку. Непросеянная шелуха — минус звезда в оценке (§4.3).
    /// Нужен компонент <see cref="Vessel"/> kind=Colander.
    /// </summary>
    [RequireComponent(typeof(Vessel))]
    public class Colander : MonoBehaviour
    {
        [SerializeField] private float siftPerSecond = 0.45f;

        private Vessel _vessel;
        private float _progress;   // 0..1 просеивания текущей партии

        /// <summary>Прогресс просеивания 0..1 (для шкалы).</summary>
        public float SiftProgress => _progress;

        private void Awake() => _vessel = GetComponent<Vessel>();

        private void Update()
        {
            if (_vessel.mix.beans <= 0.001f) _progress = 0f;   // партию высыпали — следующая с нуля
        }

        /// <summary>Трясти над раковиной (зовёт рука, пока держим действие над раковиной).</summary>
        public void Sift(float dt)
        {
            if (_vessel.mix.beans <= 0.001f || _vessel.mix.sifted) return;
            _progress = Mathf.Min(1f, _progress + siftPerSecond * dt);
            if (_progress >= 1f) _vessel.mix.sifted = true;
        }

        /// <summary>Строка состояния для панели сосуда (VesselPanel).</summary>
        public string StatusLine()
        {
            if (_vessel.mix.beans <= 0.001f) return "пусто — пересыпь обжаренные зёрна";
            if (_vessel.mix.sifted) return "<color=#88ff88>Просеяно — можно в кофемолку</color>";
            return _progress > 0.01f
                ? $"Просеивание {(_progress * 100f):0}% — тряси над раковиной"
                : "Шелуха! Зажми над раковиной";
        }
    }
}

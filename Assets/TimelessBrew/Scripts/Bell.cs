using System.Collections;
using UnityEngine;
using TimelessBrew.Audio;

namespace TimelessBrew
{
    /// <summary>
    /// Звоночек выдачи. Подача собирается стопкой: поднос → блюдце → чашка (на зелёной скатерти).
    /// Клик по звоночку: напиток сравнивается с заказом, звёзды, гость уходит, а собранная подача
    /// на пару секунд исчезает и возвращается на свои места.
    /// </summary>
    public class Bell : MonoBehaviour
    {
        public Transform matPoint;        // центр скатерти выдачи
        public float serveRadius = 0.2f;  // в каком радиусе искать чашку на скатерти

        private GuestService _service;

        private void Start() => _service = FindFirstObjectByType<GuestService>();

        public void Ring()
        {
            AudioManager.Instance.Play(Sfx.BellServe);   // физический «динь» звоночка
            if (_service == null) _service = FindFirstObjectByType<GuestService>();
            if (_service == null) return;

            if (!_service.HasOrders) { _service.Message("Сначала вызови гостя (Tab)"); return; }

            Vessel cup = FindCupOnMat();
            if (cup == null) { _service.Message("Поставь подачу на зелёную скатерть"); return; }

            // Сборка подачи: чашка должна стоять на блюдце, а блюдце — на подносе.
            var grab = cup.GetComponent<Grabbable>();
            var saucer = cup.transform.parent != null ? cup.transform.parent.GetComponent<Grabbable>() : null;
            bool onSaucer = saucer != null && saucer.displayName == "Блюдце";
            var tray = onSaucer && saucer.transform.parent != null ? saucer.transform.parent.GetComponent<Grabbable>() : null;
            bool onTray = tray != null && tray.stackGroup == "tray";
            if (!onSaucer || !onTray) { _service.Message("Собери подачу: поднос → блюдце → чашка"); return; }

            bool cupRound = grab != null && !string.IsNullOrEmpty(grab.displayName) && grab.displayName.Contains("пузат");
            // Подача уходит лучшему совпадению среди ждущих гостей; он уходит, его чек исчезает.
            if (!_service.TryServe(cup.mix, cupRound)) { _service.Message("Сейчас никто не ждёт заказ"); return; }
            cup.Empty();

            // Подача (поднос+блюдце+чашка) исчезает на пару секунд и возвращается на свои места.
            StartCoroutine(ServeReset(new[] { grab, saucer, tray }, 2f));
        }

        private IEnumerator ServeReset(Grabbable[] items, float delay)
        {
            foreach (var it in items) if (it != null) it.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(delay);   // реальное время — не зависнет на паузе
            foreach (var it in items)
                if (it != null) { it.gameObject.SetActive(true); it.ReturnHome(); }
        }

        private Vessel FindCupOnMat()
        {
            Vector3 o = matPoint != null ? matPoint.position : transform.position; o.y = 0f;
            Vessel best = null;
            float bestDist = serveRadius;
            foreach (var v in Vessel.All)
            {
                if (v == null) continue;
                if (v.kind != Vessel.Kind.Cup || v.mix.coffee <= 0.05f) continue;
                Vector3 p = v.transform.position; p.y = 0f;
                float d = Vector3.Distance(o, p);
                if (d <= bestDist) { bestDist = d; best = v; }
            }
            return best;
        }
    }
}

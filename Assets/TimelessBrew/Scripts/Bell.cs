using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Звоночек выдачи. Ставишь готовую чашку на зелёную скатерть и кликаешь по звоночку:
    /// напиток сравнивается с заказом гостя, выставляются звёзды, чашка опустошается, гость уходит.
    /// </summary>
    public class Bell : MonoBehaviour
    {
        public Transform matPoint;        // центр скатерти выдачи
        public float serveRadius = 0.2f;  // в каком радиусе искать чашку на скатерти

        private GuestService _service;

        private void Start() => _service = FindFirstObjectByType<GuestService>();

        public void Ring()
        {
            if (_service == null) _service = FindFirstObjectByType<GuestService>();
            if (_service == null) return;

            if (_service.Current == null) { _service.Message("Сначала вызови гостя (Tab)"); return; }

            Vessel cup = FindCupOnMat();
            if (cup == null) { _service.Message("Поставь готовую чашку на зелёную скатерть"); return; }

            int stars = OrderScorer.Score(cup.mix, _service.CurrentOrder, out string reaction, out string faults);
            string guest = _service.Current.displayName;
            cup.Empty();
            _service.Served($"<b>{guest}</b>: {StarsStr(stars)}  ({reaction})  —  {faults}");
        }

        private Vessel FindCupOnMat()
        {
            Vector3 o = matPoint != null ? matPoint.position : transform.position; o.y = 0f;
            Vessel best = null;
            float bestDist = serveRadius;
            foreach (var v in Vessel.All)
            {
                if (v.kind != Vessel.Kind.Cup || v.mix.coffee <= 0.05f) continue;
                Vector3 p = v.transform.position; p.y = 0f;
                float d = Vector3.Distance(o, p);
                if (d <= bestDist) { bestDist = d; best = v; }
            }
            return best;
        }

        private static string StarsStr(int s)
        {
            string r = "";
            for (int i = 0; i < 5; i++) r += i < s ? "★" : "☆";
            return r;
        }
    }
}

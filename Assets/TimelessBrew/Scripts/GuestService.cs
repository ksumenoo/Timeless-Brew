using System.Collections.Generic;
using UnityEngine;
using TimelessBrew.UI;
using TimelessBrew.Audio;

namespace TimelessBrew
{
    /// <summary>
    /// Гости (§4.4). Теперь ОДНОВРЕМЕННО до 4 гостей: каждый — 2D-картинка из Resources/Characters
    /// (по guestId), стоят рядом вдоль −X от зелёной скатерти, и у каждого свой ЧЕК на стойке.
    /// Подача (звоночек) уходит ТОМУ гостю, чьему заказу напиток ближе всего (лучшие звёзды).
    /// </summary>
    public class GuestService : MonoBehaviour
    {
        public Transform counterPoint;   // запасной якорь, если в сцене нет ServeMat

        [Header("Гости в ряд (от зелёной скатерти)")]
        [Tooltip("Смещение первого гостя по X (в минус — левее).")]
        [SerializeField] private float guestOffsetX = -1.25f;
        [SerializeField] private float guestGroundY = 0f;
        [SerializeField] private float guestHeight = 3.6f;
        [Tooltip("Расстояние между гостями в ряду.")]
        [SerializeField] private float guestSpacing = 1.5f;
        [SerializeField] private int maxGuests = 4;

        [Header("Где появляется чек (от скатерти)")]
        [SerializeField] private Vector3 checkOffset = new(-0.18f, 0.22f, -0.06f);

        private class ActiveGuest
        {
            public GuestSO so;
            public bool morning;
            public OrderSpec order;
            public GameObject image;
            public GameObject check;
        }
        private readonly List<ActiveGuest> _guests = new();
        private int _itemLayer = -1;

        public bool HasOrders => _guests.Count > 0;
        public int GuestCount => _guests.Count;
        public int Served { get; private set; }       // обслужено гостей за смену (для итогов)
        public int TotalStars { get; private set; }    // сумма звёзд (для средней оценки)

        private void Start()
        {
            _itemLayer = LayerMask.NameToLayer("Item");
            if (_itemLayer < 0) _itemLayer = 0;
        }

        /// <summary>Позвать гостя — добавляет его в ряд (до maxGuests). false — мест нет (можно повторить).</summary>
        public bool Call(GuestSO g, bool morning)
        {
            if (g == null) return false;
            if (_guests.Count >= maxGuests) { Message($"Мест нет — сначала обслужи гостей (до {maxGuests})"); return false; }

            var order = morning ? g.morning : g.evening;
            AudioManager.Instance.PlayCapped(Sfx.GuestCall, 1.4f);

            int idx = _guests.Count;
            Vector3 baseP = Anchor();
            var ag = new ActiveGuest { so = g, morning = morning, order = order };
            ag.image = SpawnGuest(g, GuestPos(baseP, idx));
            string phase = morning ? "утро" : "вечер";
            // Лёгкий разброс чеков, чтобы не лежали идеально друг на друге.
            ag.check = Check.Create($"{g.displayName} · {phase}", order.Summary(),
                baseP + checkOffset + new Vector3(idx * 0.07f, 0f, -idx * 0.05f), _itemLayer);
            _guests.Add(ag);
            return true;
        }

        /// <summary>Короткая реплика-подсказка — всплывашкой сверху.</summary>
        public void Message(string text) => ToastUI.Toast(text, ToastUI.Neutral, 2.5f);

        /// <summary>
        /// Подать напиток. Уходит гостю с лучшим совпадением заказа; он уходит, его чек исчезает.
        /// false — гостей нет.
        /// </summary>
        public bool TryServe(Mixture cupMix, bool cupRound)
        {
            if (_guests.Count == 0) return false;

            ActiveGuest best = null;
            int bestStars = -1;
            string bestReaction = "", bestFaults = "";
            foreach (var ag in _guests)
            {
                int s = OrderScorer.Score(cupMix, ag.order, cupRound, ag.so.likesLatteArt, out string r, out string f);
                if (s > bestStars) { bestStars = s; best = ag; bestReaction = r; bestFaults = f; }
            }

            ToastUI.Toast($"<b>{best.so.displayName}</b>: {StarsStr(bestStars)}  ({bestReaction})  —  {bestFaults}", ToastUI.Good, 4.5f);
            Served++; TotalStars += bestStars;   // копим статистику для итогов смены
            Remove(best);
            return true;
        }

        private void Remove(ActiveGuest ag)
        {
            _guests.Remove(ag);
            if (ag.image != null) Destroy(ag.image);
            if (ag.check != null) Destroy(ag.check);
            Relayout();   // оставшиеся гости подтягиваются в ряду
        }

        private void Relayout()
        {
            Vector3 baseP = Anchor();
            for (int i = 0; i < _guests.Count; i++)
                if (_guests[i].image != null) _guests[i].image.transform.position = GuestPos(baseP, i);
        }

        // ===================== ВСПОМОГАТЕЛЬНОЕ =====================

        private Vector3 Anchor()
        {
            var mat = GameObject.Find("ServeMat");
            if (mat != null) return mat.transform.position;
            if (counterPoint != null) return counterPoint.position;
            return transform.position;
        }

        private Vector3 GuestPos(Vector3 baseP, int idx)
            => new(baseP.x + guestOffsetX - idx * guestSpacing, guestGroundY + guestHeight * 0.5f, baseP.z);

        private GameObject SpawnGuest(GuestSO g, Vector3 pos)
        {
            var go = new GameObject("Guest_" + g.displayName);
            go.transform.position = pos;
            go.AddComponent<Billboard>();

            var tex = !string.IsNullOrEmpty(g.guestId) ? Resources.Load<Texture2D>("Characters/" + g.guestId) : null;
            if (tex != null)
            {
                float ppu = tex.height / Mathf.Max(0.1f, guestHeight);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                go.AddComponent<SpriteRenderer>().sprite = sprite;
            }
            else
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.transform.SetParent(go.transform, false);
                fallback.transform.localScale = new Vector3(0.5f, guestHeight * 0.5f, 0.5f);
                var r = fallback.GetComponent<Renderer>();
                var mpb = new MaterialPropertyBlock();
                var col = new Color(0.82f, 0.7f, 0.52f);
                mpb.SetColor("_BaseColor", col); mpb.SetColor("_Color", col);
                r.SetPropertyBlock(mpb);
            }
            return go;
        }

        private static string StarsStr(int s)
        {
            string r = "";
            for (int i = 0; i < 5; i++) r += i < s ? "★" : "☆";
            return r;
        }
    }
}

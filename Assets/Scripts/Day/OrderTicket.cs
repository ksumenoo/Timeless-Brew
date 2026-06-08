using System;
using UnityEngine;
using TimelessBrew.Data;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Бумажка-заказ (§3.3, §6 v4.3): имя гостя + состав напитка.
    ///
    /// Поток v4.3: когда гость озвучивает заказ, бумажка появляется ПЕРЕД Нари (не сразу на
    /// чекхолдере). Чтобы взять заказ в работу, игрок берёт бумажку и вешает её на чекхолдер —
    /// после этого она «уходит в угол» (у нас — на крючок). Состояние отражает <see cref="Claimed"/>.
    ///
    /// Клик по бумажке (OnMouseDown, нужен коллайдер) вешает её на чекхолдер. Визуал текста
    /// необязателен: если назначен TextMesh — пишем в него, иначе текст в <see cref="Text"/> и в лог.
    /// </summary>
    public class OrderTicket : MonoBehaviour
    {
        [Tooltip("Опционально: 3D-текст для отображения заказа. Можно оставить пустым на этапе заглушек.")]
        [SerializeField] private TextMesh label;

        public GuestVisit Visit { get; private set; }
        public GuestController Guest { get; private set; }

        /// <summary>Взят ли заказ в работу (бумажка повешена на чекхолдер).</summary>
        public bool Claimed { get; private set; }

        /// <summary>Эталонный заказ этого тикета.</summary>
        public DrinkSpec Order => Visit != null ? Visit.Order : null;

        /// <summary>Сформированный текст тикета (имя + состав).</summary>
        public string Text { get; private set; }

        /// <summary>Игрок взял заказ в работу (повесил на чекхолдер). Чекхолдер слушает это событие.</summary>
        public event Action<OrderTicket> OnClaimed;

        /// <summary>Привязать тикет к гостю и его визиту, сформировать текст. Тикет создаётся «не взятым».</summary>
        public void Bind(GuestController guest, GuestVisit visit)
        {
            Guest = guest;
            Visit = visit;
            Claimed = false;

            string name = visit != null ? visit.DisplayName : "—";
            string body = DrinkSpecFormatter.Describe(Order);
            string takeaway = visit != null && visit.TakeawayOnly ? "  [навынос]" : string.Empty;

            Text = $"{name}{takeaway}\n— {DrinkSpecFormatter.ShortName(Order)} —\n{body}";

            if (label != null) label.text = Text;
            Debug.Log($"[OrderTicket] Заказ появился (не взят):\n{Text}", this);
        }

        /// <summary>Взять заказ в работу — повесить на чекхолдер. Повторные вызовы игнорируются.</summary>
        public void Claim()
        {
            if (Claimed) return;
            Claimed = true;
            OnClaimed?.Invoke(this);
            string who = Visit != null ? Visit.DisplayName : "—";
            Debug.Log($"[OrderTicket] Заказ взят в работу: {who}.", this);
        }

        // Клик по бумажке = взять заказ в работу (нужен коллайдер + камера в сцене).
        private void OnMouseDown()
        {
            if (!Claimed) Claim();
        }

        [ContextMenu("DEBUG: Claim (hang on checkholder)")]
        private void DebugClaim() => Claim();
    }
}

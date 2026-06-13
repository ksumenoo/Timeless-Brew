using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Чекхолдер (§6.15): на него вешаются чеки. Клик рукой с чеком — чек крепится (распределяются
    /// в ряд над планкой). Пина — логика в <see cref="Hand"/>; снятие — при подаче (гость уходит).
    /// </summary>
    public class Chekholder : MonoBehaviour
    {
        [SerializeField] private int slots = 4;   // по числу крючков
        private readonly List<Check> _pinned = new();

        /// <summary>Повесить чек. false — мест нет.</summary>
        public bool Pin(Check c)
        {
            if (c == null) return true;
            if (_pinned.Contains(c)) return true;
            if (_pinned.Count >= slots) return false;
            _pinned.Add(c);
            c.transform.SetParent(transform, true);
            c.OnPinned(this);
            Layout();
            return true;
        }

        public void Unpin(Check c) { if (_pinned.Remove(c)) Layout(); }

        private void Layout()
        {
            Bounds b = HolderBounds();
            int n = _pinned.Count;
            if (n == 0) return;

            // Чек висит НА планке: на её передней грани (к камере), по высоте — у центра планки,
            // распределены вдоль длинной горизонтальной оси. Раньше клали над верхом — отсюда «промах».
            var cam = Camera.main;
            Vector3 toCam = cam != null ? cam.transform.position - b.center : Vector3.back;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.back;
            toCam.Normalize();
            // Дистанция от центра до передней грани ВДОЛЬ направления к камере (а не половина ширины).
            float front = Mathf.Abs(toCam.x) * b.extents.x + Mathf.Abs(toCam.z) * b.extents.z + 0.05f;

            bool alongX = b.size.x >= b.size.z;
            float lo = alongX ? b.min.x : b.min.z;
            float hi = alongX ? b.max.x : b.max.z;
            float y = b.min.y + b.size.y * 0.25f;   // на уровне крючков (низ-перёд), а не над верхом планки

            // Сдвиг вправо на полчека (≈0.13 м) — чтобы чеки сели на крючки, а не между.
            Vector3 lenAxis = alongX ? Vector3.right : Vector3.forward;
            float dir = Mathf.Sign(Vector3.Dot(cam != null ? cam.transform.right : Vector3.right, lenAxis));
            if (dir == 0f) dir = 1f;
            Vector3 nudge = lenAxis * (0.26f * dir);   // ~целый чек вправо — ровно на крючки

            for (int i = 0; i < n; i++)
            {
                if (_pinned[i] == null) continue;
                // ФИКСИРОВАННЫЕ слоты-крючки слева направо: чек i всегда на крючке i (не пере-центрируем).
                float t = slots > 1 ? Mathf.Clamp01((float)i / (slots - 1)) : 0.5f;
                float coord = Mathf.Lerp(lo + (hi - lo) * 0.14f, hi - (hi - lo) * 0.14f, t);
                Vector3 basePos = alongX ? new Vector3(coord, y, b.center.z) : new Vector3(b.center.x, y, coord);
                _pinned[i].transform.position = basePos + toCam * front + nudge;
            }
        }

        private Bounds HolderBounds()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(transform.position, Vector3.one * 0.3f);
            Bounds bb = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bb.Encapsulate(rends[i].bounds);
            return bb;
        }
    }
}

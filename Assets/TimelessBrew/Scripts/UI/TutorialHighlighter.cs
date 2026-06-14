using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Подсветка цели обучения «вывернутой белой текстурой» (inverted hull): на каждый меш предмета
    /// надевается копия, у которой наружу торчит только «изнанка» — получается белый контур-силуэт,
    /// мягко пульсирующий. Основной путь — шейдер TimelessBrew/TutorialOutline (раздувание вдоль
    /// нормалей, ровный контур без заливки предмета). Если шейдер не найден — запасной путь
    /// (увеличенная копия с отсечением передних граней). Подсветка не влияет на логику обучения.
    /// </summary>
    public class TutorialHighlighter : MonoBehaviour
    {
        private readonly List<Transform> _shells = new();
        private Material _mat;
        private bool _outlineShader;   // используем нормальный контур-шейдер (а не запасную копию)
        private bool _canHull;         // запасной путь возможен (есть отсечение граней)
        private GameObject _target;
        private float _baseWidth = 0.012f;   // толщина контура в мировых единицах (тонкая каёмка)

        private void Awake()
        {
            var outline = Shader.Find("TimelessBrew/TutorialOutline");
            if (outline != null)
            {
                _mat = new Material(outline) { name = "TutorialOutline" };
                _mat.SetColor("_OutlineColor", Color.white);
                _mat.SetFloat("_OutlineWidth", 0.03f);
                _outlineShader = true;
                _canHull = true;
                return;
            }

            // Запасной путь: URP/Unlit с отсечением передних граней.
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            _mat = new Material(sh) { name = "TutorialHull" };
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", Color.white);
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", Color.white);
            _canHull = _mat.HasProperty("_Cull");
            if (_canHull)
            {
                _mat.SetFloat("_Cull", (float)CullMode.Front);
                if (_mat.HasProperty("_ZWrite")) _mat.SetFloat("_ZWrite", 0f);   // меньше заливки предмета спереди
            }
            _mat.renderQueue = 2010;
            if (!_canHull)
                Debug.LogWarning("[TimelessBrew] Нет ни контур-шейдера, ни отсечения граней — подсветка обучения отключена.");
        }

        public void SetTarget(GameObject target)
        {
            if (target == _target) return;
            Clear();
            _target = target;
            if (target == null || !_canHull) return;

            // Тонкая каёмка: доля размера цели, ограниченная сверху, чтобы контур не разрастался.
            float maxExtent = 0.1f;
            var rends = target.GetComponentsInChildren<MeshRenderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                maxExtent = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
            }
            _baseWidth = Mathf.Clamp(maxExtent * 0.05f, 0.008f, 0.018f);
            if (_outlineShader) _mat.SetFloat("_OutlineWidth", _baseWidth);

            foreach (var mf in target.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var shell = new GameObject("TutHull");
                shell.transform.SetParent(mf.transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;
                shell.transform.localScale = Vector3.one;   // контур даёт шейдер; в запасном пути масштабируем ниже
                shell.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var sr = shell.AddComponent<MeshRenderer>();
                sr.sharedMaterial = _mat;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
                _shells.Add(shell.transform);
            }
        }

        private void Update()
        {
            if (_shells.Count == 0) return;
            float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));   // 0..1, заметна и при остановленном времени

            if (_outlineShader)
            {
                _mat.SetFloat("_OutlineWidth", _baseWidth * (1f + 0.25f * pulse));   // тонкий пульсирующий контур
                return;
            }

            // Запасной путь: чуть увеличенная копия.
            float s = 1.04f + 0.015f * pulse;
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                if (_shells[i] == null) { _shells.RemoveAt(i); continue; }
                _shells[i].localScale = Vector3.one * s;
            }
        }

        private void Clear()
        {
            foreach (var t in _shells) if (t != null) Destroy(t.gameObject);
            _shells.Clear();
        }

        private void OnDestroy()
        {
            Clear();
            if (_mat != null) Destroy(_mat);
        }
    }
}

using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Items;

namespace TimelessBrew.Debugging
{
    /// <summary>
    /// Экранный отладочный HUD для тестовой сцены: показывает «невидимое» состояние
    /// (свойства не видны в инспекторе) — курсор, печку, сковороду, дуршлаг, кофемолку,
    /// турку, чайник и накопленную сессию напитка. Повесь на любой объект сцены.
    /// </summary>
    public class CookingDebugHUD : MonoBehaviour
    {
        private CursorHandler _cursor;
        private Stove _stove;
        private RoastingPan _pan;
        private Colander _colander;
        private Grinder _grinder;
        private Cezve _cezve;
        private Kettle _kettle;
        private Cup _cup;
        private Pitcher _pitcher;
        private LongSpoon _spoon;
        private BrewSession _brew;

        private void OnEnable() => Refind();

        private void Refind()
        {
            _cursor = FindObjectOfType<CursorHandler>();
            _stove = FindObjectOfType<Stove>();
            _pan = FindObjectOfType<RoastingPan>();
            _colander = FindObjectOfType<Colander>();
            _grinder = FindObjectOfType<Grinder>();
            _cezve = FindObjectOfType<Cezve>();
            _kettle = FindObjectOfType<Kettle>();
            _cup = FindObjectOfType<Cup>();
            _pitcher = FindObjectOfType<Pitcher>();
            _spoon = FindObjectOfType<LongSpoon>();
            _brew = FindObjectOfType<BrewSession>();
        }

        private void OnGUI()
        {
            if (_cursor == null || _stove == null) Refind();

            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            GUILayout.BeginArea(new Rect(10, 10, 520, 560), GUI.skin.box);

            GUILayout.Label("<b>=== COOKING DEBUG ===</b>", style);

            if (_cursor != null)
            {
                GUILayout.Label($"Курсор: держим=<b>{NameOf(_cursor.Carried)}</b>  наведён=<b>{NameOf(_cursor.HoverTarget)}</b>", style);
                GUILayout.Label($"  ЛКМ-на-цели=<b>{_cursor.HeldOnTarget}</b>  прямое удержание=<b>{NameOf(_cursor.DirectHoldTarget)}</b>", style);
            }

            if (_stove != null)
                GUILayout.Label($"Печка: T=<b>{_stove.Temperature:0.00}</b>  деление=<b>{_stove.HeatDivision}</b>  перегрев=<b>{_stove.IsOverheated}</b>", style);

            if (_pan != null)
                GUILayout.Label($"Сковорода: зёрна=<b>{_pan.HasBeans}</b>  обжарка=<b>{_pan.RoastProgress:0.00}</b>  стадия=<b>{_pan.Stage}</b>", style);

            if (_colander != null)
                GUILayout.Label($"Дуршлаг: зёрна=<b>{_colander.HasBeans}</b>  чистота=<b>{_colander.SiftProgress01:0.00}</b>", style);

            if (_grinder != null)
                GUILayout.Label($"Кофемолка: зёрна=<b>{_grinder.HasBeans}</b>  помол=<b>{_grinder.GrindProgress:0.00}</b>  фракция=<b>{_grinder.CurrentGrind}</b>", style);

            if (_kettle != null)
                GUILayout.Label($"Чайник: кипение=<b>{_kettle.BoilProgress:0.00}</b>  горячий=<b>{_kettle.IsHot}</b>", style);

            if (_cezve != null)
            {
                string waterStr = _cezve.HasWater ? _cezve.Water.ToString() : "нет";
                GUILayout.Label($"Турка: кофе=<b>{_cezve.HasCoffee}</b> вода=<b>{waterStr}</b> пенка=<b>{_cezve.FoamLevel:0.00}</b> кач-во=<b>{_cezve.BrewQuality01:0.00}</b> готова=<b>{_cezve.Ready}</b> брак=<b>{_cezve.Ruined}</b>", style);
            }

            if (_spoon != null)
                GUILayout.Label($"Ложка: молотый=<b>{_spoon.HasGround}</b>  фракция=<b>{_spoon.GroundGrind}</b>", style);

            if (_pitcher != null)
                GUILayout.Label($"Молочник: молоко=<b>{_pitcher.HasMilk}</b>", style);

            if (_cup != null)
                GUILayout.Label($"Чашка: кофе=<b>{_cup.HasCoffee}</b> ({_cup.CoffeeFill:0.00})  льётся молоко=<b>{_cup.IsPouring}</b>  молоко=<b>{_cup.HasMilk}</b>  латте=<b>{_cup.LatteArtQuality:0.00}</b>", style);

            if (_brew != null && _brew.Current != null)
            {
                var c = _brew.Current;
                GUILayout.Label($"<b>Сессия:</b> обжарка=<b>{c.roastQuality:0.00}</b> шелуха=<b>{c.siftQuality:0.00}</b> помол=<b>{c.spec.grind}</b> вода=<b>{c.spec.water}</b> варка=<b>{c.brewQuality:0.00}</b>", style);
                GUILayout.Label($"  молоко=<b>{c.spec.withMilk}</b> латте=<b>{c.latteArtQuality:0.00}</b> сахар=<b>{c.spec.sugarCubes}</b> сироп=<b>{c.spec.syrup}</b> специи=<b>{string.Join(",", c.spec.spices)}</b>", style);
            }

            GUILayout.EndArea();
        }

        private static string NameOf(Object o) => o != null ? o.name : "—";
    }
}

namespace TimelessBrew
{
    /// <summary>
    /// Мусорка (§3.3). Если держишь сосуд в руке и кликаешь по мусорке — содержимое сбрасывается
    /// (логика в <see cref="Hand"/>: Carried.Vessel.Empty()). Горсть молотого тоже выкидывается сюда.
    /// Это просто маркер-цель.
    /// </summary>
    public class Trash : UnityEngine.MonoBehaviour { }
}

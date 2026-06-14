using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Держатель списка гостей для книги рецептов (§4 — меню настройки клиентов). Заполняет сборщик.</summary>
    public class GuestLibrary : MonoBehaviour
    {
        public GuestSO[] guests = new GuestSO[0];
    }
}

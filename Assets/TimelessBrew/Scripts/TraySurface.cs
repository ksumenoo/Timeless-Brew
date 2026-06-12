using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Поверхность подноса (слой Surface, дочерний бокс над подносом): предмет, поставленный
    /// на неё, прицепляется к подносу и едет вместе с ним. Ссылку на поднос ставит утилита/сборщик.
    /// </summary>
    public class TraySurface : MonoBehaviour
    {
        public Grabbable tray;
    }
}

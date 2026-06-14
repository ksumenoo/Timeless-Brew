namespace TimelessBrew
{
    /// <summary>
    /// То, что «крутят/качают» пустой рукой удержанием (мехи печи, ручка кофемолки, кран).
    /// Рука вызывает <see cref="Hold"/> каждый кадр, пока удерживается действие над объектом.
    /// </summary>
    public interface IHandHold
    {
        void Hold(float dt);
    }
}

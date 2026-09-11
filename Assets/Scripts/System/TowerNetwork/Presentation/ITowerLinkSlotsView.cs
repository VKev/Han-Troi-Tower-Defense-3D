namespace TowerDefense3D.Towers
{
    /// <summary>
    /// The per-port link indicator authored on a tower that accepts incoming links.
    /// </summary>
    /// <remarks>
    /// A tower advertises the indicator by carrying one; towers without it are simply skipped, so
    /// no catalog flag has to say which prefabs have one.
    /// </remarks>
    public interface ITowerLinkSlotsView
    {
        /// <summary>
        /// Occupied count rather than a per-port list: ports fill in order and the indicator only
        /// answers "how many of my ports are taken", so a count is the whole of what it needs.
        /// </summary>
        void Render(int occupiedSlotCount);
    }
}

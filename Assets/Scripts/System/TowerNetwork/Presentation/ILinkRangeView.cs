using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// The reach ring drawn flat on the board: a wash inside, a rim, and markers on the axes.
    /// </summary>
    /// <remarks>
    /// One contract for two callers that both mean "this is how far from here". The drag preview
    /// shows it around the cell a tower would land on; tower selection shows it around a tower
    /// already standing. Neither owns the ring, so neither can drift from the other's look.
    ///
    /// The radius is passed on every show rather than set once, so a reach that changes - an
    /// upgraded tower, or a rule edited in the catalog - is picked up the next time the ring is
    /// drawn instead of being baked in when it was first built.
    /// </remarks>
    public interface ILinkRangeView
    {
        void Show(Vector3 centre, float radiusMeters);

        void Hide();
    }
}

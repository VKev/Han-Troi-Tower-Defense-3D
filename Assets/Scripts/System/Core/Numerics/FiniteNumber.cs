namespace TowerDefense3D.Core
{
    public static class FiniteNumber
    {
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

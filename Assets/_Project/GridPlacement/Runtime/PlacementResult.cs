using System;

namespace TowerDefense3D.GridPlacement
{
    [Flags]
    public enum PlacementFailureFlags
    {
        None = 0,
        OutOfBounds = 1 << 0,
        MissingSupport = 1 << 1,
        NotBuildable = 1 << 2,
        StaticBlocker = 1 << 3,
        Occupied = 1 << 4,
        SpawnFailed = 1 << 5
    }

    public readonly struct PlacementResult : IEquatable<PlacementResult>
    {
        public PlacementResult(PlacementFailureFlags failures)
        {
            Failures = failures;
        }

        public PlacementFailureFlags Failures { get; }
        public bool Succeeded => Failures == PlacementFailureFlags.None;

        public static PlacementResult Success => new PlacementResult(PlacementFailureFlags.None);

        public bool Equals(PlacementResult other) => Failures == other.Failures;
        public override bool Equals(object obj) => obj is PlacementResult other && Equals(other);
        public override int GetHashCode() => (int)Failures;

        public static bool operator ==(PlacementResult left, PlacementResult right) => left.Equals(right);
        public static bool operator !=(PlacementResult left, PlacementResult right) => !left.Equals(right);
    }
}

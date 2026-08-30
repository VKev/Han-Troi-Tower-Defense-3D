namespace TowerDefense3D.Enemies
{
    public static class EnemySpawnPresentationTiming
    {
        public const float PrespawnVfxDurationSeconds = 2f;

        // The enemy stays hidden through the build-up and pops 0 -> 1 only in the last stretch,
        // so it reaches full scale on the frame the ring bursts around it rather than standing
        // in place waiting for the effect to catch up.
        public const float SpawnScaleDurationSeconds = 0.15f;

        public const float SpawnScaleDelaySeconds = 0.4f;

        public const float SpawnMovementDelaySeconds =
            SpawnScaleDelaySeconds + SpawnScaleDurationSeconds;

    }
}

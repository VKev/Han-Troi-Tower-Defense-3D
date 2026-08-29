namespace TowerDefense3D.Enemies
{
    public static class EnemySpawnPresentationTiming
    {
        public const float PrespawnVfxDurationSeconds = 2f;

        // When the ring bursts inside VFX_Prespawn. Must stay equal to the start delay authored
        // on the "circle", "flash", "glow", "sparkles" and "lines" particle systems.
        public const float PrespawnRingDelaySeconds = 0.4f;

        // The enemy stays hidden through the build-up and pops 0 -> 1 only in the last stretch,
        // so it reaches full scale on the frame the ring bursts around it rather than standing
        // in place waiting for the effect to catch up.
        public const float SpawnScaleDurationSeconds = 0.15f;

        public const float SpawnScaleDelaySeconds = 0.4f;

        // The effect is planted this far ahead of the enemy, measured in the enemy's own travel
        // time, so the enemy walks through the middle of the ring while it is at its brightest
        // instead of standing on the spot for the one frame the ring appears. The ring particle
        // lives 0.4s, so half of that puts the crossing near its peak.
        public const float PrespawnLeadSeconds = 0.2f;
    }
}

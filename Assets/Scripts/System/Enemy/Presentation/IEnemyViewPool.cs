using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    public interface IEnemyViewPool
    {
        void Spawn(EnemySnapshot enemy);
        void Despawn(long enemyId);

        /// <summary>
        /// Takes a view off the board at once, with no death animation.
        /// </summary>
        /// <remarks>
        /// <see cref="Despawn"/> plays a death, which is right for an enemy that was killed and
        /// wrong for one that is simply being handed over - the standing boss stepping aside for
        /// the one that fights would otherwise be seen dying on the spot.
        /// </remarks>
        void ReleaseImmediate(long enemyId);

        /// <summary>
        /// Moves a view to a new enemy id, keeping the same object on screen.
        /// </summary>
        /// <remarks>
        /// Used when the combat plan takes over the boss that has been standing on the road: the
        /// instance carries on, under the id the plan issued for it.
        /// </remarks>
        void Rekey(long oldEnemyId, long newEnemyId);
        void ShowReaction(long enemyId, ElementReactionEvent reaction);
        /// <summary>
        /// Sets the animation playback rate for every enemy on the board, and for the ones spawned
        /// after this call.
        /// </summary>
        void SetAnimationSpeed(float speed);

        void Render(IReadOnlyList<EnemySnapshot> enemies, float interpolationAlpha);
        void ReleaseAll();
    }
}

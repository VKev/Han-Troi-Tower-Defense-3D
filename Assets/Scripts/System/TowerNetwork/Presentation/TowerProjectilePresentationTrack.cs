using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public struct TowerProjectilePresentationTrack
    {
        private Vector3 previousPosition;
        private Vector3 currentPosition;

        private TowerProjectilePresentationTrack(TowerProjectileSnapshot snapshot)
        {
            ProjectileId = snapshot.ProjectileId;
            Target = snapshot.Target;
            Payload = snapshot.Payload;
            LaunchDelayTicks = snapshot.LaunchDelayTicks;
            IsRetiring = false;
            ReleaseAfterRender = false;
            previousPosition = ToVector3(snapshot.Position);
            currentPosition = previousPosition;
        }

        public long ProjectileId { get; private set; }
        public TowerNodeId Target { get; private set; }
        public ProjectilePayload Payload { get; private set; }
        public int LaunchDelayTicks { get; private set; }
        public bool IsRetiring { get; private set; }
        public bool ReleaseAfterRender { get; private set; }
        public bool IsVisible => LaunchDelayTicks == 0 || IsRetiring;

        public static TowerProjectilePresentationTrack Create(TowerProjectileSnapshot snapshot)
        {
            return new TowerProjectilePresentationTrack(snapshot);
        }

        public void Advance(TowerProjectileSnapshot snapshot)
        {
            if (snapshot.ProjectileId != ProjectileId)
            {
                throw new ArgumentException(
                    "A presentation track cannot change projectile identity.",
                    nameof(snapshot));
            }

            Vector3 nextPosition = ToVector3(snapshot.Position);
            bool startsRendering = LaunchDelayTicks > 0 && snapshot.LaunchDelayTicks == 0;
            previousPosition = startsRendering ? nextPosition : currentPosition;
            currentPosition = nextPosition;
            Target = snapshot.Target;
            Payload = snapshot.Payload;
            LaunchDelayTicks = snapshot.LaunchDelayTicks;
            IsRetiring = false;
            ReleaseAfterRender = false;
        }

        public void BeginRetirement(TowerWorldPosition targetPosition)
        {
            previousPosition = currentPosition;
            currentPosition = ToVector3(targetPosition);
            LaunchDelayTicks = 0;
            IsRetiring = true;
            ReleaseAfterRender = false;
        }

        public void PrepareReleaseAfterRender()
        {
            previousPosition = currentPosition;
            LaunchDelayTicks = 0;
            IsRetiring = true;
            ReleaseAfterRender = true;
        }

        public Vector3 CalculateRenderedPosition(float interpolationAlpha)
        {
            if (ReleaseAfterRender)
            {
                return currentPosition;
            }

            return Vector3.Lerp(previousPosition, currentPosition, Mathf.Clamp01(interpolationAlpha));
        }

        private static Vector3 ToVector3(TowerWorldPosition position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }
    }
}

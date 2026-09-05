using System;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        private sealed class NodeState
        {
            public NodeState(
                TowerNodeId id,
                TowerRuntimeSpec spec,
                TowerWorldPosition position,
                TowerCombatDefinition definition)
            {
                Id = id;
                Spec = spec ?? throw new ArgumentNullException(nameof(spec));
                Position = position;
                Definition = definition;
                UpgradeLevel = 0;
                InputBuffer = new TowerInputBuffer(spec.InputPortCount, spec.QueueCapacityPerInput);
                CycleProgressTicks = 0;
            }

            public TowerNodeId Id { get; }

            /// <summary>
            /// Replaced when the tower is upgraded. Port counts come from the same definition, so
            /// the input buffer built for the old spec still fits the new one.
            /// </summary>
            public TowerRuntimeSpec Spec { get; set; }

            /// <summary>Kept so an upgrade can rebuild the spec from the same authoring data.</summary>
            public TowerCombatDefinition Definition { get; }

            public int UpgradeLevel { get; set; }
            public TowerWorldPosition Position { get; }
            public TowerInputBuffer InputBuffer { get; }
            public int CycleProgressTicks { get; set; }
        }

        private sealed class ProjectileState
        {
            public ProjectileState(
                long projectileId, TowerNodeId source, TowerNodeId target, int targetInputPort,
                TowerWorldPosition position, ProjectilePayload payload, int remainingLaunchDelayTicks)
            {
                if (projectileId <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(projectileId));
                }

                if (!source.IsValid)
                {
                    throw new ArgumentException("Projectile source must be valid.", nameof(source));
                }

                if (!target.IsValid)
                {
                    throw new ArgumentException("Projectile target must be valid.", nameof(target));
                }

                if (targetInputPort < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(targetInputPort));
                }

                if (remainingLaunchDelayTicks < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(remainingLaunchDelayTicks));
                }

                ProjectileId = projectileId;
                Source = source;
                Target = target;
                TargetInputPort = targetInputPort;
                Position = position;
                Payload = payload;
                RemainingLaunchDelayTicks = remainingLaunchDelayTicks;
            }

            public long ProjectileId { get; }
            public TowerNodeId Source { get; }
            public TowerNodeId Target { get; }
            public int TargetInputPort { get; }
            public TowerWorldPosition Position { get; set; }
            public ProjectilePayload Payload { get; }
            public int RemainingLaunchDelayTicks { get; set; }
        }
        
        private sealed class LinkState
        {
            public LinkState(TowerNodeId source, TowerNodeId target, int targetInputPort)
            {
                Source = source;
                Target = target;
                TargetInputPort = targetInputPort;
            }

            public TowerNodeId Source { get; }
            public TowerNodeId Target { get; }
            public int TargetInputPort { get; }
        }
    }
}

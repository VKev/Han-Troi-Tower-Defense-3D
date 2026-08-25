using System;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemyInstance
    {
        internal EnemyInstance(long id, EnemyDefinition definition, Vector3 position)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            Id = id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Health = definition.BaseMaxHealth;
            Position = position;
            PreviousPosition = position;
            TargetPointIndex = 1;
        }

        public long Id { get; }
        public EnemyDefinition Definition { get; }
        public float Health { get; internal set; }
        public float HealthFraction => Health / Definition.BaseMaxHealth;
        public Vector3 Position { get; internal set; }
        public Vector3 PreviousPosition { get; internal set; }
        public bool IsAlive => Health > 0f;
        internal int TargetPointIndex { get; set; }
    }
}

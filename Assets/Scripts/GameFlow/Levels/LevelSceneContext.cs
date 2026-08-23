using System;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Explicit readiness and teardown boundary for one authored level scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSceneContext : MonoBehaviour
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private MonoBehaviour[] participants = Array.Empty<MonoBehaviour>();

        private int initializedParticipantCount;

        public int LevelNumber => levelNumber;
        public bool IsInitialized { get; private set; }

        public bool TryInitialize(LevelSceneRuntimeContext runtimeContext, out string error)
        {
            if (runtimeContext.LevelNumber != levelNumber)
            {
                error = $"Loaded level {runtimeContext.LevelNumber} does not match authored context {levelNumber}.";
                return false;
            }

            initializedParticipantCount = 0;
            try
            {
                for (int index = 0; index < participants.Length; index++)
                {
                    MonoBehaviour participantBehaviour = participants[index];
                    if (participantBehaviour == null)
                    {
                        throw new InvalidOperationException($"Level scene participant {index} is missing.");
                    }

                    if (!(participantBehaviour is ILevelSceneParticipant participant))
                    {
                        throw new InvalidOperationException(
                            $"{participantBehaviour.name} must implement {nameof(ILevelSceneParticipant)}.");
                    }

                    participant.Initialize(runtimeContext);
                    initializedParticipantCount++;
                }

                IsInitialized = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ShutdownInitializedParticipants();
                error = exception.Message;
                return false;
            }
        }

        public void Shutdown()
        {
            ShutdownInitializedParticipants();
            IsInitialized = false;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void ShutdownInitializedParticipants()
        {
            for (int index = initializedParticipantCount - 1; index >= 0; index--)
            {
                var participant = (ILevelSceneParticipant)participants[index];
                try
                {
                    participant.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, participants[index]);
                }
            }

            initializedParticipantCount = 0;
        }
    }
}

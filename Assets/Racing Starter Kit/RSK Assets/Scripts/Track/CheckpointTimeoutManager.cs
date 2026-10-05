using UnityEngine;

namespace SpinMotion
{
    public class CheckpointTimeoutManager : MonoBehaviour
    {
        public GameEvents gameEvents;
        [Min(0.1f)] public float checkpointTimeoutSeconds = 10f;
        public bool onlyTrackPlayerOne = true;

        public float RemainingTime => remainingTime;

        private float remainingTime;
        private bool isRunning;

        private void Awake()
        {
            gameEvents.RaceStartedEvent.AddListener(OnRaceStarted);
            gameEvents.RestartRaceEvent.AddListener(OnRestartRace);
            gameEvents.RaceFinishedEvent.AddListener(OnRaceFinished);
            gameEvents.CheckpointPassedEvent.AddListener(OnCheckpointPassed);
        }

        private void Update()
        {
            if (!isRunning)
            {
                return;
            }

            remainingTime -= Time.deltaTime;
            if (remainingTime > 0f)
            {
                return;
            }

            isRunning = false;
            remainingTime = 0f;
            gameEvents.OnClickRestartRaceEvent.Invoke();
        }

        private void OnRaceStarted()
        {
            ResetTimer();
            isRunning = true;
        }

        private void OnRestartRace()
        {
            ResetTimer();
            isRunning = false;
        }

        private void OnRaceFinished(RaceFinishType raceFinishType)
        {
            isRunning = false;
        }

        private void OnCheckpointPassed(int carRacePositionIndex, int checkpointNumber)
        {
            if (onlyTrackPlayerOne && carRacePositionIndex != 0)
            {
                return;
            }

            ResetTimer();
            isRunning = true;
        }

        private void ResetTimer()
        {
            remainingTime = checkpointTimeoutSeconds;
        }
    }
}

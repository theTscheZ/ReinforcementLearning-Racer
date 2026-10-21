using UnityEngine;

namespace SpinMotion
{
    public class CheckpointTimer : MonoBehaviour
    {
        [SerializeField] private float maxSegmentTime = 20f;
        [SerializeField] private CarAgent carAgent;
        
        private float segmentStartTime;
        private bool running;

        private void Start()
        {
            StartTimer();
        }
        
        private void Update()
        {
            if (running && GetCurrentSegmentTime() >= maxSegmentTime)
            {
                Debug.LogWarning($"Segment time exceeded max time of {maxSegmentTime} seconds.");
                // reset episode
                running = false;
                carAgent.AddReward(-1f);
                carAgent.EndEpisode();
                Debug.LogWarning("[RESET] CheckpointTimer: Zeitlimit");
            }
        }

        public void StartTimer()
        {
            segmentStartTime = Time.time;
            running = true;
        }
        
        public void StopTimer()
        {
            running = false;
        }

        public float GetCurrentSegmentTime()
        {
            if (!running)
                return 0f;

            return Time.time - segmentStartTime;
        }

        public float CompleteSegment()
        {
            if (!running)
                return 0f;

            float segmentTime = Time.time - segmentStartTime;

            segmentStartTime = Time.time;
            
            // Grundreward für das Erreichen
            float reward = 1f;

            // Bonus für schnelles Erreichen
            float speedBonus = Mathf.Clamp(
                (maxSegmentTime - segmentTime) / maxSegmentTime,
                0f,
                1f
            );

            carAgent.AddReward(reward + speedBonus);
            Debug.Log($"Reward: {reward + speedBonus}");

            return segmentTime;
        }
    }
}
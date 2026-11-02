using UnityEngine;

namespace SpinMotion
{
    public class CheckpointTimer : MonoBehaviour
    {
        [SerializeField] private float maxSegmentTime = 20f;
        [SerializeField] private CarAgent carAgent;
        
        private float segmentStartTime;
        private bool running;
        public float RemainingFraction => running
            ? Mathf.Clamp01(1f - GetCurrentSegmentTime() / Mathf.Max(0.1f, maxSegmentTime)) : 0f;

        private void Start()
        {
            if (!running)
                StartTimer();
        }
        
        private void Update()
        {
            if (running && GetCurrentSegmentTime() >= maxSegmentTime)
            {
                running = false;
                carAgent.FailEpisode("Timeout");
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
            
            carAgent.ReachSegment(segmentTime);

            return segmentTime;
        }
    }
}

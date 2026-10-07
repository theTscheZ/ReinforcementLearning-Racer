using UnityEngine;

namespace SpinMotion
{
    public class CheckpointTimer : MonoBehaviour
    {
        private float segmentStartTime;
        private bool running;

        private void Start()
        {
            StartTimer();
        }

        public void StartTimer()
        {
            segmentStartTime = Time.time;
            running = true;
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

            return segmentTime;
        }
    }
}
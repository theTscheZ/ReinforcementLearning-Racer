using UnityEngine;

namespace SpinMotion
{
    public class TrackSegment : MonoBehaviour
    {
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private TrackCheckpoint checkpoint;

        public Transform EntryPoint => entryPoint;
        public Transform ExitPoint => exitPoint;
        public TrackCheckpoint Checkpoint => checkpoint;
    }
}

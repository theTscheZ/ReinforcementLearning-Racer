using UnityEngine;

namespace SpinMotion
{
    public class TrackSegment : MonoBehaviour
    {
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform exitPoint;

        public Transform EntryPoint => entryPoint;
        public Transform ExitPoint => exitPoint;
    }
}

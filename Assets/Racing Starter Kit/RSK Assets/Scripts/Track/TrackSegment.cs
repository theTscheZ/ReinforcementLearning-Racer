using UnityEngine;
using System.Collections.Generic;

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
        private readonly List<MiniCheckpoint> orderedMiniCheckpoints = new();
        private int nextMiniCheckpoint;

        public Transform CurrentTarget => nextMiniCheckpoint < orderedMiniCheckpoints.Count
            ? orderedMiniCheckpoints[nextMiniCheckpoint].transform : checkpoint != null ? checkpoint.transform : null;
        public bool AllMiniCheckpointsReached => nextMiniCheckpoint == orderedMiniCheckpoints.Count;

        public void ResetCheckpoints()
        {
            orderedMiniCheckpoints.Clear();
            var remaining = new List<MiniCheckpoint>(GetComponentsInChildren<MiniCheckpoint>(true));
            Vector3 cursor = entryPoint != null ? entryPoint.position : transform.position;
            while (remaining.Count > 0)
            {
                int closest = 0;
                for (int i = 1; i < remaining.Count; i++)
                    if ((remaining[i].transform.position - cursor).sqrMagnitude
                        < (remaining[closest].transform.position - cursor).sqrMagnitude) closest = i;
                MiniCheckpoint next = remaining[closest];
                next.ResetCheckpoint();
                orderedMiniCheckpoints.Add(next);
                cursor = next.transform.position;
                remaining.RemoveAt(closest);
            }
            nextMiniCheckpoint = 0;
        }

        public bool TryReachMiniCheckpoint(MiniCheckpoint reached)
        {
            if (nextMiniCheckpoint >= orderedMiniCheckpoints.Count
                || orderedMiniCheckpoints[nextMiniCheckpoint] != reached) return false;
            nextMiniCheckpoint++;
            return true;
        }
    }
}

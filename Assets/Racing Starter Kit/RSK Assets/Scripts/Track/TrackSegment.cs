using System.Collections.Generic;
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

        private readonly List<MiniCheckpoint> miniCheckpoints = new();
        private int nextMiniCheckpoint;

        public int MiniCheckpointCount => miniCheckpoints.Count;
        public MiniCheckpoint MiniCheckpointAt(int index) => miniCheckpoints[index];

        public MiniCheckpoint NextMiniCheckpoint => nextMiniCheckpoint < miniCheckpoints.Count
            ? miniCheckpoints[nextMiniCheckpoint] : null;

        public Transform NextTarget => NextMiniCheckpoint != null
            ? NextMiniCheckpoint.transform : checkpoint != null ? checkpoint.transform : null;

        public void ResetCheckpoints(Vector3 routeStart)
        {
            miniCheckpoints.Clear();
            miniCheckpoints.AddRange(GetComponentsInChildren<MiniCheckpoint>());
            nextMiniCheckpoint = 0;

            // Prefab sibling order is not route order (also supports reversed segments).
            // Follow the nearest remaining checkpoint from the segment entrance.
            for (int i = 0; i < miniCheckpoints.Count; i++)
            {
                int nearest = i;
                float distance = float.PositiveInfinity;
                for (int j = i; j < miniCheckpoints.Count; j++)
                {
                    Vector3 delta = miniCheckpoints[j].transform.position - routeStart;
                    delta.y = 0f;
                    if (delta.sqrMagnitude < distance)
                    {
                        distance = delta.sqrMagnitude;
                        nearest = j;
                    }
                }

                (miniCheckpoints[i], miniCheckpoints[nearest]) =
                    (miniCheckpoints[nearest], miniCheckpoints[i]);
                miniCheckpoints[i].ResetCheckpoint();
                routeStart = miniCheckpoints[i].transform.position;
            }
        }

        public bool TryCollectMiniCheckpoint(MiniCheckpoint reached)
        {
            if (reached == null || reached != NextMiniCheckpoint)
                return false;

            nextMiniCheckpoint++;
            return true;
        }
    }
}

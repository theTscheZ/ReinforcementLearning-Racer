using System.Collections.Generic;
using UnityEngine;

namespace SpinMotion
{
    public class EndlessTrackGenerator : MonoBehaviour
    {
        public Transform player;
        public Transform trackRoot;

        [Header("Track Segments")]
        public TrackSegment startSegmentPrefab;
        public List<TrackSegment> segmentPrefabs = new();
        [Min(2)] public int initialSegmentCount = 10;
        [Min(2)] public int maxActiveSegments = 14;
        [Min(1)] public int minSegmentsBeforeDespawn = 6;
        [Min(1)] public int maxPlacementAttempts = 20;

        [Header("Distances")]
        [Min(1f)] public float spawnDistanceToEnd = 80f;
        [Min(0f)] public float despawnBufferDistance = 10f;

        [Header("Randomness")]
        public bool useFixedSeed = false;
        public int seed = 0;

        private readonly List<TrackSegment> activeSegments = new();

        private Transform lastExit;
        private TrackSegment lastPlacedSegment;

        private void Start()
        {
            BuildInitialTrack();
        }

        private void Update()
        {
            if (player == null || lastExit == null || activeSegments.Count == 0)
            {
                return;
            }

            if (Vector3.Distance(player.position, lastExit.position) <= spawnDistanceToEnd)
            {
                AppendNextSegment();
            }

            TrimPassedSegments();
        }

        [ContextMenu("Rebuild Endless Track")]
        public void BuildInitialTrack()
        {
            if (player == null)
            {
                Debug.LogError("EndlessTrackGenerator requires a player Transform.");
                return;
            }

            if (startSegmentPrefab == null)
            {
                Debug.LogError("EndlessTrackGenerator requires a start segment prefab.");
                return;
            }

            if (segmentPrefabs.Count == 0)
            {
                Debug.LogError("EndlessTrackGenerator requires at least one segment prefab.");
                return;
            }

            if (maxActiveSegments < initialSegmentCount)
            {
                maxActiveSegments = initialSegmentCount;
            }

            if (minSegmentsBeforeDespawn >= maxActiveSegments)
            {
                minSegmentsBeforeDespawn = Mathf.Max(1, maxActiveSegments - 1);
            }

            if (trackRoot == null)
            {
                trackRoot = transform;
            }

            if (useFixedSeed)
            {
                Random.InitState(seed);
            }

            ClearTrack();

            var startSegment = Instantiate(startSegmentPrefab, trackRoot);
            startSegment.transform.SetPositionAndRotation(transform.position, transform.rotation);
            activeSegments.Add(startSegment);

            if (startSegment.ExitPoint == null)
            {
                Debug.LogError("Start segment is missing ExitPoint.");
                return;
            }

            lastPlacedSegment = startSegment;
            lastExit = startSegment.ExitPoint;

            while (activeSegments.Count < initialSegmentCount)
            {
                if (!AppendNextSegment())
                {
                    break;
                }
            }
        }

        private bool AppendNextSegment()
        {
            var segment = TryCreateNextSegment(lastExit, lastPlacedSegment);
            if (segment == null)
            {
                return false;
            }

            activeSegments.Add(segment);
            lastPlacedSegment = segment;
            lastExit = segment.ExitPoint;
            return true;
        }

        private TrackSegment TryCreateNextSegment(Transform previousExit, TrackSegment previousSegment)
        {
            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                var prefab = segmentPrefabs[Random.Range(0, segmentPrefabs.Count)];
                var candidate = Instantiate(prefab, trackRoot);

                if (!TryAlign(candidate, previousExit))
                {
                    DisposeSegment(candidate);
                    continue;
                }

                if (OverlapsExistingSegments(candidate, previousSegment))
                {
                    DisposeSegment(candidate);
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private bool TryAlign(TrackSegment segment, Transform previousExit)
        {
            if (segment.EntryPoint == null || segment.ExitPoint == null)
            {
                Debug.LogError($"Segment '{segment.name}' is missing EntryPoint or ExitPoint.");
                return false;
            }

            var rotationDelta = Quaternion.FromToRotation(segment.EntryPoint.forward, previousExit.forward);
            segment.transform.rotation = rotationDelta * segment.transform.rotation;

            var positionDelta = previousExit.position - segment.EntryPoint.position;
            segment.transform.position += positionDelta;
            return true;
        }

        private bool OverlapsExistingSegments(TrackSegment candidate, TrackSegment previousSegment)
        {
            var candidateColliders = candidate.GetComponentsInChildren<Collider>();
            for (int i = 0; i < activeSegments.Count; i++)
            {
                var existing = activeSegments[i];
                if (existing == null || existing == previousSegment)
                {
                    continue;
                }

                var existingColliders = existing.GetComponentsInChildren<Collider>();
                for (int c = 0; c < candidateColliders.Length; c++)
                {
                    if (!candidateColliders[c].enabled || candidateColliders[c].isTrigger)
                    {
                        continue;
                    }

                    var candidateBounds = candidateColliders[c].bounds;
                    for (int e = 0; e < existingColliders.Length; e++)
                    {
                        if (!existingColliders[e].enabled || existingColliders[e].isTrigger)
                        {
                            continue;
                        }

                        if (candidateBounds.Intersects(existingColliders[e].bounds))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void TrimPassedSegments()
        {
            while (activeSegments.Count > maxActiveSegments && activeSegments.Count > minSegmentsBeforeDespawn)
            {
                var oldestSegment = activeSegments[0];
                if (oldestSegment == null || oldestSegment.ExitPoint == null)
                {
                    activeSegments.RemoveAt(0);
                    continue;
                }

                var toPlayer = player.position - oldestSegment.ExitPoint.position;
                var playerPastExit = Vector3.Dot(oldestSegment.ExitPoint.forward, toPlayer) > 0f;
                if (!playerPastExit || toPlayer.magnitude < despawnBufferDistance)
                {
                    break;
                }

                activeSegments.RemoveAt(0);
                DisposeSegment(oldestSegment);
            }
        }

        private void ClearTrack()
        {
            for (int i = activeSegments.Count - 1; i >= 0; i--)
            {
                if (activeSegments[i] != null)
                {
                    DisposeSegment(activeSegments[i]);
                }
            }

            activeSegments.Clear();
            lastExit = null;
            lastPlacedSegment = null;
        }

        private void DisposeSegment(TrackSegment segment)
        {
            segment.transform.SetParent(null);
            Destroy(segment.gameObject);
        }
    }
}

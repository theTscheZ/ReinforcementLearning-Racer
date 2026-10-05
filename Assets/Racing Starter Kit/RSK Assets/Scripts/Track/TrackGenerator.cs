using System.Collections.Generic;
using UnityEngine;

namespace SpinMotion
{
    public class TrackGenerator : MonoBehaviour
    {
        public GameEvents gameEvents;
        public Checkpoints checkpoints;
        public AIWaypoints aiWaypoints;
        public Transform trackRoot;

        [Header("Track Segments")]
        public TrackSegment startSegmentPrefab;
        public List<TrackSegment> segmentPrefabs = new();
        [Min(1)] public int segmentCount = 12;
        [Min(1)] public int maxPlacementAttempts = 20;
        public bool regenerateOnRestart = false;

        [Header("Randomness")]
        public bool useFixedSeed = false;
        public int seed = 0;

        private readonly List<TrackSegment> spawnedSegments = new();

        private void Awake()
        {
            if (trackRoot == null)
            {
                trackRoot = transform;
            }

            if (regenerateOnRestart && gameEvents != null)
            {
                gameEvents.RestartRaceEvent.AddListener(GenerateTrack);
            }
        }

        private void Start()
        {
            GenerateTrack();
        }

        public void GenerateTrack()
        {
            if (startSegmentPrefab == null)
            {
                Debug.LogError("TrackGenerator requires a start segment prefab.");
                return;
            }

            if (segmentPrefabs.Count == 0)
            {
                Debug.LogError("TrackGenerator requires at least one segment prefab.");
                return;
            }

            if (useFixedSeed)
            {
                Random.InitState(seed);
            }

            ClearTrack();

            var startSegment = Instantiate(startSegmentPrefab, trackRoot);
            startSegment.transform.SetPositionAndRotation(transform.position, transform.rotation);
            spawnedSegments.Add(startSegment);

            if (startSegment.ExitPoint == null)
            {
                Debug.LogError("Start segment is missing ExitPoint.");
                RebuildTrackConsumers();
                return;
            }

            var lastExit = startSegment.ExitPoint;
            var previousSegment = startSegment;

            for (int i = 0; i < segmentCount; i++)
            {
                var segment = TryCreateNextSegment(lastExit, previousSegment);
                if (segment == null)
                {
                    Debug.LogWarning($"Track generation stopped early at segment {i + 1}/{segmentCount}.");
                    break;
                }

                spawnedSegments.Add(segment);
                lastExit = segment.ExitPoint;
                previousSegment = segment;
            }

            RebuildTrackConsumers();
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

            for (int i = 0; i < spawnedSegments.Count; i++)
            {
                var existing = spawnedSegments[i];
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

        private void ClearTrack()
        {
            for (int i = spawnedSegments.Count - 1; i >= 0; i--)
            {
                if (spawnedSegments[i] != null)
                {
                    DisposeSegment(spawnedSegments[i]);
                }
            }

            spawnedSegments.Clear();
        }

        private void DisposeSegment(TrackSegment segment)
        {
            segment.transform.SetParent(null);
            Destroy(segment.gameObject);
        }

        private void RebuildTrackConsumers()
        {
            if (checkpoints != null)
            {
                checkpoints.RebuildCheckpoints();
            }

            if (aiWaypoints != null)
            {
                aiWaypoints.RebuildWaypoints();
            }
        }
    }
}

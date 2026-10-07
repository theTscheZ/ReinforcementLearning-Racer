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
        // [Min(1)] public int maxPlacementAttempts = 20;
        [SerializeField] private CheckpointTimer checkpointTimer;

        // [Header("Distances")]
        // [Min(1f)] public float spawnDistanceToEnd = 80f;
        // [Min(0f)] public float despawnBufferDistance = 10f;
        //
        // [Header("Spawning")]
        // [Min(1)] public int maxSpawnsPerFrame = 3;
        // [Min(0f)] public float retryDelayAfterFailure = 1f;

        [Header("Randomness")]
        public bool useFixedSeed = false;
        public int seed = 0;

        [Header("Debug")]
        public bool verboseLogging = false;

        private readonly List<TrackSegment> activeSegments = new();
        private readonly List<int> prefabOrder = new();

        private Transform lastExit;
        private TrackSegment lastPlacedSegment;
        private float nextSpawnAllowedTime;

        private void Start()
        {
            BuildInitialTrack();
        }

        // private void Update()
        // {
        // }

        [ContextMenu("Rebuild Endless Track")]
        public void BuildInitialTrack()
        {
            if (player == null)
            {
                Debug.LogError("EndlessTrackGenerator requires a player Transform.");
                return;
            }

            // Von Hand platziertes Startsegment (Szene) suchen; sonst Prefab-Asset als Fallback.
            TrackSegment sceneStart = FindSceneStartSegment();
            bool prefabAssetUsable =
                startSegmentPrefab != null &&
                !startSegmentPrefab.gameObject.scene.IsValid() &&
                startSegmentPrefab.ExitPoint != null;

            if (sceneStart == null && !prefabAssetUsable)
            {
                Debug.LogError(
                    "No start segment: place a TrackSegment (with ExitPoint) in the scene " +
                    "or assign a start segment prefab asset."
                );
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

            if (trackRoot == null)
            {
                trackRoot = transform;
            }

            // Mindestens ein gültiges Prefab nötig (nicht null, Entry + Exit vorhanden).
            BuildShuffledPrefabOrder();
            if (prefabOrder.Count == 0)
            {
                Debug.LogError("No valid segment prefab found (needs EntryPoint and ExitPoint).");
                return;
            }

            if (useFixedSeed)
            {
                Random.InitState(seed);
            }

            ClearTrack();
            nextSpawnAllowedTime = 0f;

            TrackSegment startSegment;
            if (sceneStart != null)
            {
                // Von Hand platziertes Segment: an seiner Position lassen, NICHT klonen.
                // Es wird wie ein generiertes Segment geführt und später auch weggetrimmt.
                startSegment = sceneStart;
                startSegment.gameObject.SetActive(true);
            }
            else
            {
                startSegment = Instantiate(startSegmentPrefab, trackRoot);
                startSegment.transform.SetPositionAndRotation(transform.position, transform.rotation);
            }

            activeSegments.Add(startSegment);
            InitializeSegment(startSegment);

            // Collider-Bounds sind sonst noch auf der Prefab-Position (Physics synchronisiert erst später).
            Physics.SyncTransforms();

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

        // Sucht das Startsegment, das schon in der Szene liegt:
        // 1) ein im Feld zugewiesenes Szenenobjekt, sonst
        // 2) automatisch das TrackSegment in der Szene, das dem Generator am nächsten liegt.
        private TrackSegment FindSceneStartSegment()
        {
            if (startSegmentPrefab != null && startSegmentPrefab.gameObject.scene.IsValid())
            {
                return IsUsableStart(startSegmentPrefab) ? startSegmentPrefab : null;
            }

            var found = FindObjectsByType<TrackSegment>(FindObjectsSortMode.None);
            TrackSegment best = null;
            float bestSqrDistance = float.MaxValue;
            int candidates = 0;

            for (int i = 0; i < found.Length; i++)
            {
                if (!IsUsableStart(found[i]))
                {
                    continue;
                }

                candidates++;
                float sqrDistance = (found[i].transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = found[i];
                }
            }

            if (candidates > 1)
            {
                Debug.LogWarning(
                    $"[TRACK] {candidates} TrackSegments in scene, using the closest one to the generator: {best.name}"
                );
            }

            return best;
        }

        private bool IsUsableStart(TrackSegment segment)
        {
            return segment != null
                   && segment.ExitPoint != null
                   && !activeSegments.Contains(segment);
        }
        
        private bool AppendNextSegment()
        {
            var segment = TryCreateNextSegment(
                lastExit,
                lastPlacedSegment
            );

            if (segment == null)
            {
                return false;
            }

            activeSegments.Add(segment);

            lastPlacedSegment = segment;
            lastExit = segment.ExitPoint;

            InitializeSegment(segment);

            return true;
        }

        private TrackSegment TryCreateNextSegment(
            Transform previousExit,
            TrackSegment previousSegment)
        {
            // Jedes Prefab höchstens einmal pro Versuchsrunde probieren: Die Ausrichtung ist
            // deterministisch, ein erneuter Versuch mit demselben Prefab liefert dasselbe Ergebnis.
            BuildShuffledPrefabOrder();
            int attempts = prefabOrder.Count;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var prefab = segmentPrefabs[prefabOrder[attempt]];

                if (verboseLogging)
                {
                    Debug.Log($"[TRACK] Attempt {attempt + 1}/{attempts}: {prefab.name}");
                }

                var candidate = Instantiate(prefab, trackRoot);
                //weil aus irgendeinem grund manchmal deaktiviert gespawnt wird
                candidate.gameObject.SetActive(true);

                if (!TryAlign(candidate, previousExit))
                {
                    Debug.LogWarning($"[TRACK] REJECTED {prefab.name}: alignment failed");
                    DisposeSegment(candidate);
                    continue;
                }

                // Ohne Sync wären die Collider-Bounds noch an der alten Position -> falsche Overlap-Ergebnisse.
                Physics.SyncTransforms();

                if (OverlapsExistingSegments(candidate, previousSegment))
                {
                    if (verboseLogging)
                    {
                        Debug.LogWarning($"[TRACK] REJECTED {prefab.name}: overlap");
                    }

                    DisposeSegment(candidate);
                    continue;
                }

                if (verboseLogging)
                {
                    Debug.Log($"[TRACK] ACCEPTED {prefab.name}");
                }

                return candidate;
            }

            Debug.LogError($"[TRACK] FAILED: all {attempts} attempts rejected.");

            // Spieler steht auf dem aktuell letzten Segment?
            if (activeSegments.Count >= 2 &&
                IsPlayerOnLastSegment())
            {
                var segmentToRemove =
                    activeSegments[^2];

                Debug.LogWarning(
                    $"[TRACK] Removing previous segment '{segmentToRemove.name}' and retrying."
                );

                activeSegments.RemoveAt(activeSegments.Count - 2);

                DisposeSegment(segmentToRemove);

                Physics.SyncTransforms();

                return TryCreateNextSegment(
                    previousExit,
                    previousSegment
                );
            }

            return null;
        }
        
        private bool IsPlayerOnLastSegment()
        {
            if (activeSegments.Count == 0)
                return false;

            var lastSegment = activeSegments[^1];

            var colliders =
                lastSegment.GetComponentsInChildren<Collider>();

            foreach (var col in colliders)
            {
                if (col.bounds.Contains(player.position))
                    return true;
            }

            return false;
        }

        private void BuildShuffledPrefabOrder()
        {
            prefabOrder.Clear();

            for (int i = 0; i < segmentPrefabs.Count; i++)
            {
                var prefab = segmentPrefabs[i];
                if (prefab != null && prefab.EntryPoint != null && prefab.ExitPoint != null)
                {
                    prefabOrder.Add(i);
                }
            }

            // Fisher-Yates
            for (int i = prefabOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (prefabOrder[i], prefabOrder[j]) = (prefabOrder[j], prefabOrder[i]);
            }
        }

        private bool TryAlign(
            TrackSegment segment,
            Transform previousExit)
        {
            if (segment.EntryPoint == null ||
                segment.ExitPoint == null)
            {
                Debug.LogError(
                    $"Segment '{segment.name}' is missing EntryPoint or ExitPoint."
                );

                return false;
            }

            Transform entry = segment.EntryPoint;

            // Entry soll dieselbe Vorwärtsrichtung wie der
            // vorherige Exit haben, aber immer mit Y-Up.
            Quaternion targetRotation = Quaternion.LookRotation(
                previousExit.forward,
                Vector3.up
            );

            Quaternion entryRotation = Quaternion.LookRotation(
                entry.forward,
                Vector3.up
            );

            segment.transform.rotation =
                targetRotation *
                Quaternion.Inverse(entryRotation) *
                segment.transform.rotation;

            // Entry exakt auf Exit verschieben.
            Vector3 positionDelta =
                previousExit.position -
                segment.EntryPoint.position;

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
        
        private void RemoveSegmentsBefore(TrackSegment reachedSegment)
        {
            int reachedIndex = activeSegments.IndexOf(reachedSegment);

            if (reachedIndex <= 0)
            {
                return;
            }

            for (int i = reachedIndex - 1; i >= 0; i--)
            {
                TrackSegment segment = activeSegments[i];

                activeSegments.RemoveAt(i);
                DisposeSegment(segment);
            }
        }
        
        private void EnsureTrackAhead()
        {
            while (activeSegments.Count < maxActiveSegments)
            {
                if (!AppendNextSegment())
                {
                    Debug.LogWarning(
                        "[TRACK] Could not fill track to maxActiveSegments."
                    );

                    break;
                }
            }
        }
        
        private void InitializeSegment(TrackSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            if (segment.Checkpoint == null)
            {
                Debug.LogError(
                    $"Segment '{segment.name}' has no checkpoint assigned."
                );

                return;
            }

            segment.Checkpoint.Initialize(this, segment);
        }

        private void ClearTrack()
        {
            for (int i = activeSegments.Count - 1; i >= 0; i--)
            {
                DisposeSegment(activeSegments[i]);
            }

            activeSegments.Clear();
            lastExit = null;
            lastPlacedSegment = null;
        }

        private void DisposeSegment(TrackSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            // Destroy wirkt erst am Frame-Ende: sofort deaktivieren, damit
            // Collider des verworfenen Segments nicht mehr im Spiel "herumstehen".
            segment.gameObject.SetActive(false);
            segment.transform.SetParent(null);
            Destroy(segment.gameObject);
        }
        
        public void CheckpointReached(TrackSegment reachedSegment)
        {
            if (reachedSegment == null)
            {
                return;
            }
            
            float segmentTime = checkpointTimer.CompleteSegment();

            Debug.Log(
                $"Checkpoint reached! Segment time: {segmentTime:F2}s"
            );

            int reachedIndex = activeSegments.IndexOf(reachedSegment);

            if (reachedIndex < 0)
            {
                Debug.LogWarning(
                    $"[TRACK] Checkpoint reached for unknown segment '{reachedSegment.name}'."
                );

                return;
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[TRACK] Checkpoint reached: {reachedSegment.name}"
                );
            }

            RemoveSegmentsBefore(reachedSegment);

            EnsureTrackAhead();
        }
    }
}
using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

namespace SpinMotion
{
    [RequireComponent(typeof(CarController), typeof(Rigidbody))]
    public class CarAgent : Agent
    {
        // Keep the Inspector observation size in sync with CollectObservations.
        public const int ObservationCount = 18;
        public const int CompactObservationCount = ObservationCount;
        public const string TrafficSensorName = "TrafficSensor";
        [SerializeField] private CarController car;
        [SerializeField] private EndlessTrackGenerator trackGenerator;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private RaceTimer raceTimer;
        [SerializeField] private CheckpointTimer checkpointTimer;

        [Header("Rewards")]
        [SerializeField] private float checkpointReward = 0.5f;
        [Min(0.02f), SerializeField] private float minimumCheckpointSeconds = 0.1f;
        [Min(0f), SerializeField] private float segmentReward = 0.75f;
        [SerializeField] private float progressRewardPerMetre = 0.03f;
        [Min(0f), SerializeField] private float forwardDistanceRewardPerMetre = 0.06f;
        [Min(0f), SerializeField] private float forwardSpeedBonusPerMetre = 0.04f;
        [Min(0.1f), SerializeField] private float speedRewardReference = 20f;

        [Header("Race opponent")]
        [SerializeField] private bool spawnOpponent = true;
        [SerializeField] private ModelAsset opponentModel;
        [SerializeField] private Vector3 opponentSpawnOffset = new Vector3(2.5f, 0f, 0f);
        [SerializeField] private bool randomizeOpponentStart;
        [SerializeField] private Vector2 opponentForwardRange = new Vector2(0f, 6f);
        [SerializeField] private bool isOpponent;
        [Header("Opponent training presence")]
        [SerializeField] private bool maintainOpponentPresence = true;
        [Min(8f), SerializeField] private float opponentMaxDistance = 15f;
        [Min(0.1f), SerializeField] private float opponentRespawnDelay = 0.5f;
        [Min(0.1f), SerializeField] private float opponentRepositionCooldown = 1f;
        private float opponentFailureTime;
        private float nextOpponentRejoin;
        private float nextOpponentSample;
        private int opponentRejoins;
        private int opponentFailureRejoins;
        private int opponentDistanceRejoins;
        private bool pendingRejoin;
        private Vector3 rejoinPosition;
        private Quaternion rejoinRotation;
        private TrackSegment rejoinSegment;
        private int rejoinMini;
        private CarAgent opponent;
        public bool IsOpponent => isOpponent;
        public bool IsRacing => !episodeEnding && gameObject.activeInHierarchy;
        public float SegmentElapsed => segmentElapsed;

        // Set by the Racer menu; sensor settings themselves are visible in the scene.
        [HideInInspector, SerializeField] private bool compactTrainingSensors;
        private float segmentElapsed;
        private float totalCheckpointSeconds;
        private float checkpointRewards;

        [Header("Recovery and road sensing")]
        [SerializeField] private float stuckTimeLimit = 8f;
        [SerializeField] private float roadProbeSpacing = 2f;
        [SerializeField] private float roadProbeLookAhead = 4f;
        [SerializeField] private bool verboseLogging;

        private readonly RaycastHit[] roadHits = new RaycastHit[16];
        private Rigidbody rb;
        private WheelCollider[] wheels;
        private Transform progressTarget;
        private Vector3 lastPosition;
        private float bestTargetDistance;
        private float timeSinceLastMiniCheckpoint;
        private float timeWithoutProgress;
        private float progressSinceStuckReset;
        private int miniCheckpointsReached;
        private int segmentsReached;
        private float progressReward;
        private Vector3 routeOrigin;
        private Vector3 routeDirection;
        private float routeDistance;
        private float bestForwardExtent;
        private float forwardMetres;
        private float forwardDistanceReward;
        private float forwardSpeedReward;
        private float roadContactSum;
        private int roadContactSamples;
        private bool episodeEnding;

        public override void Initialize()
        {
            if (car == null)
                car = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<WheelCollider>();

            if (spawnPoint == null || trackGenerator == null)
                Debug.LogError("CarAgent requires Spawn Point and Track Generator.", this);
        }

        protected override void OnEnable()
        {
            var traffic = transform.Find(TrafficSensorName);
            if (traffic != null)
                traffic.gameObject.SetActive(compactTrainingSensors && !isOpponent);
            // Tags belong to hit colliders, not just their parent GameObject.
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || collider is WheelCollider)
                    continue;
                collider.gameObject.tag = "Car";
                collider.gameObject.layer = LayerMask.NameToLayer(isOpponent ? "AIPlayer" : "Player");
            }
            // Old ONNX cannot consume a second ray sensor. Never silently run it with new inputs.
            var behavior = GetComponent<BehaviorParameters>();
            if (compactTrainingSensors && !isOpponent && behavior.Model != null && behavior.Model == opponentModel)
            {
                behavior.Model = null;
                behavior.BehaviorType = BehaviorType.Default;
                Debug.LogWarning("Removed the incompatible legacy model. Start the trainer or use Racer > Race two trained models.", this);
            }
            base.OnEnable();
        }

        private void FixedUpdate()
        {
            if (episodeEnding || rb == null || spawnPoint == null)
                return;

            float dt = Time.fixedDeltaTime;
            timeSinceLastMiniCheckpoint += dt;
            segmentElapsed += dt;
            if (isOpponent && segmentElapsed >= 20f)
            {
                FailEpisode("Timeout");
                return;
            }
            timeWithoutProgress += dt;

            if (transform.position.y < spawnPoint.position.y - 2f)
            {
                FailEpisode("Fall");
                return;
            }

            if (!isOpponent)
                MaintainOpponent();

            Transform target = trackGenerator != null ? trackGenerator.GetTarget(this) : null;
            if (target != progressTarget)
                ResetProgressTarget(target);

            float groundedFraction = GroundedRoadFraction();
            roadContactSum += groundedFraction;
            roadContactSamples++;
            float forwardProgress = AwardForwardTravel(transform.position - lastPosition, groundedFraction);
            float targetProgress = 0f;

            // Only pay for new closest approach on the road. Reversing and driving
            // forward again cannot repeatedly earn reward for the same metres.
            if (target != null && groundedFraction >= 0.75f)
            {
                float distance = FlatDistance(transform.position, target.position);
                float progress = Mathf.Min(Mathf.Max(0f, bestTargetDistance - distance),
                    Vector3.Distance(lastPosition, transform.position));
                if (progress > 0f)
                {
                    float reward = progress * Mathf.Max(0f, progressRewardPerMetre);
                    AddReward(reward);
                    progressReward += reward;
                    targetProgress = progress;
                    bestTargetDistance = distance;
                }
            }

            // Count the same physical movement only once for stuck recovery.
            progressSinceStuckReset += Mathf.Max(forwardProgress, targetProgress);
            if (progressSinceStuckReset >= 0.25f)
            {
                progressSinceStuckReset = 0f;
                timeWithoutProgress = 0f;
            }
            lastPosition = transform.position;
            if (timeWithoutProgress >= stuckTimeLimit)
                FailEpisode("Stuck");
        }

        private float AwardForwardTravel(Vector3 displacement, float groundedFraction)
        {
            if (progressTarget == null || routeDistance <= 0f)
                return 0f;
            displacement.y = 0f;
            float extent = Mathf.Clamp(Vector3.Dot(transform.position - routeOrigin, routeDirection), 0f, routeDistance);
            float newExtent = Mathf.Max(0f, extent - bestForwardExtent);
            // Advance even off-road or while facing backwards: those metres cannot be
            // reclaimed later. The fixed route axis prevents farming circles or reversing.
            bestForwardExtent = Mathf.Max(bestForwardExtent, extent);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float forwardMotion = Mathf.Max(0f, Vector3.Dot(displacement, forward));
            float metres = Mathf.Min(newExtent, forwardMotion);
            if (metres <= 0f || groundedFraction < 0.5f || !HasRoadAt(transform.position))
                return 0f;

            float speed = Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, forward));
            float distanceReward = metres * Mathf.Max(0f, forwardDistanceRewardPerMetre);
            float speedBonus = metres * Mathf.Max(0f, forwardSpeedBonusPerMetre)
                * Mathf.Clamp01(speed / Mathf.Max(0.1f, speedRewardReference));
            AddReward(distanceReward + speedBonus);
            forwardMetres += metres;
            forwardDistanceReward += distanceReward;
            forwardSpeedReward += speedBonus;
            return metres;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 velocity = transform.InverseTransformDirection(rb.linearVelocity);
            sensor.AddObservation(Mathf.Clamp(velocity.z / 25f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(velocity.x / 10f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(velocity.y / 10f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(rb.angularVelocity.y / 5f, -1f, 1f));

            Transform target = trackGenerator != null ? trackGenerator.GetTarget(this) : null;
            Vector3 toTarget = target != null ? target.position - transform.position : Vector3.zero;
            toTarget.y = 0f;
            Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);
            sensor.AddObservation(localDirection.x);
            sensor.AddObservation(localDirection.z);
            sensor.AddObservation(Mathf.Clamp01(toTarget.magnitude / 50f));
            sensor.AddObservation(checkpointTimer != null ? checkpointTimer.RemainingFraction
                : Mathf.Clamp01(1f - segmentElapsed / 20f));
            sensor.AddObservation(GroundedRoadFraction());

            // A near-field 3x3 footprint complements the existing distant ray fan.
            // Each value means actual road under that point, excluding triggers/the car.
            for (int row = 0; row < 3; row++)
                for (int column = -1; column <= 1; column++)
                    sensor.AddObservation(HasRoadAt(transform.TransformPoint(
                        new Vector3(column * roadProbeSpacing, 0f, row * roadProbeLookAhead))));
        }

        public bool HasRoadAt(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 1.5f,
                Vector3.down, roadHits, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider ground = null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = roadHits[i].collider;
                if (hit.attachedRigidbody == rb || hit.transform.IsChildOf(transform))
                    continue;
                if (roadHits[i].distance < nearest)
                {
                    nearest = roadHits[i].distance;
                    ground = hit;
                }
            }
            return ground != null && ground.CompareTag("Road");
        }

        private float GroundedRoadFraction()
        {
            int grounded = 0;
            foreach (WheelCollider wheel in wheels)
                if (wheel.GetGroundHit(out WheelHit hit) && hit.collider.CompareTag("Road"))
                    grounded++;
            return wheels.Length > 0 ? (float)grounded / wheels.Length : 0f;
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (episodeEnding)
                return;
            float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            // Two actions remain: steering and signed gas/brake (reverse at low speed).
            float drive = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            car.Move(steering, Mathf.Max(0f, drive), Mathf.Min(0f, drive), 0f);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.ContinuousActions;
            actions[0] = Input.GetAxis("Horizontal");
            actions[1] = Input.GetAxis("Vertical");
        }

        public override void OnEpisodeBegin()
        {
            episodeEnding = false;
            if (rb == null || spawnPoint == null || trackGenerator == null)
                return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            transform.SetPositionAndRotation(pendingRejoin ? rejoinPosition : GetSpawnPosition(),
                pendingRejoin ? rejoinRotation : spawnPoint.rotation);
            car.ResetControlState();
            if (isOpponent && !pendingRejoin)
                SeparateFromPlayer();
            if (!isOpponent)
                trackGenerator.ResetTrack();
            else if (pendingRejoin)
                trackGenerator.RegisterRacerAt(this, rejoinSegment, rejoinMini);
            else
                trackGenerator.RegisterRacer(this);
            Physics.SyncTransforms();
            if (raceTimer != null)
                raceTimer.ResetTimer();
            if (checkpointTimer != null)
                checkpointTimer.StartTimer();

            timeSinceLastMiniCheckpoint = 0f;
            segmentElapsed = 0f;
            totalCheckpointSeconds = 0f;
            checkpointRewards = 0f;
            timeWithoutProgress = 0f;
            progressSinceStuckReset = 0f;
            miniCheckpointsReached = 0;
            segmentsReached = 0;
            progressReward = 0f;
            forwardMetres = 0f;
            forwardDistanceReward = 0f;
            forwardSpeedReward = 0f;
            roadContactSum = 0f;
            roadContactSamples = 0;
            ResetProgressTarget(trackGenerator.GetTarget(this));
            if (!isOpponent)
            {
                opponentRejoins = opponentFailureRejoins = opponentDistanceRejoins = 0;
                nextOpponentRejoin = Time.time + opponentRepositionCooldown;
                nextOpponentSample = Time.time;
                RecordParameters();
                if (spawnOpponent)
                {
                    if (opponent == null)
                        CreateOpponent();
                    else
                    {
                        opponent.gameObject.SetActive(true);
                        opponent.EndEpisode();
                    }
                }
                else if (opponent != null)
                {
                    trackGenerator.RemoveRacer(opponent);
                    opponent.gameObject.SetActive(false);
                }
            }
        }

        private void MaintainOpponent()
        {
            if (!spawnOpponent || !maintainOpponentPresence || trackGenerator == null)
                return;
            if (opponent == null)
                CreateOpponent();
            if (opponent == null)
                return;

            bool active = opponent.IsRacing;
            float distance = FlatDistance(transform.position, opponent.transform.position);
            if (Time.time >= nextOpponentSample)
            {
                var stats = Academy.Instance.StatsRecorder;
                stats.Add("Opponent/Within Range", active && distance <= opponentMaxDistance ? 1f : 0f);
                stats.Add("Opponent/Detected", active && OpponentDetected() ? 1f : 0f);
                nextOpponentSample = Time.time + 1f;
            }
            if (active && distance <= opponentMaxDistance)
                return;
            if (Time.time < nextOpponentRejoin || (!active && Time.time - opponent.opponentFailureTime < opponentRespawnDelay))
                return;
            if (GroundedRoadFraction() < 0.5f)
                return;
            bool rejoined = TryRejoinOpponent();
            Academy.Instance.StatsRecorder.Add("Opponent/Placement Unavailable", rejoined ? 0f : 1f);
            nextOpponentRejoin = Time.time + (rejoined ? opponentRepositionCooldown : 0.25f);
            if (rejoined)
            {
                opponentRejoins++;
                if (active) opponentDistanceRejoins++; else opponentFailureRejoins++;
            }
        }

        private bool OpponentDetected()
        {
            var traffic = transform.Find(TrafficSensorName)?.GetComponent<RayPerceptionSensorComponent3D>();
            var outputs = traffic?.RaySensor?.RayPerceptionOutput?.RayOutputs;
            if (outputs == null)
                return false;
            foreach (var output in outputs)
                if (output.HitTaggedObject && output.HitGameObject != null && output.HitGameObject.activeInHierarchy)
                    return true;
            return false;
        }

        private bool TryRejoinOpponent()
        {
            if (!trackGenerator.TryRoadHeight(transform.position, out float playerGround))
                return false;
            float bodyHeight = transform.position.y - playerGround + 0.05f;
            var ownBodies = opponent.GetComponentsInChildren<Collider>(true);
            var playerBodies = GetComponentsInChildren<Collider>();
            // Search along the next waypoints and across the road centre. Never
            // use the global start or pay the learner for skipped checkpoints.
            for (float ahead = 6f; ahead <= 12f; ahead += 2f)
            {
                if (!trackGenerator.TryGetRejoinPose(this, ahead, out Vector3 routePosition,
                    out Quaternion rotation, out TrackSegment segment, out int mini))
                    continue;
                for (int lane = 0; lane < 3; lane++)
                {
                    float lateral = lane == 0 ? 0f : lane == 1 ? 1f : -1f;
                    Vector3 position = routePosition + rotation * Vector3.right * lateral;
                    if (!trackGenerator.HasRoadFootprint(position, rotation, out float roadHeight))
                        continue;
                    position.y = roadHeight + bodyHeight;
                    if (FlatDistance(transform.position, position) < 4f || FlatDistance(transform.position, position) > opponentMaxDistance)
                        continue;
                    Quaternion delta = rotation * Quaternion.Inverse(opponent.transform.rotation);
                    bool overlap = false;
                    foreach (var own in ownBodies)
                        foreach (var playerBody in playerBodies)
                        {
                            if (own.isTrigger || playerBody.isTrigger || own is WheelCollider || playerBody is WheelCollider)
                                continue;
                            Vector3 bodyPosition = position + delta * (own.transform.position - opponent.transform.position);
                            if (Physics.ComputePenetration(own, bodyPosition, delta * own.transform.rotation,
                                playerBody, playerBody.transform.position, playerBody.transform.rotation, out _, out _))
                                overlap = true;
                        }
                    if (overlap)
                        continue;
                    opponent.pendingRejoin = true;
                    opponent.rejoinPosition = position;
                    opponent.rejoinRotation = rotation;
                    opponent.rejoinSegment = segment;
                    opponent.rejoinMini = mini;
                    // OnEnable and EndEpisode may both invoke OnEpisodeBegin;
                    // leave the pending pose installed until both have finished.
                    opponent.transform.SetPositionAndRotation(position, rotation);
                    opponent.gameObject.SetActive(true);
                    opponent.EndEpisode();
                    opponent.pendingRejoin = false;
                    float matchingSpeed = Mathf.Clamp(Vector3.Dot(rb.linearVelocity, rotation * Vector3.forward), 0f, 25f);
                    opponent.rb.linearVelocity = rotation * Vector3.forward * matchingSpeed;
                    Physics.SyncTransforms();
                    return true;
                }
            }
            return false;
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 offset = opponentSpawnOffset;
            if (isOpponent && randomizeOpponentStart)
            {
                offset.x = Mathf.Abs(offset.x) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                // Keep the opponent on the start segment; never spawn before its entrance.
                offset.z = UnityEngine.Random.Range(Mathf.Max(0f, opponentForwardRange.x),
                    Mathf.Max(0f, opponentForwardRange.x, opponentForwardRange.y));
            }
            return spawnPoint.position + (isOpponent ? spawnPoint.TransformDirection(offset) : Vector3.zero);
        }

        private void SeparateFromPlayer()
        {
            if (trackGenerator.player == null)
                return;
            var ownColliders = GetComponentsInChildren<Collider>();
            var playerColliders = trackGenerator.player.GetComponentsInChildren<Collider>();
            // Safety margin for differently scaled car bodies or edited starting offsets.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                bool overlapping = false;
                foreach (var own in ownColliders)
                    foreach (var other in playerColliders)
                    {
                        if (own.isTrigger || other.isTrigger || own is WheelCollider || other is WheelCollider)
                            continue;
                        if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out _))
                            overlapping = true;
                    }
                if (!overlapping)
                    return;
                transform.position += spawnPoint.forward;
            }
            Debug.LogError("Could not separate opponent spawn from the player. Check the start grid.", this);
        }

        private void CreateOpponent()
        {
            if (opponentModel == null)
            {
                Debug.LogError("Assign the trained legacy model as Opponent Model.", this);
                return;
            }
            // Keep the clone inactive until its policy and role are configured.
            var staging = new GameObject("Opponent setup");
            staging.SetActive(false);
            var clone = Instantiate(gameObject, staging.transform);
            clone.SetActive(false);
            opponent = clone.GetComponent<CarAgent>();
            opponent.isOpponent = true;
            opponent.spawnOpponent = false;
            opponent.compactTrainingSensors = false;
            opponent.car = clone.GetComponent<CarController>();
            opponent.raceTimer = null;
            opponent.checkpointTimer = null;
            var roadSensor = clone.GetComponent<RayPerceptionSensorComponent3D>();
            roadSensor.RaysPerDirection = 35;
            clone.transform.Find(TrafficSensorName)?.gameObject.SetActive(false);
            var behavior = clone.GetComponent<BehaviorParameters>();
            behavior.BrainParameters.VectorObservationSize = ObservationCount;
            behavior.BehaviorName = "RacerOpponent";
            behavior.Model = opponentModel;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            foreach (var camera in clone.GetComponentsInChildren<Camera>(true))
                camera.enabled = false;
            foreach (var listener in clone.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
            clone.name = "Trained model opponent";
            clone.transform.SetParent(transform.parent);
            // Agent's first OnEpisodeBegin can be deferred until the next Academy reset.
            // Place the inactive body BEFORE activation, so it never occupies the player's pose.
            clone.transform.SetPositionAndRotation(opponent.GetSpawnPosition(), spawnPoint.rotation);
            clone.SetActive(true);
            opponent.OnEpisodeBegin();
            Physics.SyncTransforms();
            foreach (var paint in clone.GetComponentsInChildren<CarColor>(true))
                if (paint.TryGetComponent<Renderer>(out var renderer))
                    renderer.material.color = new Color(1f, 0.25f, 0.05f);
            Destroy(staging);
        }

        private void RecordParameters()
        {
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Parameters/Checkpoint Reward", checkpointReward);
            stats.Add("Parameters/Segment Reward", segmentReward);
            stats.Add("Parameters/Minimum Checkpoint Seconds", minimumCheckpointSeconds);
            stats.Add("Parameters/Progress Reward Per Metre", progressRewardPerMetre);
            stats.Add("Parameters/Forward Distance Reward Per Metre", forwardDistanceRewardPerMetre);
            stats.Add("Parameters/Forward Speed Bonus Per Metre", forwardSpeedBonusPerMetre);
            stats.Add("Parameters/Speed Reward Reference", speedRewardReference);
            stats.Add("Parameters/Stuck Time Limit", stuckTimeLimit);
            var road = GetComponent<RayPerceptionSensorComponent3D>();
            var traffic = transform.Find(TrafficSensorName)?.GetComponent<RayPerceptionSensorComponent3D>();
            stats.Add("Parameters/Road Rays", road != null ? 2 * road.RaysPerDirection + 1 : 0);
            stats.Add("Parameters/Vector Observations", (compactTrainingSensors ? CompactObservationCount : ObservationCount));
            stats.Add("Parameters/Opponent Casts", compactTrainingSensors && traffic != null ? 2 * traffic.RaysPerDirection + 1 : 0);
            stats.Add("Parameters/Random Opponent Start", randomizeOpponentStart ? 1f : 0f);
            stats.Add("Parameters/Maintain Opponent Presence", maintainOpponentPresence ? 1f : 0f);
            stats.Add("Parameters/Opponent Max Distance", opponentMaxDistance);
            stats.Add("Parameters/Opponent Enabled", spawnOpponent && opponentModel != null ? 1f : 0f);
        }

        private void ResetProgressTarget(Transform target)
        {
            progressTarget = target;
            bestTargetDistance = target != null ? FlatDistance(transform.position, target.position) : 0f;
            lastPosition = transform.position;
            routeOrigin = transform.position;
            routeDirection = target != null ? Vector3.ProjectOnPlane(target.position - routeOrigin, Vector3.up).normalized : Vector3.zero;
            routeDistance = bestTargetDistance;
            bestForwardExtent = 0f;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public bool ReachMiniCheckpoint(MiniCheckpoint checkpoint)
        {
            if (episodeEnding || !trackGenerator.TryReachMiniCheckpoint(this, checkpoint))
                return false;
            float elapsed = timeSinceLastMiniCheckpoint;
            float reward = TimeScaledReward(checkpointReward, elapsed, minimumCheckpointSeconds);
            AddReward(reward);
            checkpointRewards += reward;
            totalCheckpointSeconds += elapsed;
            if (!isOpponent)
            {
                var stats = Academy.Instance.StatsRecorder;
                stats.Add("Timing/Average Seconds Per CP", elapsed);
                // Limit positional charts on the endless track to the first 20 CPs.
                if (miniCheckpointsReached < 20)
                    stats.Add($"Timing/CP {miniCheckpointsReached + 1:00} Seconds", elapsed);
                stats.Add("Rewards/Mini Checkpoint", reward);
            }
            miniCheckpointsReached++;
            timeSinceLastMiniCheckpoint = 0f;
            timeWithoutProgress = 0f;
            progressSinceStuckReset = 0f;
            ResetProgressTarget(trackGenerator.GetTarget(this));
            return true;
        }

        public static float TimeScaledReward(float reward, float seconds, float minimumSeconds)
        {
            return Mathf.Max(0f, reward) / Mathf.Max(0.02f, minimumSeconds, seconds);
        }

        public void ReachSegment(float elapsedSeconds)
        {
            if (episodeEnding)
                return;
            float reward = TimeScaledReward(segmentReward, elapsedSeconds, minimumCheckpointSeconds);
            AddReward(reward);
            checkpointRewards += reward;
            if (!isOpponent)
            {
                var stats = Academy.Instance.StatsRecorder;
                stats.Add("Timing/Average Seconds Per Segment", elapsedSeconds);
                stats.Add("Rewards/Segment", reward);
            }
            segmentsReached++;
            segmentElapsed = 0f;
            timeSinceLastMiniCheckpoint = 0f;
            timeWithoutProgress = 0f;
            progressSinceStuckReset = 0f;
            ResetProgressTarget(trackGenerator.GetTarget(this));
        }

        public void FailEpisode(string reason)
        {
            if (episodeEnding)
                return;
            episodeEnding = true;
            if (checkpointTimer != null)
                checkpointTimer.StopTimer();
            if (isOpponent)
            {
                trackGenerator.RemoveRacer(this);
                car.ResetControlState();
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                opponentFailureTime = Time.time;
                // The primary's presence controller can safely rejoin this opponent.
                gameObject.SetActive(false);
                return;
            }
            var stats = Academy.Instance.StatsRecorder;
            if (spawnOpponent && maintainOpponentPresence)
            {
                stats.Add("Opponent/Rejoins Per Episode", opponentRejoins);
                stats.Add("Opponent/Failure Rejoins Per Episode", opponentFailureRejoins);
                stats.Add("Opponent/Distance Rejoins Per Episode", opponentDistanceRejoins);
            }
            stats.Add("Racer/Mini Checkpoints", miniCheckpointsReached);
            stats.Add("Racer/Segments", segmentsReached);
            stats.Add("Racer/Progress Reward", progressReward);
            stats.Add("Racer/Forward Metres", forwardMetres);
            stats.Add("Rewards/Forward Distance", forwardDistanceReward);
            stats.Add("Rewards/Forward Speed", forwardSpeedReward);
            stats.Add("Racer/Average Road Contact", roadContactSamples > 0 ? roadContactSum / roadContactSamples : 0f);
            stats.Add("Rewards/Checkpoint Total", checkpointRewards);
            stats.Add("Timing/Completed CP Count", miniCheckpointsReached);
            if (miniCheckpointsReached > 0)
                stats.Add("Timing/Episode Average Seconds Per CP", totalCheckpointSeconds / miniCheckpointsReached);
            stats.Add("Racer/Fall", reason == "Fall" ? 1f : 0f);
            stats.Add("Racer/Stuck", reason == "Stuck" ? 1f : 0f);
            stats.Add("Racer/Timeout", reason == "Timeout" ? 1f : 0f);
            if (verboseLogging)
                Debug.Log($"[Racer] {reason}; mini={miniCheckpointsReached}, segments={segmentsReached}", this);
            EndEpisode();
        }
    }
}


using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

namespace SpinMotion
{
    [RequireComponent(typeof(CarController))]
    public class CarAgent : Agent
    {
        [SerializeField] private CarController car;
        [SerializeField] private Transform targetCheckpoint;
        [SerializeField] private EndlessTrackGenerator trackGenerator;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private RaceTimer raceTimer;
        [SerializeField] private CheckpointTimer checkpointTimer;
        
        [SerializeField] private float miniCheckpointTimeLimit = 5f;
        [SerializeField] private float checkpointReward = 1f;
        [SerializeField] private float speedRewardFactor = 0.02f;
        private float timeSinceLastMiniCheckpoint;
        private bool episodeEnding;
        
        private Rigidbody rb;
        private WheelCollider[] wheels;

        // Reconstructed from TRAINING.md in the 2026-10-24 transfer.
        // Exact scales and probe spacing were not included in that export.
        [Header("Reconstructed observation calibration")]
        [SerializeField, Min(0.01f)] private float velocityScale = 25f;
        [SerializeField, Min(0.01f)] private float yawRateScale = 5f;
        [SerializeField, Min(0.01f)] private float targetDistanceScale = 100f;
        [SerializeField, Min(0.01f)] private float roadProbeHalfWidth = 1f;
        [SerializeField, Min(0.01f)] private float roadProbeHeight = 2f;
        [SerializeField, Min(0.01f)] private float roadProbeDepth = 5f;

        public override void Initialize()
        {
            if (car == null)
                car = GetComponent<CarController>();
            
            rb = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<WheelCollider>();
            GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize = 18;
        }

        public void Update()
        {
            if (episodeEnding)
                return;
            if (car.transform.position.y < -1f)
            {
                AbortEpisode();
            }
        }
        
        private void FixedUpdate()
        {
            timeSinceLastMiniCheckpoint += Time.fixedDeltaTime;
        }

        public void AbortEpisode()
        {
            if (episodeEnding) return;
            episodeEnding = true;
            if (checkpointTimer != null) checkpointTimer.StopTimer();
            AddReward(-1f);
            EndEpisode();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 velocity = transform.InverseTransformDirection(rb.linearVelocity);
            sensor.AddObservation(Mathf.Clamp(velocity.x / velocityScale, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(velocity.y / velocityScale, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(velocity.z / velocityScale, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(
                transform.InverseTransformDirection(rb.angularVelocity).y / yawRateScale, -1f, 1f));

            Transform target = trackGenerator != null ? trackGenerator.targetCheckpoint : targetCheckpoint;
            Vector3 delta = target != null
                ? transform.InverseTransformDirection(target.position - transform.position)
                : Vector3.zero;
            delta.y = 0f;
            Vector3 direction = delta.normalized;
            sensor.AddObservation(direction.x);
            sensor.AddObservation(direction.z);
            sensor.AddObservation(Mathf.Clamp01(delta.magnitude / targetDistanceScale));
            sensor.AddObservation(checkpointTimer != null ? checkpointTimer.RemainingTimeNormalized : 0f);
            int grounded = 0;
            foreach (WheelCollider wheel in wheels)
                if (wheel.GetGroundHit(out WheelHit hit) && IsRoad(hit.collider)) grounded++;
            sensor.AddObservation(wheels.Length > 0 ? (float)grounded / wheels.Length : 0f);

            // Row-major: near/middle/far (0/4/8 m), then left/centre/right.
            for (int row = 0; row < 3; row++)
                for (int column = -1; column <= 1; column++)
                    sensor.AddObservation(HasRoadBelow(column * roadProbeHalfWidth, row * 4f) ? 1f : 0f);
        }

        private static bool IsRoad(Collider collider)
        {
            return collider != null && collider.CompareTag("Road");
        }

        private bool HasRoadBelow(float lateral, float forward)
        {
            Vector3 origin = transform.position + transform.right * lateral
                + transform.forward * forward + Vector3.up * roadProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
                roadProbeDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider surface = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                surface = hit.collider;
            }
            return IsRoad(surface);
        }

        public override void OnActionReceived(
            ActionBuffers actions)
        {
            float steering =
                Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);

            float drive = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            float throttle = Mathf.Max(0f, drive);

            // float brake =
            //     -Mathf.Clamp01(actions.ContinuousActions[2]);

            car.Move(steering, throttle, Mathf.Min(0f, drive), 0f);
            // Debug.Log(
            //     $"Agent Actions: steer={steering}, " +
            //     $"gas={throttle}, " //+
            //     // $"brake={actions.ContinuousActions[2]:F2}"
            // );

            // Kleine Zeitstrafe: Der Agent soll Fortschritt machen.
            // AddReward(-0.0005f);
        }

        public override void Heuristic(
            in ActionBuffers actionsOut)
        {
            var actions = actionsOut.ContinuousActions;

            actions[0] = Input.GetAxis("Horizontal");
            actions[1] = Input.GetAxis("Vertical");
        }
        
        private void ResetCar()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation);
            car.ResetControlState();
            Physics.SyncTransforms();
        }
        
        public override void OnEpisodeBegin()
        {
            episodeEnding = false;
            ResetCar();

            // Hier muss auch die Strecke zurückgesetzt werden.
            // Danach muss der Generator wieder ein gültiges
            // TargetCheckpoint bereitstellen.

            if (trackGenerator != null)
            {
                trackGenerator.ResetTrack();
            }

            if (raceTimer != null)
            {
                raceTimer.ResetTimer();
            }
            
            if (checkpointTimer != null)
            {
                checkpointTimer.StartTimer();
            }
            
            timeSinceLastMiniCheckpoint = 0f;
        }

        
        public bool ReachMiniCheckpoint(MiniCheckpoint checkpoint)
        {
            if (episodeEnding || trackGenerator == null || !trackGenerator.TryReachMiniCheckpoint(checkpoint))
                return false;
            float elapsed = Mathf.Max(timeSinceLastMiniCheckpoint, 0.1f);

            // Grundreward für das Erreichen
            float reward = checkpointReward;

            // Bonus für schnelles Erreichen
            float speedBonus = Mathf.Clamp(
                (miniCheckpointTimeLimit - elapsed)
                / miniCheckpointTimeLimit,
                0f,
                1f
            ) * speedRewardFactor;

            AddReward(reward + speedBonus);

            Debug.Log(
                $"MiniCheckpoint! Zeit: {elapsed:F2}s, " +
                $"Reward: {reward + speedBonus:F3}"
            );

            // Timer für den nächsten MiniCheckpoint zurücksetzen
            timeSinceLastMiniCheckpoint = 0f;
            return true;
        }

    }
}

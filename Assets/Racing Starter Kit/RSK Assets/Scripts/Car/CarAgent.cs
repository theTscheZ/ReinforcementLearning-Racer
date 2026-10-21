
using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

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

        public override void Initialize()
        {
            if (car == null)
                car = GetComponent<CarController>();
            
            rb = GetComponent<Rigidbody>();
        }

        public void Update()
        {
            if (episodeEnding)
                return;
            if (car.transform.position.y < -1f)
            {
                if (checkpointTimer != null)
                    checkpointTimer.StopTimer();
                // Das Auto ist von der Strecke gefallen.
                // Episode zurücksetzen.
                episodeEnding = true;
                AddReward(-1f);
                Debug.LogWarning("[RESET] CarAgent: Auto gefallen");
                EndEpisode();
            }
        }
        
        private void FixedUpdate()
        {
            timeSinceLastMiniCheckpoint += Time.fixedDeltaTime;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // // Checkpoint sicher prüfen
            // if (targetCheckpoint == null)
            // {
            //     sensor.AddObservation(0f);
            //     sensor.AddObservation(0f);
            // }
            // else
            // {
            //     Vector3 targetLocal = transform.InverseTransformPoint(
            //         targetCheckpoint.position
            //     );
            //
            //     sensor.AddObservation(
            //         Mathf.Clamp(targetLocal.x / 50f, -1f, 1f)
            //     );
            //
            //     sensor.AddObservation(
            //         Mathf.Clamp(targetLocal.z / 100f, -1f, 1f)
            //     );
            // }
        }

        public override void OnActionReceived(
            ActionBuffers actions)
        {
            float steering =
                Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);

            float throttle =
                Mathf.Clamp01(actions.ContinuousActions[1]);

            // float brake =
            //     -Mathf.Clamp01(actions.ContinuousActions[2]);

            car.Move(steering, throttle, 0f, 0f);
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
            Debug.Log("DREH AN MEINEM ZIPPY");
            var actions = actionsOut.ContinuousActions;

            actions[0] = Input.GetAxis("Horizontal");
            actions[1] = Input.GetAxis("Vertical") > 0f
                ? Input.GetAxis("Vertical") : 0f;

            actions[2] = Input.GetAxis("Vertical") < 0f
                ? -Input.GetAxis("Vertical") : 0f;
        }
        
        private void ResetCar()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation);
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

        
        public void ReachMiniCheckpoint(MiniCheckpoint checkpoint)
        {
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
        }

    }
}

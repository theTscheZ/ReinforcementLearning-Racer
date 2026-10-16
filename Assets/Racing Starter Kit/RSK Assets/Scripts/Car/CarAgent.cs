
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
        [SerializeField] private TrackSegment prefab;
        
        private Rigidbody rb;

        public override void Initialize()
        {
            if (car == null)
                car = GetComponent<CarController>();
            
            rb = GetComponent<Rigidbody>();
        }

        public void Update()
        {
            if (car.transform.position.y < -5f)
            {
                // Das Auto ist von der Strecke gefallen.
                // Episode zurücksetzen.
                AddReward(-1f);
                EndEpisode();
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Checkpoint sicher prüfen
            if (targetCheckpoint == null)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            else
            {
                Vector3 targetLocal = transform.InverseTransformPoint(
                    targetCheckpoint.position
                );

                sensor.AddObservation(
                    Mathf.Clamp(targetLocal.x / 50f, -1f, 1f)
                );

                sensor.AddObservation(
                    Mathf.Clamp(targetLocal.z / 100f, -1f, 1f)
                );
            }
        }

        public override void OnActionReceived(
            ActionBuffers actions)
        {
            float steering =
                Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);

            float throttle =
                Mathf.Clamp01(actions.ContinuousActions[1]);

            float brake =
                -Mathf.Clamp01(actions.ContinuousActions[2]);

            car.Move(steering, throttle, brake, 0f);

            // Kleine Zeitstrafe: Der Agent soll Fortschritt machen.
            AddReward(-0.0005f);
        }

        public override void Heuristic(
            in ActionBuffers actionsOut)
        {
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
            ResetCar();

            // Hier muss auch die Strecke zurückgesetzt werden.
            // Danach muss der Generator wieder ein gültiges
            // TargetCheckpoint bereitstellen.

            if (trackGenerator != null)
            {
                trackGenerator.ResetTrack();
            }
            // Instantiate(prefab, trackRoot.transform);
            
        }
    }
}


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

        public override void Initialize()
        {
            if (car == null)
                car = GetComponent<CarController>();
        }

        public override void CollectObservations(
            VectorSensor sensor)
        {
            // Position des Checkpoints relativ zum Auto
            Vector3 targetLocal =
                transform.InverseTransformPoint(
                    targetCheckpoint.position);

            sensor.AddObservation(
                Mathf.Clamp(targetLocal.x / 50f, -1f, 1f));

            sensor.AddObservation(
                Mathf.Clamp(targetLocal.z / 100f, -1f, 1f));

            // Geschwindigkeit relativ zur Ausrichtung des Autos
            Vector3 localVelocity =
                transform.InverseTransformDirection(
                    GetComponent<Rigidbody>().linearVelocity);

            sensor.AddObservation(
                Mathf.Clamp(localVelocity.x / 30f, -1f, 1f));

            sensor.AddObservation(
                Mathf.Clamp(localVelocity.z / 50f, -1f, 1f));

            sensor.AddObservation(
                Mathf.Clamp01(car.CurrentSpeed / car.MaxSpeed));
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
    }
}

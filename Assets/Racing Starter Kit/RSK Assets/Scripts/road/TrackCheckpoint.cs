using UnityEngine;

namespace SpinMotion
{
    [RequireComponent(typeof(Collider))]
    public class TrackCheckpoint : MonoBehaviour
    {
        private EndlessTrackGenerator generator;
        private TrackSegment ownerSegment;

        public void Initialize(
            EndlessTrackGenerator generator,
            TrackSegment ownerSegment)
        {
            this.generator = generator;
            this.ownerSegment = ownerSegment;
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();

            if (!col.isTrigger)
            {
                Debug.LogWarning(
                    $"Checkpoint '{name}' collider was not a trigger. Enabling IsTrigger."
                );

                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Falls dein Auto einen Rigidbody auf einem Parent besitzt,
            // ist attachedRigidbody robuster als CompareTag direkt am Collider.
            Transform hitTransform =
                other.attachedRigidbody != null
                    ? other.attachedRigidbody.transform
                    : other.transform;

            var agent = hitTransform.GetComponent<CarAgent>();
            if (agent != null && generator != null)
                generator.CheckpointReached(agent, ownerSegment);
        }
    }
}

using UnityEngine;

namespace SpinMotion
{
    [RequireComponent(typeof(Collider))]
    public class TrackCheckpoint : MonoBehaviour
    {
        private EndlessTrackGenerator generator;
        private TrackSegment ownerSegment;

        private bool triggered;

        public void Initialize(
            EndlessTrackGenerator generator,
            TrackSegment ownerSegment)
        {
            this.generator = generator;
            this.ownerSegment = ownerSegment;
            triggered = false;
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
            if (triggered)
            {
                return;
            }

            // Falls dein Auto einen Rigidbody auf einem Parent besitzt,
            // ist attachedRigidbody robuster als CompareTag direkt am Collider.
            Transform hitTransform =
                other.attachedRigidbody != null
                    ? other.attachedRigidbody.transform
                    : other.transform;

            if (!hitTransform.CompareTag("Player"))
            {
                return;
            }
            // Debug.Log($"Checkpoint '{name}' triggered by '{hitTransform.name}'.");
            triggered = true;
            
            generator?.CheckpointReached(ownerSegment);
        }
    }
}
using UnityEngine;

namespace SpinMotion
{
    public class MiniCheckpoint : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponentInParent<CarAgent>();
            if (agent != null)
                agent.ReachMiniCheckpoint(this);
        }

        // Collection state belongs to each racer, never to the shared trigger.
        public void ResetCheckpoint() { }
    }
}

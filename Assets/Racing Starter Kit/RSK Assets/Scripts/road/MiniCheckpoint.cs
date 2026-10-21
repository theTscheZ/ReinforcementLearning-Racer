using UnityEngine;

namespace SpinMotion
{
    public class MiniCheckpoint : MonoBehaviour
    {
        private bool collected;

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
                return;

            CarAgent agent = other.GetComponentInParent<CarAgent>();

            if (agent == null)
                return;

            collected = true;
            agent.ReachMiniCheckpoint(this);
        }

        public void ResetCheckpoint()
        {
            collected = false;
        }
    }
}
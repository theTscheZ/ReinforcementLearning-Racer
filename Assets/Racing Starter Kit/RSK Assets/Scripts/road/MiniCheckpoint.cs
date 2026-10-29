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

            if (agent.ReachMiniCheckpoint(this))
                collected = true;
        }

        public void ResetCheckpoint()
        {
            collected = false;
        }
    }
}

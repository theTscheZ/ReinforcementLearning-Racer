using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// gather all provided checkpoints on the public list to track them in the race for all cars
/// </summary>
namespace SpinMotion
{
    public class Checkpoints : MonoBehaviour
    {
        public List<Checkpoint> checkpoints = new();

        private void Start()
        {
            RebuildCheckpoints();
        }

        public void RebuildCheckpoints()
        {
            checkpoints.Clear();
            checkpoints.AddRange(GetComponentsInChildren<Checkpoint>().ToList());
            for (int i = 0; i < checkpoints.Count; i++)
            {
                checkpoints[i].SetCheckpointNumber(i + 1);
                checkpoints[i].gameObject.name = BuildCheckpointName(checkpoints[i].gameObject.name, i + 1);
            }
            RaceData.CheckpointsCount = checkpoints.Count;
        }

        private string BuildCheckpointName(string currentName, int checkpointNumber)
        {
            var baseName = currentName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Checkpoint";
            }

            return baseName + checkpointNumber;
        }
    }
}
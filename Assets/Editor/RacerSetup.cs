using SpinMotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

public static class RacerSetup
{
    [MenuItem("Racer/Training/1 - Solo (new compact run)")]
    public static void Training() => Configure(true, false, false);

    [MenuItem("Racer/Training/2 - With opponent (continue compact model)")]
    public static void TrainingWithOpponent() => Configure(true, false, true);

    [MenuItem("Racer/Race against trained model (keyboard)")]
    public static void KeyboardRace() => Configure(false, true, true);

    [MenuItem("Racer/Race two trained models")]
    public static void ModelRace() => Configure(false, false, true);

    private static void Configure(bool training, bool keyboard, bool withOpponent)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Stop Play Mode before changing the sensor profile.");
        var agent = Object.FindFirstObjectByType<CarAgent>();
        if (agent == null)
            throw new System.InvalidOperationException("Open EndlessScene first.");
        var model = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/ML-Agents/Models/CarAgent-1564601.onnx");
        if (model == null)
            throw new System.InvalidOperationException("The trained opponent model is missing.");
        var behavior = agent.GetComponent<BehaviorParameters>();
        var rays = agent.GetComponent<RayPerceptionSensorComponent3D>();
        var trafficObject = agent.transform.Find(CarAgent.TrafficSensorName);
        if (trafficObject == null)
        {
            var child = new GameObject(CarAgent.TrafficSensorName);
            Undo.RegisterCreatedObjectUndo(child, "Add traffic sensor");
            child.transform.SetParent(agent.transform, false);
            child.transform.localPosition = Vector3.up * 0.5f;
            child.AddComponent<RayPerceptionSensorComponent3D>();
            trafficObject = child.transform;
        }
        var traffic = trafficObject.GetComponent<RayPerceptionSensorComponent3D>();
        Undo.RecordObjects(new Object[] { trafficObject.gameObject, traffic }, "Configure traffic sensor");
        traffic.SensorName = CarAgent.TrafficSensorName;
        traffic.DetectableTags = new System.Collections.Generic.List<string> { "Car" };
        traffic.RaysPerDirection = 4;
        traffic.MaxRayDegrees = 180f;
        traffic.RayLength = 20f;
        traffic.SphereCastRadius = 0.35f;
        traffic.StartVerticalOffset = 0f;
        traffic.EndVerticalOffset = 0f;
        // Only the other car's body layer: no self hits or checkpoint-trigger hits.
        traffic.RayLayerMask = 1 << LayerMask.NameToLayer("AIPlayer");
        trafficObject.gameObject.SetActive(training);
        Undo.RecordObjects(new Object[] { agent, behavior, rays }, "Configure racer");
        foreach (var collider in agent.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger || collider is WheelCollider)
                continue;
            Undo.RecordObject(collider.gameObject, "Tag car body");
            collider.gameObject.tag = "Car";
            collider.gameObject.layer = LayerMask.NameToLayer("Player");
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider.gameObject);
        }
        var serialized = new SerializedObject(agent);
        serialized.FindProperty("compactTrainingSensors").boolValue = training;
        serialized.FindProperty("spawnOpponent").boolValue = withOpponent;
        serialized.FindProperty("maintainOpponentPresence").boolValue = training && withOpponent;
        serialized.FindProperty("randomizeOpponentStart").boolValue = training && withOpponent;
        serialized.FindProperty("opponentModel").objectReferenceValue = model;
        serialized.ApplyModifiedProperties();
        rays.RaysPerDirection = training ? 5 : 35;
        behavior.BrainParameters.VectorObservationSize = training ? CarAgent.CompactObservationCount : CarAgent.ObservationCount;
        behavior.Model = training ? null : model;
        behavior.BehaviorType = training ? BehaviorType.Default
            : keyboard ? BehaviorType.HeuristicOnly : BehaviorType.InferenceOnly;
        EditorUtility.SetDirty(traffic);
        EditorUtility.SetDirty(agent);
        EditorUtility.SetDirty(behavior);
        EditorUtility.SetDirty(rays);
        EditorSceneManager.MarkSceneDirty(agent.gameObject.scene);
        Debug.Log(training
            ? (withOpponent
                ? "Opponent training: same 18 vector + 44 road + 27 traffic observations as solo; continue the compact checkpoint. Randomized opponent starts."
                : "Solo training: 11 road rays, 9 ground probes, 9 traffic rays (no opponent); 18 vector observations. New run for this profile.")
            : "Race ready. Opponent always uses the trained model and legacy sensors.");
    }
}

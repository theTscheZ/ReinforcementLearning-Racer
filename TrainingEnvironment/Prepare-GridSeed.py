from pathlib import Path

import onnx
from onnx import numpy_helper
from onnx.reference import ReferenceEvaluator
import torch
from mlagents.plugins.trainer_type import register_trainer_plugins
from mlagents.trainers.cli_utils import load_config
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents_envs.base_env import (
    ActionSpec, BehaviorSpec, DimensionProperty, ObservationSpec, ObservationType,
)


def main():
    root = Path(__file__).resolve().parents[1]
    assets = root / "Assets" / "ML-Agents"
    model = onnx.load(assets / "Models" / "CarAgent-2000011.onnx")
    destination = root / "results" / "CarRacer_GridSeed_2000011" / "CarAgent" / "checkpoint.pt"
    if destination.is_file():
        print(f"Grid seed already exists at {destination}")
        return
    register_trainer_plugins()
    settings = RunOptions.from_dict(load_config(assets / "racer.yaml")).behaviors["CarAgent"]
    observations = [
        ObservationSpec((width,), (DimensionProperty.NONE,), ObservationType.DEFAULT, name)
        for width, name in [(44, "RoadSensor"), (27, "TrafficSensor"), (18, "VectorSensor_size18")]
    ]
    spec = BehaviorSpec(observations, ActionSpec.create_continuous(2))
    torch.manual_seed(1)
    policy = TorchPolicy(1, spec, settings.network_settings, SimpleActor, {})
    weights = {tensor.name: numpy_helper.to_array(tensor) for tensor in model.graph.initializer}
    actor_state = policy.actor.state_dict()
    for name, parameter in policy.actor.named_parameters():
        if parameter.requires_grad:
            actor_state[name] = torch.from_numpy(weights[name].copy())
    policy.actor.load_state_dict(actor_state)

    samples = [torch.rand(32, obs.shape[0]) for obs in observations]
    inputs = {f"obs_{index}": sample.numpy() for index, sample in enumerate(samples)}
    expected, = ReferenceEvaluator(model).run(["deterministic_continuous_actions"], inputs)
    with torch.no_grad():
        actions = policy.actor(samples)[-1]
    torch.testing.assert_close(actions, torch.from_numpy(expected), rtol=1e-5, atol=1e-6)

    optimizer = TorchPPOOptimizer(policy, settings)
    modules = {**policy.get_modules(), **optimizer.get_modules()}
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open("xb") as checkpoint:
        torch.save({name: module.state_dict() for name, module in modules.items()}, checkpoint)
    print(f"Verified 32 observations against ONNX and saved {destination}")


if __name__ == "__main__":
    main()

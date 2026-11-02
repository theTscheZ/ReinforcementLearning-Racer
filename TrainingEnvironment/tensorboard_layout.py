"""Add Racer Custom Scalars charts to an EXISTING ML-Agents run."""
import argparse
from pathlib import Path
from torch.utils.tensorboard import SummaryWriter


def add_layout(run_dir: Path):
    behavior_dir = run_dir / "CarAgent"
    if not behavior_dir.is_dir():
        raise SystemExit(f"Training run not found: {behavior_dir}. Start training first.")
    layout = {
        "Checkpoint times (seconds, lower is faster)": {
            "Mean completed CP time": ["Multiline", ["Timing/Average Seconds Per CP"]],
            "Mean segment time": ["Multiline", ["Timing/Average Seconds Per Segment"]],
            "First 20 CP positions": ["Multiline", [f"Timing/CP {i:02d} Seconds" for i in range(1, 21)]],
        },
        "Progress and failures": {
            "Completed checkpoints": ["Multiline", ["Racer/Mini Checkpoints", "Racer/Segments"]],
            "Failure fractions": ["Multiline", ["Racer/Fall", "Racer/Stuck", "Racer/Timeout"]],
        },
        "Forward driving": {
            "Distance and speed rewards": ["Multiline", ["Rewards/Forward Distance", "Rewards/Forward Speed", "Racer/Progress Reward"]],
            "New forward metres": ["Multiline", ["Racer/Forward Metres"]],
            "Road contact fraction": ["Multiline", ["Racer/Average Road Contact"]],
        },
        "Opponent presence": {
            "Nearby and detected fractions": ["Multiline", ["Opponent/Within Range", "Opponent/Detected"]],
            "Rejoins per episode": ["Multiline", ["Opponent/Rejoins Per Episode", "Opponent/Failure Rejoins Per Episode", "Opponent/Distance Rejoins Per Episode"]],
            "Safe placement unavailable": ["Multiline", ["Opponent/Placement Unavailable"]],
        },
        "Parameters": {
            "Rewards": ["Multiline", ["Parameters/Checkpoint Reward", "Parameters/Segment Reward", "Parameters/Progress Reward Per Metre", "Parameters/Forward Distance Reward Per Metre", "Parameters/Forward Speed Bonus Per Metre"]],
            "Time limits": ["Multiline", ["Parameters/Minimum Checkpoint Seconds", "Parameters/Stuck Time Limit"]],
            "Sensors": ["Multiline", ["Parameters/Road Rays", "Parameters/Vector Observations", "Parameters/Opponent Enabled", "Parameters/Opponent Casts", "Parameters/Random Opponent Start"]],
        },
    }
    with SummaryWriter(str(behavior_dir)) as writer:
        writer.add_custom_scalars(layout)
    print(f"Custom Scalars layout written to {behavior_dir}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path, help="Existing results/<run-id> directory")
    add_layout(parser.parse_args().run_dir)

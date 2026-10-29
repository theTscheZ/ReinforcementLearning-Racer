# Recovery of the trained racer

Source: `Racer-Training-Transfer-20261024.zip`, supplied on 2026-10-29.
The original training notes are preserved in `RacerTraining-20261024.md`.
Those notes describe tests of the original project, not tests of this reconstruction.

## Verified facts

The archive and the existing model have identical SHA-256:
`8b89754573f38a454eaeeaf7840c70a1839b78a1c1e19233342b03f47b40a0af`.
ONNX checking and CPU execution succeeded.

- `obs_0`: `[batch, 284]`. Existing RoadSensor has 71 rays and two tags,
  producing four values per ray. Its settings have been retained.
- `obs_1`: `[batch, 18]`.
- Two continuous actions, steering followed by signed drive: positive gas,
  negative brake/reverse. No recurrent memory.
- The notes specify decisions every five physics steps, next-mini-checkpoint
  targeting, and the observation groups below.

## Reconstructed observation contract

The archive contains no C# source. The group ordering follows the description
in the notes; the exact original ordering within groups, divisors, clamping,
ground-contact encoding and probe lateral spacing are not proven.
This is a candidate implementation, not a recovered copy of the original code.

| Indices | Implemented values | Evidence / uncertainty |
| --- | --- | --- |
| 0–2 | Local velocity x/y/z, divided by 25 m/s, clamped to -1…1 | Three local velocity values documented; scale/order inferred |
| 3 | Local yaw angular velocity / 5 rad/s, clamped to -1…1 | One angular velocity documented; scale/axis inferred |
| 4–5 | Normalized horizontal local target direction x/z | Two direction values documented; exact formula inferred |
| 6 | Horizontal target distance / 100 m, clamped to 0…1 | Distance documented; scale inferred |
| 7 | Remaining segment time / maximum segment time | Remaining time documented; normalization inferred |
| 8 | Fraction of wheels touching a Road-tagged collider | Ground contact documented; fraction encoding inferred |
| 9–17 | Road below probes: 0, 4, 8 m ahead; left, centre, right in each row | Nine probes and forward distances documented; row order, ±1 m width, cast height/depth and binary encoding inferred |

Calibration fields are exposed on `CarAgent` under **Reconstructed observation
calibration**. A matching shape alone does not prove matching driving behavior.
Recovering the original `CarAgent.cs` remains the best way to remove uncertainty.

## Scene and runtime changes

`EndlessScene` uses 18 vector observations, two actions, Decision Period 5,
CPU/Burst inference and the supplied model. The existing RoadSensor is retained.
The heuristic writes only two actions. The drive action now supports braking
and reversing; wheel brakes release correctly and control values reset.

Mini-checkpoints are ordered by nearest neighbour from segment entry, reset each
episode and accepted in order. The target moves after each accepted checkpoint.
The hand-placed start segment survives episode resets. Initial track construction
does not run twice if the agent has already built it.

The original training reward/stillstand logic has not been fully reconstructed.
The current training YAML remains unchanged. Use this reconstruction for inference
evaluation; it is not a faithful environment for resuming the archived training.

The selected ONNX file is exempted from `.gitignore`; no commit or push is made.

## Validation on 2026-10-29

The seven changed runtime scripts match the isolated test copy byte for byte.
Unity 6000.0.84f1 executed the model with CPU/Burst inference. In a 15-second
wall-time smoke test at time scale 5, the agent reached up to 15 complete segments
in an episode and displaced up to 342 m from its spawn. This was one run on random
track with stochastic inference, not a reliability benchmark.

The test also passed: exactly 18 finite observations, three episode resets with
valid targets, ordered mini/main checkpoint acceptance, rejection of repeated and
premature checkpoints, target advancement, braking on all four wheels, brake
release, reverse torque and clearing control state. No runtime errors or observation
shape warnings were found in the successful run. See `RacerRecoveryValidation.txt`.

The first test-copy imports failed to bind project scripts when Unity was launched
through a Windows short-name temporary path. Reimporting and running with the
canonical full path resolved that fixture issue; it required no changes to gameplay.

To run in the working project, reopen `EndlessScene` after Unity recompiles and
press Play. The saved scene already references the model with Inference Only,
CPU/Burst and Decision Period 5.

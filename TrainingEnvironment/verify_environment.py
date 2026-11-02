"""Check the restored interpreter, package versions and CPU PyTorch runtime."""
import importlib.metadata as metadata
import json
import platform
import re
import struct
import subprocess
import sys
from pathlib import Path


def canonical(name):
    return re.sub(r"[-_.]+", "-", name).lower()


def main():
    snapshot = json.loads(Path(__file__).with_name("environment.json").read_text(encoding="utf-8"))
    errors = []
    if platform.python_version() != snapshot["python_version"]:
        errors.append(f"Python: expected {snapshot['python_version']}, got {platform.python_version()}")
    if sys.platform != "win32" or struct.calcsize("P") * 8 != 64:
        errors.append("Expected Windows x64")
    installed = {canonical(d.metadata["Name"]): d.version for d in metadata.distributions()}
    for name, expected in snapshot["packages"].items():
        actual = installed.get(canonical(name))
        # The original torch metadata omits +cpu; verify its actual runtime below.
        if canonical(name) == "torch" and actual:
            actual, expected = actual.split("+")[0], expected.split("+")[0]
        if actual != expected:
            errors.append(f"{name}: expected {expected}, got {actual or 'missing'}")
    if errors:
        raise SystemExit("Environment differs:\n" + "\n".join(errors))
    import numpy
    import onnx
    import torch

    if torch.__version__ != snapshot["torch_runtime_version"] or torch.version.cuda is not None:
        raise SystemExit(f"Expected CPU PyTorch {snapshot['torch_runtime_version']}, got {torch.__version__}")
    # Exercise compiled numerical libraries, not only their distribution metadata.
    result = torch.from_numpy(numpy.array([1.0, 2.0], dtype=numpy.float32)).sum().item()
    if result != 3.0:
        raise SystemExit("Torch/NumPy runtime check failed")
    subprocess.run([sys.executable, "-m", "pip", "check"], check=True)
    print(f"Verified Python {platform.python_version()}, {len(snapshot['packages'])} pinned packages, PyTorch {torch.__version__}")


if __name__ == "__main__":
    main()

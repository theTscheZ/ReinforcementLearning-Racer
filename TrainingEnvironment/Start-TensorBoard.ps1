param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [ValidateRange(1, 65535)][int]$Port = 6006,
    [string]$VenvPath = ".venv"
)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$pythonExe = Join-Path $projectRoot "$VenvPath/Scripts/python.exe"
$tensorboardExe = Join-Path $projectRoot "$VenvPath/Scripts/tensorboard.exe"
$runDirectory = Join-Path (Join-Path $projectRoot "results") $RunId
& $pythonExe (Join-Path $PSScriptRoot "tensorboard_layout.py") $runDirectory
if ($LASTEXITCODE -ne 0) { throw "Custom Scalars setup failed. Start the training run first." }
& $tensorboardExe "--logdir=$(Join-Path $projectRoot 'results')" "--port=$Port"
if ($LASTEXITCODE -ne 0) { throw "TensorBoard exited with code $LASTEXITCODE" }

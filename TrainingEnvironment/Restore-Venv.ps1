param(
    [string]$PythonExe = "",
    [string]$VenvPath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($env:OS -ne "Windows_NT") {
    throw "This export targets Windows x64."
}
if ([string]::IsNullOrWhiteSpace($VenvPath)) {
    $VenvPath = Join-Path $projectRoot ".venv"
}
if (Test-Path -LiteralPath $VenvPath) {
    throw "Destination already exists: $VenvPath. Use -VenvPath to choose a new environment."
}

$pythonPrefix = @()
if ([string]::IsNullOrWhiteSpace($PythonExe)) {
    $PythonExe = "py"
    $pythonPrefix = @("-3.10")
}
$pythonInfo = & $PythonExe @pythonPrefix -c "import platform,struct,sys; print(platform.python_version()+'|'+str(struct.calcsize('P')*8)+'|'+sys.platform)"
if ($LASTEXITCODE -ne 0) { throw "Could not start Python. Use -PythonExe with the path to python.exe." }
if ($pythonInfo.Trim() -ne "3.10.11|64|win32") {
    throw "Expected Python 3.10.11, Windows x64; found $pythonInfo."
}

& $PythonExe @pythonPrefix -m venv $VenvPath
if ($LASTEXITCODE -ne 0) { throw "Creating the virtual environment failed." }
$environmentPython = Join-Path $VenvPath "Scripts/python.exe"
& $environmentPython -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot "requirements-bootstrap.txt")
if ($LASTEXITCODE -ne 0) { throw "Installing the pinned pip/setuptools versions failed." }
& $environmentPython -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot "requirements-win-cpu.txt")
if ($LASTEXITCODE -ne 0) { throw "Installing the exported dependencies failed." }
& $environmentPython (Join-Path $PSScriptRoot "verify_environment.py")
if ($LASTEXITCODE -ne 0) { throw "Environment verification failed." }
& $environmentPython -m mlagents.trainers.learn --help
if ($LASTEXITCODE -ne 0) { throw "ML-Agents trainer could not start." }
Write-Host "Environment restored and verified: $VenvPath"

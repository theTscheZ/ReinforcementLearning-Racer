# Python-Umgebung auf einem anderen PC einrichten

Dieser Export stammt aus der funktionierenden Trainingsumgebung vom 24.10.2026.
Ziel: **Windows 64 Bit, Python 3.10.11, PyTorch 2.1.1 CPU, ML-Agents 1.1.0**.
ML-Agents 1.1.0 erlaubt laut installierten Paketmetadaten Python 3.10.1 bis
3.10.12. Für dieselbe Umgebung wird hier ausdrücklich 3.10.11 verwendet.

## Einrichtung

1. Das Unity-Projekt auf den anderen PC kopieren/klonen. Unity-Version:
   `6000.0.84f1`; die Unity-Abhängigkeiten stehen in `Packages`.
2. [Python 3.10.11 für Windows x64](https://www.python.org/downloads/release/python-31011/)
   installieren, einschließlich Python Launcher (`py`).
3. Das Transfer-ZIP ins Projektverzeichnis entpacken. Die Ordner
   `TrainingEnvironment`, `results` und `Assets` gehören direkt ins Projekt.
4. In PowerShell im Projektordner ausführen:

```powershell
& .\TrainingEnvironment\Restore-Venv.ps1
```

Alternativ einen bestimmten Interpreter oder einen anderen Zielordner angeben:

```powershell
& .\TrainingEnvironment\Restore-Venv.ps1 -PythonExe "C:\Pfad\zu\Python310\python.exe" -VenvPath ".venv-racer"
```

Das Skript erstellt eine neue Umgebung und verändert keine bestehende `.venv`.
Es lädt die exakt aufgezeichneten Paketversionen aus dem Internet, einschließlich
des offiziellen CPU-Wheels von PyTorch, und prüft anschließend Paketabhängigkeiten,
Versionen, Torch/NumPy und den Trainerstart. Eine NVIDIA-Grafikkarte ist nicht
erforderlich. Das ZIP enthält die Installationsdefinitionen; es ist kein
Offline-Paket mit sämtlichen Python-Installern und Wheels.

Die Aktivierung der Umgebung ist optional. Die folgenden Befehle verwenden
direkt die Programme in `.venv`.

## Neuer Feedback-Stand (31.10.2026)

Die aktuelle Szene unterst�tzt einen Modell-Gegner, zeitabh�ngige positive
Rewards, kompakte Trainingssensoren und eigene TensorBoard-Diagramme.
Siehe `Assets/ML-Agents/TRAINING.md` f�r die aktuellen Men�befehle und Starts.
Das kompakte Sensorprofil ben�tigt einen **neuen Lauf**; die folgenden
Resume-/Modellhinweise gelten f�r das bisherige Profil mit 18 Vektorwerten
und 284 Raywerten. Der alte Transfer-Export enth�lt diese Neuerungen nicht.

## Training fortsetzen (bisheriges Sensorprofil)

Im Transfer-ZIP ist der vollständige aktuelle Lauf mit dem Checkpoint bei
**1.564.601 Schritten** enthalten, einschließlich `CarAgent/checkpoint.pt`.
Zum Weitertrainieren werden die `.pt`-Dateien benötigt; ONNX allein reicht nicht.

```powershell
& .\.venv\Scripts\mlagents-learn.exe .\Assets\ML-Agents\racer.yaml --run-id=CarRacer_RoadFeedback_Smoke_20261024 --resume
```

Danach in Unity `EndlessScene` starten, Behavior Type `Default` verwenden.
Die aktuelle Trainingskonfiguration muss aus dem Unity-Projekt mitkopiert werden.

```powershell
& .\.venv\Scripts\tensorboard.exe --logdir=results
```

## Nur das trainierte Auto fahren lassen

1. `Assets/ML-Agents/Models/CarAgent-1564601.onnx` in Unity importieren lassen.
2. `Player Car 1` auswählen, Komponente `Behavior Parameters` öffnen.
3. Das ONNX-Asset aus dem Project-Fenster ins Feld `Model` ziehen.
4. `Behavior Type` auf **Inference Only** stellen; `Inference Device` zunächst CPU.
5. Play drücken. Python/venv/Trainer werden zum Ausführen des Modells nicht benötigt.

Sensoren und Agent-Skript müssen weiter vorhanden sein: Das Modell erwartet
284 Ray-Beobachtungswerte und 18 zusätzliche Vektorwerte, und liefert zwei Aktionen.
Zum erneuten Training Behavior Type wieder auf `Default` stellen.

## Enthaltene Dateien

- `requirements-freeze-all.txt`: unveränderte vollständige Ausgabe von
  `pip freeze --all`, einschließlich pip und setuptools.
- `requirements-bootstrap.txt`: dieselben Versionen von pip und setuptools.
- `requirements-win-cpu.txt`: Paketversionen für die Wiederherstellung;
  Torch ist ausdrücklich an das offizielle Windows-CPU-Wheel gebunden.
- `environment.json`: Interpreter, Paketversionen, Trainingsschritte und
  SHA-256 des exportierten ONNX-Modells. Enthält keine lokalen Benutzerpfade.
- `Restore-Venv.ps1`: Einrichtung ohne Eingriff in bestehende Umgebungen.
- `verify_environment.py`: Versions- und Funktionstest.

Die bisherige `.venv` sollte nicht als Ordner auf den anderen PC kopiert werden:
Programme und Skripte darin können absolute Pfade enthalten.
Das Unity-Projekt selbst ist nicht vollständig im Transfer-ZIP enthalten.
Die Ordner `results` und ONNX/PT-Dateien werden im aktuellen Projekt von Git
ignoriert; deshalb sind sie zusätzlich im Transfer-ZIP enthalten.

Referenzen:
[Unity-Modell und Behavior Type](https://unity-technologies.github.io/ml-agents/Learning-Environment-Design-Agents/),
[PyTorch CPU-Versionen](https://docs.pytorch.org/get-started/previous-versions/).

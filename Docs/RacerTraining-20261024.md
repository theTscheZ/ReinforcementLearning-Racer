# Racer-Training

## Neuer Trainingslauf

Der ursprüngliche Testlauf `CarRacer_RoadFeedback_Smoke_20261024` wurde inzwischen
bis **1.564.601 Schritte** weitertrainiert und gespeichert. "Smoke" ist nur der
Name des ursprünglichen kurzen Tests. Zum Fortsetzen mit der normalen Konfiguration:

```powershell
& ".\.venv\Scripts\mlagents-learn.exe" ".\Assets\ML-Agents\racer.yaml" --run-id=CarRacer_RoadFeedback_Smoke_20261024 --resume
```

Alternativ ein komplett neuer Lauf (PowerShell im Projektordner):

```powershell
& ".\.venv\Scripts\mlagents-learn.exe" ".\Assets\ML-Agents\racer.yaml" --run-id=CarRacer_RoadFeedback_01
```

Danach in Unity `EndlessScene` starten. Behavior Type: Default. Decision Period: 5.
Der Agent setzt seine Vector Observation Size beim Start automatisch auf 18.
Eine neue Run-ID verwenden: Die alten Modelle haben andere Beobachtungen und
eine andere Bedeutung der zweiten Aktion. Nicht mit `--resume` oder
`--initialize-from` aus diesen alten Läufen weitertrainieren.

```powershell
& ".\.venv\Scripts\tensorboard.exe" --logdir=results
```

## Trainiertes Modell ohne Python verwenden

Das aktuelle Modell liegt zusätzlich unter
`Assets/ML-Agents/Models/CarAgent-1564601.onnx`.

1. Unity das ONNX-Asset importieren lassen.
2. `Player Car 1` auswählen und in `Behavior Parameters` das Asset ins Feld
   `Model` ziehen.
3. `Behavior Type` auf `Inference Only`, `Inference Device` zunächst auf CPU setzen.
4. Play drücken. Ein Python-Trainer ist dafür nicht nötig.

Die Ray-Sensoren, die 18 Vektorbeobachtungen und das Agent-Skript bleiben notwendig.
Zum Weitertrainieren `Behavior Type` wieder auf `Default` setzen.
Die gespeicherte Szene wurde für diesen Export nicht auf Inferenz umgestellt.

## Welche Datei wofür geändert wurde

| Datei | Änderung |
| --- | --- |
| `CarAgent.cs` | 18 statt 3 selbst gesammelte Beobachtungen: lokale Geschwindigkeit (3), Drehgeschwindigkeit (1), Zielrichtung (2), Zielentfernung (1), Restzeit (1), Bodenkontakt (1), neun Bodenfühler (9). Bestehender Ray-Sensor bleibt zusätzlich erhalten. Dichte Fortschrittsrewards, Stillstandserkennung, gemeinsame Abbruchmethode und zusätzliche TensorBoard-Werte. Die zwei Aktionen sind jetzt Lenken und Gas/Bremse/Rückwärts. Manueller Testzugriff auf die nicht vorhandene dritte Aktion entfernt. |
| `TrackSegment.cs` | Mini-Checkpoints ab Einstieg nach Nähe ordnen, bei Episodenbeginn zurücksetzen und jeweils nur den nächsten akzeptieren. |
| `EndlessTrackGenerator.cs` | Ziel wechselt nach Mini- und Hauptcheckpoints. Kein zweiter Streckenaufbau in `Start`, wenn die Episode schon aufgebaut hat. Seed vor dem ersten Shuffle setzen. Checkpoint-Rewards erst nach Reihenfolgeprüfung, UI-Zugriff mit Nullprüfung. |
| `MiniCheckpoint.cs` und `TrackCheckpoint.cs` | Checkpoint erst als eingesammelt markieren, wenn Agent/Generator ihn akzeptiert haben. |
| `CheckpointTimer.cs` | Restzeit als Beobachtung, zentraler Episodenabbruch bei Timeout, kein doppelter Timerstart, Hauptcheckpoint-Rewards über den Agenten. |
| `CarController.cs` | Motor-, Brems- und Lenkradwerte beim Reset löschen. Fußbremse wird nicht mehr an den Hinterrädern überschrieben; alte Bremswerte werden vor dem neuen Steuerschritt gelöscht. |
| `EndlessScene.unity` | Vector Observation Size 18, Decision Period 5, Mini-Checkpoint-Grundreward 0.5. |
| `racer.yaml` | `gamma` 0.99 → 0.995, `time_horizon` 64 → 128, `beta` 0.01 → 0.005, Checkpoints alle 100.000 statt 500.000 Schritte, Obergrenze 2.000.000 statt 500.000 Schritte. Nicht verwendeten `conditioning_type` entfernt. PPO, Netzgröße, Lernrate, Batch- und Buffergröße unverändert. |

Das war eine gemeinsame Änderung an Wahrnehmung, Lernsignal und Ablauf. Die
Tests belegen die Funktion des Gesamtstands; es wurde kein separater Vergleich
jedes einzelnen Eingriffs durchgeführt, der eine einzige Ursache beweisen würde.

`RacerSmokeCheck.cs` war temporärer Testcode für Sensoren, Fahrzeugsteuerung,
Checkpoints und Resets. Er ist aus `Assets` entfernt. In der ignorierten
Testkopie `.utmp/RacerValidation` liegt noch eine Kopie, die vom eigentlichen
Unity-Projekt nicht verwendet wird. `.utmp/racer-smoke.yaml` war die kurze
Testkonfiguration, `Temp/RacerSmokeReport.txt` ihr Prüfbericht. Die
`Racer/...`-Werte in TensorBoard sind dagegen absichtlich bleibende Diagnosewerte.

## Umgebung auf einen anderen PC übertragen

Siehe `TrainingEnvironment/README.md` und `TrainingEnvironment/Restore-Venv.ps1`
im Projektordner. Exportiert sind alle 61 installierten Paketversionen inklusive
pip und setuptools, Python-Version und CPU-PyTorch-Build. Das Transfer-ZIP liegt
unter `.utmp/exports/Racer-Training-Transfer-20261024.zip` und enthält zusätzlich
den aktuellen Trainingslauf mit ONNX-Modellen und fortsetzbaren PT-Checkpoints.

## Was sich geändert hat

- Der bestehende Raycast bleibt erhalten. Neun zusätzliche Bodenabfragen prüfen
  Straße direkt unter/neben dem Auto und 4 bzw. 8 Meter davor. Eigene Collider
  und Trigger werden dabei ignoriert.
- Der Agent beobachtet die Richtung und Entfernung zum nächsten Mini-Checkpoint,
  Geschwindigkeit, Drehgeschwindigkeit, Bodenkontakt und verbleibende Segmentzeit.
- Mini-Checkpoints werden bei jedem Reset entlang des Segments sortiert. Die
  Zielposition wechselt nach jedem Mini- und Hauptcheckpoint. Die Sortierung
  folgt dem jeweils nächsten Punkt ab Segmentbeginn; bei neuen, sich kreuzenden
  Strecken müssen die Wegpunkte entsprechend geprüft werden.
- Nur neue Annäherung an das aktuelle Ziel bringt Fortschrittsreward (0.03 pro
  Meter), und nur mit mindestens drei Rädern auf Road-Collidern. Wiederholtes
  Vor- und Zurückfahren bezahlt dieselben Meter nicht erneut. Stillstehen wird
  nicht positiv belohnt.
- Mini-Checkpoint: 0.5 plus maximal 0.02 Zeitbonus. Hauptcheckpoint: 0.75 plus
  maximal 0.1 Zeitbonus. Nur Checkpoints in Reihenfolge werden bezahlt.
- Sturz, Segmentzeitlimit und acht Sekunden ohne nennenswerten Fortschritt
  beenden die Episode mit -1. Alle Abbrüche laufen über dieselbe Reset-Methode.
- Aktion 0 lenkt. Aktion 1 gibt bei positiven Werten Gas, bei negativen Werten
  bremst sie bzw. fährt bei geringer Geschwindigkeit rückwärts. Die Fußbremse
  erreicht jetzt alle vier Räder und wird beim Loslassen zurückgesetzt.
- Entscheidungen alle fünf Physikschritte statt jeden Schritt; `gamma=0.995`.
  Bei 0.02 s Physikschritten beträgt die Halbwertszeit späterer Rewards damit
  ungefähr 13.8 Sekunden statt zuvor 1.38 Sekunden.

## Fortschritt beurteilen

In TensorBoard sind zusätzlich verfügbar:

- `Racer/Mini Checkpoints` und `Racer/Segments`: pro abgeschlossener Episode
  erreichte Checkpoints. Diese Werte sollen im Verlauf steigen.
- `Racer/Fall`, `Racer/Stuck`, `Racer/Timeout`: Anteil der jeweiligen Abbruchgründe.
- `Racer/Progress Reward`: Fortschrittsreward pro abgeschlossener Episode.

Der Reward allein beweist kein gutes Fahren. Checkpoint-Zahlen und sichtbares
Fahrverhalten mitbeurteilen. Ein kurzes Training prüft den Ablauf, garantiert
aber keine Konvergenz. `max_steps` ist eine Obergrenze, keine Erfolgsgarantie.

## Geprüft am 24.10.2026

C# kompiliert; YAML wird vom installierten ML-Agents-Trainer akzeptiert.
Ein isolierter Test im Unity Play Mode hat geprüft:

- alle acht Segment-Prefabs: Wegpunkte vollständig, wiederholbar und ohne
  doppelte Auszahlung; mehrfacher Episodenreset;
- 18 Beobachtungen und Decision Period 5; Straße/Abgrund durch Bodenfühler
  unterscheidbar; vorhandene Rays treffen Road und Checkpoint;
- tatsächliche Fahrzeugbewegung und positiver Fortschrittsreward vor dem ersten
  Mini-Checkpoint; Rückwärtsfahrt; Bremsen und Loslassen an allen vier Rädern;
- Zielwechsel zum nächsten Segment und Zurückweisung wiederholter Checkpoints;
- Reset bei Sturz, Stillstand und Zeitlimit.

Zusätzlich wurde ein begrenzter PPO-Lauf mit 40.000 Entscheidungen ausgeführt
(gleiche Umgebung und Hyperparameter; konstante Lernrate für den kurzen Test).
Im ersten bzw. letzten Messfenster:

| Messwert | 5.000 Schritte | 40.000 Schritte |
| --- | ---: | ---: |
| Mittlerer Episodenreward | -0.838 | 1.344 |
| Mini-Checkpoints pro Episode | 0.125 | 1.667 |
| Vollständige Segmente pro Episode | 0.031 | 1.000 |

Das letzte Fenster umfasst nur neun Episoden: acht endeten wegen Stillstand,
eine wegen Sturz. Das belegt erste Lernfortschritte, aber noch kein zuverlässiges
Fahren über längere Strecken. Das exportierte Modell und der fortsetzbare
Checkpoint liegen unter `results/CarRacer_RoadFeedback_Smoke_20261024/`.

Referenz: [Unity ML-Agents: Agenten und Rewards](https://unity-technologies.github.io/ml-agents/Learning-Environment-Design-Agents/).

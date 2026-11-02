# Racer: Rennen, Training und TensorBoard

## Szene und Trainingsmodi

Die aktuell gespeicherte `EndlessScene` steht auf Gegnertraining: `Spawn Opponent`
und `Maintain Opponent Presence` ein, `Behavior Type = Default`, Model leer,
Straßen-Sensor 5 Rays pro Richtung, zusätzlicher TrafficSensor aktiv. Für einen
neuen Solo-Lauf zuerst `Racer > Training > 1 - Solo` wählen. Das gespeicherte
Gegnertraining setzt bereits gelernte Solo-Gewichte voraus; Startbefehle siehe unten.
Zuerst den Python-Trainer starten, erst dann Unity Play drücken. Ohne Trainer
verwendet `Default` die Tastatur-Heuristik; das ist kein trainiertes Fahrverhalten.

Für ein Demo-Rennen zuerst einen der Racer-Rennmodi wählen. Der orange Gegner
verwendet weiterhin `CarAgent-1564601.onnx` mit seinen ursprünglichen Sensoren
und wird nicht mittrainiert.

Im Unity-Menü **Racer** gibt es vier Modi (vor Play auswählen):

- **Race two trained models**: beide Wagen benutzen das vorhandene Modell.
- **Race against trained model (keyboard)**: Lenken/Gas über Horizontal/Vertical
  (Pfeiltasten/WASD), Gegner weiter mit Modell.
- **Training > 1 - Solo (new compact run)**: Hauptwagen `Default`, kompakte
  Sensoren, kein Gegner. Hier beginnen.
- **Training > 2 - With opponent (continue compact model)**: dasselbe Sensorformat,
  trainierter Gegner mit wechselnden Startpositionen. Mit dem Solo-Modell fortsetzen.

Die Menübefehle markieren die Szene als geändert; bei Bedarf speichern.
Der Gegner startet mit einstellbarem Versatz `(2.5, 0, 0)` relativ zum Spawn.
Jeder Wagen sammelt seine eigenen Checkpoints in Reihenfolge. Die Straße bleibt
unter dem langsameren Wagen erhalten. Im Gegner-Trainingsmodus wird ein
abgestürzter oder weit entfernter Gegner automatisch nahe dem Hauptwagen wieder
eingesetzt. In den Demo-Rennen wartet ein gescheiterter Gegner weiter auf die
nächste Episode. Scheitert der Hauptwagen, starten beide samt Strecke neu.
Es gibt keine zusätzliche Siegprämie auf der endlosen Strecke.

## Positive, zeitabhängige Rewards

Vor diesem Feedback gab es bereits Fortschrittsrewards und einen kleinen linearen
Zeitbonus. Jetzt gilt:

- Mini-Checkpoint: `0.5 / max(0.1, Sekunden seit letztem Checkpoint)`.
- Segment-Ende: `0.75 / max(0.1, Sekunden seit Segmentbeginn)`.
- Beispiel Mini-CP: 1 Sekunde = 0.5 Reward; 2 Sekunden = 0.25 Reward.
- Fortschritt auf der Straße: weiterhin `0.03` pro neu gewonnenem Meter Richtung
  nächstem Ziel, nur mit mindestens 75 % der Räder auf Road.
- Zusätzlich: `0.06` pro neu gewonnenem Vorwärtsmeter entlang der Richtung vom
  letzten Zielwechsel zum nächsten Checkpoint; mindestens die Hälfte der Räder
  muss Road berühren und unter dem Wagen muss Straße liegen.
- Zusätzlich auf diese Meter: `0.04 * clamp(Vorwärtsgeschwindigkeit / 20, 0, 1)`.
  So bringt dieselbe neue Strecke bei höherem Tempo mehr Reward, auch ohne CP.
  20 m/s entsprechen 72 km/h. Geschwindigkeit ohne neue Strecke zahlt nichts.
- Der Fortschrittsstand bleibt je Ziel erhalten: Zurückfahren und dieselben Meter
  erneut abfahren zahlt nicht nochmals. Fortschritt über das Ziel hinaus wird
  für diese Zusatzrewards nicht bezahlt; Zielwechsel sind weiterhin geordnet.
- Wiederholte/falsche Checkpoints zahlen nichts; Stillstand zahlt nichts.
- Sturz, acht Sekunden ohne Fortschritt und Segment-Timeout beenden die Episode
  ohne negativen Reward. Der Trainer diskontiert spätere Rewards weiterhin.

„Immer positiv“ bedeutet positive Rewards für Erfolge, sonst null. Eine positive
Belohnung pro Warte-Schritt würde Stillstehen belohnen und wird nicht vergeben.
Die Mindestzeit begrenzt Rewards bei Triggern im selben Physikschritt.
Die Formeln setzen einen Anreiz für schnellere Durchfahrten; die tatsächliche
Lernwirkung muss in einem neuen Trainingslauf beurteilt werden.

## Sensorprofile und neues Training

| Profil | Straßen-Rays | Bodenfühler | Auto-Rays | Vektorwerte | Sensorwerte |
| --- | ---: | ---: | ---: | ---: | --- |
| Vorhandenes ONNX / Gegner | 71 | 9 | 0 | 18 | Road: 284 |
| Neues Training (Solo und Gegner) | 11 | 9 | 9 | 18 | Road: 44, Traffic: 27 |

Es gibt jetzt zwei normale, sichtbare `Ray Perception Sensor 3D`-Komponenten:

- Am Hauptwagen: **RoadSensor**, `Rays Per Direction = 5` (11 Rays insgesamt),
  Tags `Road`, `Checkpoint`, Startoffset 2 m, Endoffset -1 m, Länge 15 m.
- Am Kindobjekt **TrafficSensor**: Höhe 0.5 m, `Rays Per Direction = 4`
  (9 Rays insgesamt, 8 verschiedene Richtungen in 45-Grad-Abständen; hinten
  doppelt), `Max Ray Degrees = 180`, Start-/Endoffset 0 m, Länge 20 m,
  Spherecast-Radius 0.35 m, einziger Tag `Car`.

Der Auto-Sensor erkennt auch links/rechts und schräg hinten Fahrzeuge. Seine
Layer-Maske enthält nur `AIPlayer`, also den Gegnerkörper. Das verhindert eigene
Treffer und Treffer auf Checkpoint-Trigger. Straßen oder Mauern werden von diesem
Sensor ebenfalls ausgefiltert; Fahrzeuge werden damit auch durch eine Mauer
hindurch erkannt. Der Sensor ist zur Erkennung anderer Autos bestimmt.
Die tatsächlichen Karosserie-Collider tragen den Tag `Car`; nur den Parent zu
taggen würde für die eingebauten Ray-Sensoren nicht genügen.

Die vorherigen selbst programmierten sechs Spherecasts und deren zwölf
Vektorwerte sind entfernt. Die neun Bodenfühler bleiben erhalten. Das zusätzliche
Ray-Sensor-Set liefert eigene Observations direkt an ML-Agents. Es wird bei
Solo-Training weiter ausgewertet, auch wenn es keine Autos trifft. Solo- und
Gegnertraining besitzen daher exakt dasselbe Eingabeformat.

Die Rays werden nicht mehr beim Play-Start heimlich von 35 auf 5 umgestellt.
Die Szeneneinstellungen sind direkt sichtbar und werden durch die Racer-Menüs
für Training oder Legacy-Demorennen gesetzt. Der bisherige Profil-Checkbox ist
im Inspector entfernt; er war mit dem alten Modell leicht falsch kombinierbar.

**Mit diesem Sensorformat einen neuen Lauf beginnen.** Das alte ONNX und die
vorherigen Profile mit 20 oder 30 Vektorwerten passen nicht zu diesen drei
Sensoreingängen. Dafür kein `--resume` oder `--initialize-from` verwenden.
Erst ein Solo-Lauf mit den neuen nativen Sensoren kann anschließend ohne
Neulernen von null in Phase 2 übernommen werden. Bei späterer Inferenz mit einem
neu trainierten Modell beide Ray-Sensoren samt Einstellungen erhalten.

### Phase 1: Allein fahren lernen

1. Unity-Menü `Racer > Training > 1 - Solo (new compact run)` wählen.
2. Im Projektordner starten:

```powershell
& .\.venv\Scripts\mlagents-learn.exe .\Assets\ML-Agents\racer.yaml --run-id=CarRacer_NativeSolo_01
```

3. Unity Play drücken. Decision Period bleibt 5. Nach 100.000 bis 200.000
   Schritten erstmals Fortschritt prüfen. Das ist ein Kontrollpunkt, kein
   versprochenes Lernziel. Erst zur nächsten Phase wechseln, wenn mehrere
   Segmente über verschiedene Strecken-Seeds zuverlässig geschafft werden.
4. Mit Strg+C im Trainer geordnet beenden und speichern lassen; Unity stoppen.

### Phase 2: Mit Gegner weiterlernen

1. Unity-Menü `Racer > Training > 2 - With opponent (continue compact model)`.
   Der Gegner startet abwechselnd links/rechts mit zufälligem Vorlauf von 0 bis
   6 Metern auf dem Startsegment. Bei einer Überlappung wird der Gegner zusätzlich
   nach vorne verschoben, bis beide Karosserien getrennt sind. Demo-Rennmodi
   behalten einen festen Startplatz.
2. Neue Run-ID, aber Gewichte vom kompakten Solo-Lauf übernehmen:

```powershell
& .\.venv\Scripts\mlagents-learn.exe .\Assets\ML-Agents\racer.yaml --run-id=CarRacer_NativeRace_01 --initialize-from=CarRacer_NativeSolo_01
```

3. Unity Play drücken. Dieser Lauf beginnt mit dem gelernten Fahrverhalten,
   nicht mit zufälligen Gewichten. Seine Schrittzählung und Lernratenplanung
   beginnen neu. Der alte Solo-Lauf bleibt separat für Vergleiche erhalten.

Zum Wiederaufnehmen eines unterbrochenen Laufs mit derselben Run-ID `--resume`
verwenden. Für den Phasenwechsel oben wird bewusst ein eigener Lauf mit
`--initialize-from` verwendet. Beide neuen Phasen haben 18 Vektorwerte + 44 Straßen-Raywerte + 27 Auto-Raywerte;
das alte 18/284-Modell und die zwischenzeitlichen Profile mit 20/30 Vektorwerten
sind hierfür inkompatibel. Die .pt-Checkpoints in `results` aufbewahren; ONNX allein
reicht zum Weitertrainieren nicht.

### Was max_steps bedeutet

`max_steps: 2000000` ist das Gesamtbudget an gesammelten Beobachtung/Aktion-
Erfahrungen des Trainers über alle Episoden. Es ist weder die Zahl der Rennen
noch die maximale Länge einer einzelnen Episode und auch nicht die Zahl der
Gradientenupdates. Bei mehreren lernenden Agenten mit demselben Behavior werden
Erfahrungen addiert. Der feste Inferenz-Gegner wird nicht mitgezählt.

Mit 0.02 Sekunden Physikschritt und Decision Period 5 kommt normalerweise alle
0.1 simulierten Sekunden eine Entscheidung. 2 Millionen Entscheidungen
entsprechen damit größenordnungsmäßig 55.6 Stunden simulierter Fahrzeit eines
Wagens; Episodenenden können die Zahl etwas verändern. Die echte Rechenzeit
hängt von Time Scale, Rechner und Simulation ab. Die Grenze kann wegen der
Verarbeitung ganzer Trajektorien geringfügig überschritten werden.

Die Agent-Komponente hat zusätzlich ein anderes `MaxStep`: ein Limit pro
Episode in Agentenschritten. Das ist hier 0 (kein solches Limit); Sturz,
Stillstand und Segmentzeitlimit begrenzen die Episoden weiterhin.

Das Trainingscap bleibt zunächst bei 2 Millionen. Lernrate und PPO-Epsilon
werden in dieser Konfiguration abhängig vom Fortschritt zum Cap abgesenkt.
Früher stoppen ist möglich; für weiteres Lernen nach einem ausgeschöpften
Budget ein passendes neues Budget planen. Mehr Schritte garantieren keine
Verbesserung: Checkpoint-Zeiten, erreichte Segmente und Fehlerquoten gemeinsam
auf festen Test-Seeds vergleichen.

## TensorBoard und eigene Diagramme

ML-Agents schreibt die über `Academy.Instance.StatsRecorder` erfassten Werte nach
`results/<run-id>/CarAgent`; Ausgabe alle `summary_freq=10000` Trainingsschritte.
Der Gegner schreibt keine eigenen Werte in diese Statistik.

Nach Start des Trainings in einem zweiten Terminal:

```powershell
& .\TrainingEnvironment\Start-TensorBoard.ps1 -RunId CarRacer_NativeRace_01
```

Dann [TensorBoard öffnen](http://localhost:6006). Unter **Custom Scalars** stehen
zusammengestellte Diagramme für Zeiten, Fortschritt/Fehlschläge und Parameters.
Unter **Scalars** sind sämtliche Rohwerte verfügbar. Alternativ einzeln:

```powershell
& .\.venv\Scripts\python.exe .\TrainingEnvironment\tensorboard_layout.py .\results\CarRacer_NativeRace_01
& .\.venv\Scripts\tensorboard.exe --logdir=results --port=6006
```

Wichtige Messwerte:

- `Timing/Average Seconds Per CP`: Mittel aller erreichten Mini-CP-Durchfahrten
  im TensorBoard-Messfenster; niedriger bedeutet schneller.
- `Timing/CP 01 Seconds` bis `CP 20 Seconds`: Zeiten pro Mini-CP-Position innerhalb
  einer Episode, gemittelt über das Messfenster.
- `Timing/Average Seconds Per Segment`: Zeit kompletter Segmente.
- `Timing/Episode Average Seconds Per CP`: Mittelzeit je abgeschlossener Episode
  mit mindestens einem erreichten Mini-CP; Episoden werden gleich gewichtet.
- `Timing/Completed CP Count`, `Racer/Mini Checkpoints`, `Racer/Segments`:
  erreichte Checkpoints je abgeschlossener Episode.
- `Racer/Fall`, `Racer/Stuck`, `Racer/Timeout`: Anteil der jeweiligen Abbruchgründe.
- `Parameters/...`: verwendete Rewardfaktoren, Mindestzeit, Stillstandsgrenze,
  Sensoranzahl und aktivierter Gegner. PPO-Hyperparameter stehen zusätzlich
  in den standardmäßigen ML-Agents-Logs.
- `Rewards/...` und `Racer/Progress Reward`: Checkpoint- und Fortschrittsrewards.

Zeiten enthalten nur erfolgreich erreichte Checkpoints. Immer zusammen mit
Checkpoint-Anzahl und Abbruchraten lesen: ein schneller einzelner Checkpoint
bei häufigen Fehlschlägen bedeutet noch keine bessere Rennleistung. Wegen der
zufälligen Strecke sind CP-Positionen zwischen Episoden nicht identische Kurven;
für Vergleiche einen festen Strecken-Seed verwenden.

## Vorhandener Trainingsstand und Übertragung

Der alte Lauf `CarRacer_RoadFeedback_Smoke_20261024` enthält den Checkpoint bei
1.564.601 Schritten. Das zugehörige ONNX liegt unter `Assets/ML-Agents/Models/`
und wird zusammen mit den anderen exportierten Inferenzmodellen unter
`Assets/ML-Agents/Models` versioniert.
Für Training werden zusätzlich PT-Checkpoints benötigt; `results` bleibt ignoriert.

Die vorigen 40.000-Schritt-Tests vom 24.10.2026 betrafen den damaligen Reward-
und Sensorstand und belegen keine Lernqualität des neuen kompakten Profils.
Eine Python-Umgebung auf einem anderen PC wird mit
`TrainingEnvironment/Restore-Venv.ps1` wiederhergestellt; Details stehen in
`TrainingEnvironment/README.md`. Der alte Transfer-Export enthält den alten
Projektstand und muss mit den aktuellen Assets/Skripten ergänzt werden.

Referenz: [Unity ML-Agents: TensorBoard und eigene Metriken](https://unity-technologies.github.io/ml-agents/Using-Tensorboard/).

## Geprüft am 31.10.2026

Unity 6000.0.84f1: C# kompiliert; isolierte Play-Mode-Prüfungen für Legacy-
und Kompaktprofil bestanden. Geprüft wurden Modellaktionen des Gegners,
Sensorformen (18/284 bzw. damals 20/44), Startpositionen auf der Straße, unabhängige
Checkpoint-Stände, Reihenfolge/Doppelauszahlung, Erhalt der Straße für den
langsameren Wagen, Gegnerabbruch, gemeinsamer Reset, zeitabhängige Rewardformel
und Gegnererkennung mit/ohne Fahrzeug vor dem Sensor.

`racer.yaml` wird vom installierten Trainer akzeptiert. Die erzeugte Custom-
Scalars-Konfiguration wurde mit TensorBoards EventAccumulator gelesen.
Es wurde kein neuer Lernlauf bis zur Konvergenz ausgeführt; die Fahrqualität des
kompakten Profils und des neuen Reward-Schemas ist noch nicht gemessen.

Die anschließende Erweiterung verwendet 30/44 in beiden Trainingsphasen.
Die Prüfung umfasst nun auch seitliche/diagonale/hintere Erkennung, Verdecken
hinter Hindernissen, Solo-Beobachtungen ohne Gegner und wechselnde Startplätze.

## Korrektur am 01.11.2026

Die gespeicherte Szene wurde auf Solo-Training gestellt und der zusätzliche
Auto-Sensor durch eine native Ray-Perception-Komponente ersetzt. Im alten Stand
meldete das Unity-Log `Sensor shapes must match. [30] != [18]`: das alte Modell
war mit anderen Observations betrieben worden. Der Gegner wird nun bereits vor
seiner Aktivierung positioniert, danach explizit für die erste Episode
initialisiert; eine Überschneidungsprüfung hält beide Karosserien getrennt.

Unity-Play-Mode-Prüfungen für diesen Stand bestanden: Solo ohne Gegner und ohne
Eigentreffer; aktive native Sensoren mit 44/27 Raywerten und 18 Vektorwerten;
Legacy-Gegner weiter mit 284/18; erster Spawn und zwölf gemeinsame Resets ohne
Karosserieüberschneidung; keine explosive Startgeschwindigkeit; Car-Erkennung
in acht Richtungen; zurückgesetzter Gegner auf der Straße.

## Training im Hintergrund

`Run In Background` ist in den Player Settings eingeschaltet. Unity und den
Python-Trainer während des Trainings geöffnet lassen; das Unity-Fenster muss
nicht im Vordergrund sein. Nach Alt+Tab prüfen, ob im Trainer die Step-Zahl
weiter steigt. Ein pausierter Play Mode oder Windows-Energiesparmodus wird durch
diese Einstellung nicht aufgehoben.

Erster Prüfpunkt: 100.000 bis 200.000 Schritte. Wenn die erreichten Segmente
zunehmen und Abbrüche abnehmen, Solo-Training weiterlaufen lassen. Sobald mehrere
Segmente auf verschiedenen Strecken-Seeds zuverlässig geschafft werden, kann
Phase 2 folgen. 2 Millionen Schritte bleiben das vorläufige Gesamtbudget, kein
zwingendes Mindestziel. Laufzeit in Stunden ist rechnerabhängig; die Step-Zahl
ist für den Vergleich aussagekräftiger.

## Dichtere Vorwärtsrewards am 01.11.2026

Der erste native Solo-Lauf erreichte bei 70.000 bis 90.000 Schritten keine
Checkpoints mehr; der mittlere Reward lag fast bei null und sämtliche Episoden
endeten mit Stillstand. Der ursprüngliche 0.03/m-Reward war weiterhin vorhanden,
kam in diesem Lauf aber fast nicht mehr an. Die zusätzlichen Strecken- und
Geschwindigkeitsrewards verstärken nun das Signal vor dem ersten Checkpoint.
Das ist keine Garantie für Konvergenz; die Lernwirkung muss erneut beobachtet
werden. Sensoren, Aktionen, CP-Zeitrewards und positive Reward-Vorzeichen bleiben
unverändert. Derselbe native Lauf kann daher mit `--resume` fortgesetzt werden.
Beim Vergleich alter/neuer Rewards den Änderungszeitpunkt beachten.

Zusätzliche TensorBoard-Werte:
- `Racer/Forward Metres`: neue belohnte Vorwärtsmeter je Episode (kein Kilometerzähler).
- `Rewards/Forward Distance` und `Rewards/Forward Speed`: getrennte Rewardsummen.
- `Racer/Average Road Contact`: mittlerer Anteil der Räder auf Road je Episode.
- `Parameters/Forward Distance Reward Per Metre`, `Parameters/Forward Speed Bonus Per Metre`
  und `Parameters/Speed Reward Reference`: verwendete Rewardfaktoren.

Für die neuen Custom-Scalars-Gruppen den TensorBoard-Layout-Befehl bzw. das
Start-TensorBoard-Skript erneut ausführen. Neue Scalar-Tags erscheinen auch ohne
diesen Schritt. Play-Mode-Prüfungen mit echter Radphysik bestätigen Rewards vor
dem ersten CP und den Erhalt des ursprünglichen Fortschrittsrewards; gezielte
Prüfungen bestätigen mehr Reward für dieselbe Strecke bei höherem Tempo sowie
keine Auszahlung für Stillstand, Wiederholungen, Rückwärtsfahrt, Luft und Offroad.

## Gegner in der Nähe halten (02.11.2026)

`Racer > Training > 2 - With opponent` schaltet `Maintain Opponent Presence` ein.
Das feste alte ONNX bleibt der Gegner; Rewards, Aktionen und Sensorformen ändern
sich nicht. Ein vorhandener nativer Gegner-Lauf kann mit `--resume` fortgesetzt
werden. Beim erstmaligen Wechsel weiterhin die Solo-Gewichte über
`--initialize-from` in einen neuen Gegner-Lauf übernehmen.

- Bei Sturz, Stillstand oder Timeout wird der Gegner zunächst deaktiviert. Nach
  mindestens 0.5 simulierten Sekunden versucht der Hauptwagen, ihn wieder einzusetzen.
- Ist der aktive Gegner mehr als 15 Meter entfernt (vorne oder hinten), wird er
  ebenfalls wieder herangeführt. Zwischen erfolgreichen Platzierungen liegt
  mindestens 1 simulierte Sekunde. Danach darf er ganz normal wieder wegfahren.
- Einstiegspunkte werden 6 bis 12 Meter entlang der nächsten Checkpoints gesucht,
  bei Bedarf mit einem seitlichen Versatz von 1 Meter. Kurven und der Übergang
  ins nächste Segment werden anhand der geordneten Wegpunkte berücksichtigt.
- Die Straße wird unter der Karosseriemitte und an vier Ecken geprüft; eine
  Überschneidung mit dem lernenden Wagen schließt den Platz aus. Der Gegner
  erhält eine zur Streckenrichtung passende Orientierung und die passende
  Vorwärtsgeschwindigkeit des Hauptwagens, begrenzt auf 25 m/s.
- Der Gegner bekommt einen eigenen, zum Einstiegspunkt passenden Checkpoint-Stand.
  Übersprungene Checkpoints werden nicht ausgezahlt. Am Hauptwagen bleiben
  Strecke, Checkpoint-Reihenfolge, Episode und Rewards unberührt.
- Gibt es gerade keinen sicheren Platz oder ist der Hauptwagen selbst nicht
  ausreichend auf der Straße, wird gewartet und erneut versucht. Kurze Lücken
  sind damit möglich; der Gegner bleibt nicht mehr bis zum Episodenende weg.
- Die Eingriffe sind für Training gedacht. Normale Demo-Rennen schalten die
  automatische Wiederannäherung aus, Solo schaltet den Gegner weiterhin aus.

Zusätzliche TensorBoard-Werte (Anwesenheit wird ohne zusätzliche Sensor-Rays
etwa einmal pro simulierter Sekunde erfasst):

- `Opponent/Within Range`: Anteil der Stichproben mit aktivem Gegner innerhalb
  der 15-Meter-Grenze.
- `Opponent/Detected`: Anteil mit Gegnerkontakt in den zuletzt erfassten
  TrafficSensor-Rays; Abstand allein garantiert wegen Ray-Zwischenräumen keinen Treffer.
- `Opponent/Rejoins Per Episode`, `Opponent/Failure Rejoins Per Episode`,
  `Opponent/Distance Rejoins Per Episode`: Wiedereinstiege je Hauptwagen-Episode.
- `Opponent/Placement Unavailable`: Anteil der tatsächlich versuchten
  Platzierungen, für die kein sicherer Einstiegspunkt gefunden wurde.

Die Custom-Scalars-Gruppe `Opponent presence` erscheint nach erneutem Ausführen
von `tensorboard_layout.py` bzw. `Start-TensorBoard.ps1` für den Gegner-Lauf.
Die Rohwerte erscheinen auch im Scalars-Tab.

Unity-Play-Mode-Prüfungen bestanden: automatische Reaktivierung nach Ausfall,
Wiederannäherung bei großem Abstand, Abstand und Straßenkontakt beim Einstieg,
passende Route auch nach zwei abgeschlossenen Segmenten, keine Überschneidung,
keine Änderung von Hauptwagen-Pose/Reward/Schrittzahl/Checkpoint, keine erzwungene
Platzierung ohne Straße, deaktivierte Eingriffe in Solo und Demo sowie
unveränderte Sensorformen für Hauptwagen und Legacy-Gegner.

## Mitgelieferte exportierte Modelle

| Datei in Assets/ML-Agents/Models | Quelle | Eingänge |
| --- | --- | --- |
| CarAgent-1564601.onnx | Alter RoadFeedback-Lauf, 1.564.601 Schritte; fester Legacy-Gegner | 284 Raywerte + 18 Vektorwerte |
| CarAgent-2000011.onnx | NativeSolo_01, 2.000.011 Schritte | 44 Straßen-Raywerte + 27 Auto-Raywerte + 18 Vektorwerte |
| CarAgent-2000009.onnx | NativeRace_01, 2.000.009 Schritte | 44 Straßen-Raywerte + 27 Auto-Raywerte + 18 Vektorwerte |

Die beiden neuen Modelle wurden anhand ihrer SHA-256-Prüfsummen den lokalen
Trainingsläufen zugeordnet. Zum Ausprobieren das neue Modell am Hauptwagen
mit beiden nativen Sensoren und `Inference Only` betreiben; nicht im
Legacy-Rennmodus mit 35 Rays pro Richtung. Der feste Gegner-Slot bleibt auf das
Legacy-Modell ausgelegt. Die ONNX-Dateien sind für Inferenz; fortsetzbare
PT-Checkpoints und TensorBoard-Verläufe unter `results` bleiben lokale Dateien.
Automatisch erzeugte Dateien unter `Assets/ML-Agents/Timers` werden nicht mehr
versioniert.

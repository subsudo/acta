# Status

## Stand
Stand dieser Doku: 2026-04-29
Aktueller sichtbarer Versionsstand: `Acta v0.9.7`

## Technischer Gesamtzustand
- Build zuletzt grün
- Hauptapp lauffaehig
- One-file-Exe-Workflow vorhanden
- Stundenplan-Diagnose technisch vorhanden, aber nicht mehr prominent in der Hauptoberflaeche
- Odoo-/Header-Cache aktiv

## Was aktuell funktional vorhanden ist
- Live-Suche mit TN-Kuerzeln
- temporaere Liste und gespeicherte Listen
- kompakter Detailbereich
- Fotoanzeige / Platzhalter
- Odoo-Link aus DOCX-Header
- Mini-Stundenplan aus `KW_*.docx`
- Word-Aktionen inkl. Bookmark-Spruengen
- `Akte` oeffnet jetzt Word-nativ ueber die Shell; Bookmark-Aktionen (`BU`/`BI`/`BE`/`LB`) laufen weiter seriell ueber einen zentralen `WordStaHost` auf einem dedizierten STA-Thread
- Single-Instance-Start: zweite Starts holen die laufende App nach vorne statt eine zweite Instanz zu oeffnen
- Log-Zugang direkt ueber die Einstellungen
- Hauptfenster startet jetzt bei jedem Start frisch auf dem Primary-Monitor und nutzt `SystemParameters.WorkArea` statt alter `Left`/`Top`-Restore-Logik
- subtiler Aktivitaetsindikator beim Index-Refresh in der Statusleiste
- native Windows-Titelleiste ist wieder aktiv
- die sichtbare Versionsnummer sitzt jetzt im Fenstertitel als `Acta vX.Y.Z`
- Refresh und Einstellungen sitzen wieder in der normalen oberen Aktionsleiste neben `Details`
- Gespeicherte Listen speichern Inhaltsänderungen jetzt still im Hintergrund; die Header-Aktionen `Leeren` und `Als Liste speichern` bleiben nur noch für die temporäre Liste sichtbar
- Optionale lokale Teilnehmernotizen sind als angedockte Notizspalte vorhanden; sie speichern pro Windows-User und Teilnehmer automatisch im lokalen XAMLPackage-Format.
- Der Detailbereich ist etwas kompakter gestellt: Foto sitzt leicht höher, der Fotorahmen ist enger gefasst, die Odoo-Karte entfällt und Foto/Odoo/Stundenplan rücken ruhiger zusammen; die Stundenplanfläche selbst bleibt unverändert
- die fruehere Auto-fit- und Sonderlogik fuer Spalten-/Fensterbreiten ist wieder entfernt; Listen-, Haupt- und Detailspalte arbeiten wieder ueber die stabile Standard-Splitterlogik
- UI-Skalierung arbeitet jetzt mit 5 Stufen; neue Nutzer starten kompakter und bestehende 4-Stufen-Werte werden einmalig auf die neue Logik migriert
- Word-Aktionen sind global gegen Doppelauslösung abgesichert; parallele zweite Klicks werden verworfen
- Lock-Rennen bei `docs.Open()` werden fachlich als Sperrfall behandelt statt als rohe COM-Fehlermeldung
- Gesperrte Akten werden nicht mehr ueber einen Acta-eigenen ReadOnly-Dialog geoeffnet: Word uebernimmt jetzt wieder seinen nativen Dialog `Dokument wird verwendet` und die eigene ReadOnly-UX
- Vor Word-Aktionen erzwingen Statusmeldung, Wait-Cursor und Render-Flush ein sofort sichtbares Feedback; im Detailfenster läuft derselbe Wait-Cursor ohne zusätzliche Status-UI
- Word-Fenster werden von Acta nicht mehr aktiv platziert; Word entscheidet Monitor, Größe und Position selbst
- Kleine Stabilitätsrunde ist drin: Odoo-/Ordner-`Process.Start(...)` ist gegen Shell-Fehler abgesichert, die Modul-Normalisierung toleriert doppelte Keys aus fehlerhaften JSON-Daten, der Such-Regex ist kompiliert, und Theme-Brushes werden beim Erzeugen eingefroren
- Der Mini-Stundenplan setzt seine Standard-Textfarben jetzt wieder über Resource-Referenzen statt über lokale Brush-Werte; das ist robuster bei Theme-Wechseln und spart wiederholte `FindResource(...)`-Aufrufe
- Der COM-Bookmark-Pfad bleibt bei Sperren fail-closed: technische Fehler oder ReadOnly-Zustaende werden nicht mehr still als erfolgreicher Bookmark-Fall akzeptiert, sondern an den nativen Word-Open-Pfad abgegeben
- Such- und Listenpfade wurden leicht entlastet: das TN-Index-Dictionary wird gecacht, unnötige Lowercase-Allokationen in der Suche entfallen, und reine Layout-/Skalierungswechsel erzwingen keine pauschalen `Items.Refresh()`-Aufrufe mehr
- Vor Word-Aktionen zeigt das Hauptfenster kurz `Öffne Dokument...`, ohne einen dauerhaften Word-Status einzuführen
- Word-Aktionen geben jetzt auch unmittelbar sichtbares UI-Feedback: Wait-Cursor und Render-Flush sorgen dafür, dass die Rückmeldung vor dem kurzen COM-Freeze tatsächlich sichtbar wird
- Bei nativem `Akte`-Open gibt es nur einen kurzen UI-Cooldown gegen Doppelklicks; die eigentliche Sperr- und ReadOnly-Behandlung liegt dann komplett bei Word
- `E BU` und `E BI` sind nicht mehr Teil von Acta-Quick-Actions
- Bekannte harmlose Word-Sonderfälle bei `UserControl` und `Hwnd` werden defensiver geloggt, damit normale Öffnungen die Logs nicht mehr mit dauernden Warnungen füllen
- Der `Start`-Pfad toleriert jetzt beide Realstrukturen: TN-Ordner direkt auf erster Ebene oder genau eine Zwischenebene tiefer; tiefer wird bewusst nicht rekursiv gesucht
- Der Mini-Stundenplan skaliert die LP-/Zimmerzeile bei kleineren UI-Stufen jetzt kompakter und mit etwas mehr Innenabstand, damit die kurzen Randtexte in den Zellen nicht mehr so leicht abgeschnitten werden
- Odoo-Links aus DOCX-Headern werden vor dem Öffnen jetzt auf echte absolute `http/https`-URLs begrenzt; ungültige oder nicht erlaubte Ziele werden freundlich geblockt statt an `Process.Start(...)` durchgereicht
- Die statischen Status-Brushes des Mini-Stundenplans sind eingefroren (`Freeze()`), damit der WPF-Pfad sauberer und günstiger bleibt
- Der Header-Metadaten-Cache baut sein Persist-Dokument jetzt noch unter Lock auf, schreibt aber die JSON-Datei erst danach; damit blockiert der Cache andere Zugriffe nicht mehr unnötig mit File-I/O
- Das separate Detailfenster öffnet Teilnehmerordner jetzt ebenso robust wie das Hauptfenster, mit Logging und Warnmeldung statt nackter Shell-Exception
- Die `ext`/`disp`-Statuszellen des Mini-Stundenplans hängen jetzt ebenfalls an der UI-Skalierung und wirken auf großen Stufen nicht mehr zu klein
- Der Wochenplan-Cache schreibt seine JSON-Datei nicht mehr innerhalb des Cache-Locks; das reduziert unnötige Blockierung bei langsamerem Dateisystemzugriff
- Mini-Stundenplan `disp.` wird jetzt nur noch über echte rote Markierung erkannt (`w:highlight=red` oder rotes `w:shd`/`fill`); rote Schrift allein gilt nicht mehr als dispensiert, damit neue TN nicht fälschlich als `disp.` erscheinen
- Der Wochenplan-Cache behandelt temporäre Lesefehler jetzt robuster: ein gesperrtes `KW_*.docx` überschreibt keinen guten Cache mehr mit leerem Inhalt, und nach einer Frist wird erneut versucht zu lesen statt den alten Wochenstand endlos als frisch zu betrachten
- GitHub-Auto-Update ist jetzt vorhanden: Acta prüft im Hintergrund auf das neueste stabile Release, zeigt bei Bedarf einen Update-Dialog, lädt `Acta.exe` nach `%LOCALAPPDATA%\XHub\updates\pending` und startet einen eingebetteten externen Updater für den EXE-Austausch

## Produktnaher Ist-Zustand
- Sichtbarer Produktname in der UI ist aktuell `Acta`; interner Projekt-/Repo-Name bleibt vorerst `XHub`.
- First-Run startet standardmaessig im Hellmodus.
- First-Run-Defaults fuellen bereits LV-, LB- und Stundenplan-Pfad mit den aktuellen FuturX-Standardpfaden vor.
- Die sichtbare Versionsnummer sitzt nicht mehr in der Statusleiste, sondern im nativen Fenstertitel des Hauptfensters.
- Die obere Aktionsleiste bleibt als ruhiger, vollflaechiger Header-Strip unter der nativen Titelleiste; zwischen Suchbereich und Hauptinhalt gibt es bewusst keine harte Trennlinie.

### Stabil / brauchbar
- Kernworkflow Suche -> Liste -> Akte/Ordner
- lokale Persistenz
- UI-Grundlayout
- Import in temporaere Liste
- Listenverwaltung
- Index-Refresh laeuft im Hintergrund weiter, ohne die UI sichtbar zu blockieren

### In echtem Test weiter absichern
- Stundenplan-Matching gegen echten Bestand
- Odoo-Link-Erkennung bei gesperrten oder anders strukturierten DOCX-Dateien
- Word-Verhalten nach dem nativen `Akte`-Open und dem COM-Bookmark-Fallback im echten Alltag weiter beobachten

## Aktuelle offene Punkte
- Stundenplan-Matching ist konservativ, aber noch nicht final belastbar fuer alle Namensformen
- Odoo-Link-Erkennung ist im echten Serverbestand nicht in allen Faellen stabil bestaetigt
- einige historische Docs widersprechen dem heutigen Codezustand
- Diagnose-Logik fuer den Stundenplan ist noch im Code, aber nicht mehr als Hauptbutton exponiert

## Was fuer die naechste AI/Weiterarbeit wichtig ist
- primaere aktuelle Projektdoku liegt in `docs/`
- historische Dateien bleiben wichtig, aber sind nicht automatisch aktuelle Wahrheit
- private lokale Realtest-Artefakte koennen fuer Verifikation weiter sinnvoll sein, gehoeren aber bewusst nicht ins öffentliche Repo
- wichtige Debug-Artefakte:
  - `weekly-schedule-diagnostics.json`
  - `weekly-schedule-diagnostics.bak`
  - Modell dazu: [WeeklyScheduleDiagnosticsDocument.cs](../XHub/Models/WeeklyScheduleDiagnosticsDocument.cs)

## Stundenplan-Diagnose
- Die Diagnose-Schreiblogik ist weiterhin vorhanden, damit Realtests bei Bedarf auswertbar bleiben.
- Zielort:
  - `%LOCALAPPDATA%\XHub\diagnostics`
- Inhalt:
  - erkannte Slots
  - Unterrichtsbloecke
  - Rohzeilen
  - Match-Kandidaten
  - Ergebnis pro TN

## Logs
- Log-Dateien liegen unter `%LOCALAPPDATA%\XHub\logs`
- In den Einstellungen gibt es unter `Daten` einen direkten Button `Log-Ordner oeffnen`
- Das ist aktuell der pragmatische Support-/Debug-Zugang fuer verteilte Tests
- Der Updater schreibt zusätzlich `updater-YYYY-MM-DD.log` in denselben Log-Ordner

## Praktische Stolpersteine fuer Git/GitHub
- Root ist auf Quellcode, Doku, Mockups und bewusst behaltene Handover-Dateien bereinigt
- `bin/` und `obj/` liegen weiter lokal im Projekt und bleiben ueber `.gitignore` draussen
- One-file-Publish-Ordner sollen lokal ausserhalb des versionierten Zustands bleiben
- einige Root-Markdown-Dateien sind einmalige Handoffs, keine laufende Projektdoku

## Annahmen
- die aktuelle Codebasis im Unterordner [XHub](../XHub) ist die massgebliche Quelle
- Publish-Artefakte sind lokale Outputs und keine beabsichtigten Repo-Inhalte

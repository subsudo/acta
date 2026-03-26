# Status

## Stand
Stand dieser Doku: 2026-03-19
Aktueller sichtbarer Versionsstand: `Acta v0.9.2`

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
- Word-Fensterlogik mit optionalem `maximiert auf Monitor X`
- Single-Instance-Start: zweite Starts holen die laufende App nach vorne statt eine zweite Instanz zu oeffnen
- Log-Zugang direkt ueber die Einstellungen
- Fensterposition wird bei geaenderten Monitor-Setups auf sichtbare Screens geprueft und faellt sonst auf den Hauptbildschirm zurueck
- subtiler Aktivitaetsindikator beim Index-Refresh in der Statusleiste
- native Windows-Titelleiste ist wieder aktiv
- die sichtbare Versionsnummer sitzt jetzt im Fenstertitel als `Acta vX.Y.Z`
- Refresh und Einstellungen sitzen wieder in der normalen oberen Aktionsleiste neben `Details`
- die fruehere Auto-fit- und Sonderlogik fuer Spalten-/Fensterbreiten ist wieder entfernt; Listen-, Haupt- und Detailspalte arbeiten wieder ueber die stabile Standard-Splitterlogik
- UI-Skalierung arbeitet jetzt mit 5 Stufen; neue Nutzer starten kompakter und bestehende 4-Stufen-Werte werden einmalig auf die neue Logik migriert
- Word-Aktionen sind global gegen Doppelauslösung abgesichert; parallele zweite Klicks werden verworfen
- Lock-Rennen bei `docs.Open()` werden fachlich als Sperrfall behandelt statt als rohe COM-Fehlermeldung
- Vor Word-Aktionen erzwingen Statusmeldung, Wait-Cursor und Render-Flush ein sofort sichtbares Feedback; im Detailfenster läuft derselbe Wait-Cursor ohne zusätzliche Status-UI
- Kleine Stabilitätsrunde ist drin: Odoo-/Ordner-`Process.Start(...)` ist gegen Shell-Fehler abgesichert, die Modul-Normalisierung toleriert doppelte Keys aus fehlerhaften JSON-Daten, der Such-Regex ist kompiliert, und Theme-Brushes werden beim Erzeugen eingefroren
- Der Mini-Stundenplan setzt seine Standard-Textfarben jetzt wieder über Resource-Referenzen statt über lokale Brush-Werte; das ist robuster bei Theme-Wechseln und spart wiederholte `FindResource(...)`-Aufrufe
- Die Word-ReadOnly-Prüfung ist jetzt diagnostizierbar und fail-closed: technische Fehler beim Sperrstatus werden geloggt und nicht mehr still als „Akte ist frei“ behandelt
- Such- und Listenpfade wurden leicht entlastet: das TN-Index-Dictionary wird gecacht, unnötige Lowercase-Allokationen in der Suche entfallen, und reine Layout-/Skalierungswechsel erzwingen keine pauschalen `Items.Refresh()`-Aufrufe mehr
- Vor Word-Aktionen zeigt das Hauptfenster kurz `Öffne Dokument...`, ohne einen dauerhaften Word-Status einzuführen
- Word-Aktionen geben jetzt auch unmittelbar sichtbares UI-Feedback: Wait-Cursor und Render-Flush sorgen dafür, dass die Rückmeldung vor dem kurzen COM-Freeze tatsächlich sichtbar wird
- Der `Start`-Pfad toleriert jetzt beide Realstrukturen: TN-Ordner direkt auf erster Ebene oder genau eine Zwischenebene tiefer; tiefer wird bewusst nicht rekursiv gesucht
- Der Mini-Stundenplan skaliert die LP-/Zimmerzeile bei kleineren UI-Stufen jetzt kompakter und mit etwas mehr Innenabstand, damit die kurzen Randtexte in den Zellen nicht mehr so leicht abgeschnitten werden
- Odoo-Links aus DOCX-Headern werden vor dem Öffnen jetzt auf echte absolute `http/https`-URLs begrenzt; ungültige oder nicht erlaubte Ziele werden freundlich geblockt statt an `Process.Start(...)` durchgereicht
- Die statischen Status-Brushes des Mini-Stundenplans sind eingefroren (`Freeze()`), damit der WPF-Pfad sauberer und günstiger bleibt
- Der Header-Metadaten-Cache baut sein Persist-Dokument jetzt noch unter Lock auf, schreibt aber die JSON-Datei erst danach; damit blockiert der Cache andere Zugriffe nicht mehr unnötig mit File-I/O
- Das separate Detailfenster öffnet Teilnehmerordner jetzt ebenso robust wie das Hauptfenster, mit Logging und Warnmeldung statt nackter Shell-Exception
- Die `ext`/`disp`-Statuszellen des Mini-Stundenplans hängen jetzt ebenfalls an der UI-Skalierung und wirken auf großen Stufen nicht mehr zu klein
- Der Wochenplan-Cache schreibt seine JSON-Datei nicht mehr innerhalb des Cache-Locks; das reduziert unnötige Blockierung bei langsamerem Dateisystemzugriff
- Mini-Stundenplan `disp.` wird jetzt nur noch über echte rote Markierung erkannt (`w:highlight=red` oder rotes `w:shd`/`fill`); rote Schrift allein gilt nicht mehr als dispensiert, damit neue TN nicht fälschlich als `disp.` erscheinen

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
- Word-Verhalten auf mehreren Monitoren

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

## Praktische Stolpersteine fuer Git/GitHub
- Root ist auf Quellcode, Doku, Mockups und bewusst behaltene Handover-Dateien bereinigt
- `bin/` und `obj/` liegen weiter lokal im Projekt und bleiben ueber `.gitignore` draussen
- One-file-Publish-Ordner sollen lokal ausserhalb des versionierten Zustands bleiben
- einige Root-Markdown-Dateien sind einmalige Handoffs, keine laufende Projektdoku

## Annahmen
- die aktuelle Codebasis im Unterordner [XHub](../XHub) ist die massgebliche Quelle
- Publish-Artefakte sind lokale Outputs und keine beabsichtigten Repo-Inhalte

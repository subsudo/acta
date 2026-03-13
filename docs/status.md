# Status

## Stand
Stand dieser Doku: 2026-03-13

## Technischer Gesamtzustand
- Build zuletzt grün
- Hauptapp lauffaehig
- One-file-Exe-Workflow vorhanden
- Stundenplan-Diagnose aktiv
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

## Produktnaher Ist-Zustand

### Stabil / brauchbar
- Kernworkflow Suche -> Liste -> Akte/Ordner
- lokale Persistenz
- UI-Grundlayout
- Import in temporaere Liste
- Listenverwaltung

### In echtem Test weiter absichern
- Stundenplan-Matching gegen echten Bestand
- Odoo-Link-Erkennung bei gesperrten oder anders strukturierten DOCX-Dateien
- Word-Verhalten auf mehreren Monitoren

## Aktuelle offene Punkte
- Stundenplan-Matching ist konservativ, aber noch nicht final belastbar fuer alle Namensformen
- Odoo-Link-Erkennung ist im echten Serverbestand nicht in allen Faellen stabil bestaetigt
- einige historische Docs widersprechen dem heutigen Codezustand
- Diagnose-Features sind aktuell noch absichtlich produktnah eingebaut, um Realtests abzusichern

## Was fuer die naechste AI/Weiterarbeit wichtig ist
- primaere aktuelle Projektdoku liegt in `docs/`
- historische Dateien bleiben wichtig, aber sind nicht automatisch aktuelle Wahrheit
- wichtige Realtest-Artefakte:
  - [KW_11.docx](../KW_11.docx)
  - [XHub/Beispielakte.docx](../XHub/Beispielakte.docx)
  - [XHub/MockServer](../XHub/MockServer)
- wichtige Debug-Artefakte:
  - `weekly-schedule-diagnostics.json`
  - `weekly-schedule-diagnostics.bak`
  - Modell dazu: [WeeklyScheduleDiagnosticsDocument.cs](../XHub/Models/WeeklyScheduleDiagnosticsDocument.cs)

## Stundenplan-Diagnose
- Die Diagnose wird aktuell bewusst produktnah geschrieben, damit Realtests auswertbar bleiben.
- Zielort:
  - bevorzugt `diagnostics\` neben der App/Exe
  - sonst Fallback nach `%LOCALAPPDATA%\XHub\diagnostics`
- Inhalt:
  - erkannte Slots
  - Unterrichtsbloecke
  - Rohzeilen
  - Match-Kandidaten
  - Ergebnis pro TN

## Praktische Stolpersteine fuer Git/GitHub
- Root ist aktuell mit Publish-Ordnern und Build-Artefakten gefuellt
- `bin/` und `obj/` liegen im Projekt
- mehrere alte One-file-Publish-Ordner duerfen nicht zur Quellwahrheit werden
- einige Root-Markdown-Dateien sind einmalige Handoffs, keine laufende Projektdoku

## Annahmen
- die aktuelle Codebasis im Unterordner [XHub](../XHub) ist die massgebliche Quelle
- Publish-Artefakte im Root sind historische Outputs, keine beabsichtigten Repo-Inhalte

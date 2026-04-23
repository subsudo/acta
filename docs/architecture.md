# Architecture

## Grundform
XHub ist eine WPF-App mit pragmatischer Service-Struktur und zentraler UI-Orchestrierung im Codebehind. Der sichtbare Produktname ist aktuell `Acta`, intern bleiben Projektname und Namespace vorerst `XHub`. Die Architektur ist bewusst modular genug fuer Alltagserweiterungen, aber nicht auf abstrakte Enterprise-Schichten optimiert.

## Einstiegspunkte
- [App.xaml.cs](../XHub/App.xaml.cs): Bootstrap, Pfade, Theme, UI-Skalierung, Start
- [MainWindow.xaml](../XHub/MainWindow.xaml): Hauptlayout mit nativer Windows-Titelleiste und ruhigem Arbeits-Header darunter
- [MainWindow.xaml.cs](../XHub/MainWindow.xaml.cs): Hauptworkflow, UI-Steuerung und pragmatische Fensterlogik

## Hauptmodule

### Modelle
- [AppConfig.cs](../XHub/Models/AppConfig.cs): globale fachliche Konfiguration
- [UserPrefs.cs](../XHub/Models/UserPrefs.cs): lokale Benutzerpraeferenzen
- [ParticipantIndexEntry.cs](../XHub/Models/ParticipantIndexEntry.cs): Such-/Indexobjekt pro TN
- [ParticipantMiniSchedule.cs](../XHub/Models/ParticipantMiniSchedule.cs): reduzierte Stundenplananzeige
- [WeeklyScheduleDiagnosticsDocument.cs](../XHub/Models/WeeklyScheduleDiagnosticsDocument.cs): Diagnosemodell fuer Stundenplan-Parsing und Matching
- Listen-, Import- und Exportmodelle fuer lokale Datenhaltung

### Services
- [AppLogger.cs](../XHub/Services/AppLogger.cs): Datei-Logging nach `%LOCALAPPDATA%\XHub\logs`
- [JsonStorage.cs](../XHub/Services/JsonStorage.cs): sicheres JSON-Lesen/-Schreiben mit Backup-Muster
- [AppConfigService.cs](../XHub/Services/AppConfigService.cs) / [UserPrefsService.cs](../XHub/Services/UserPrefsService.cs): Config/Prefs
- [ListRepository.cs](../XHub/Services/ListRepository.cs): gespeicherte Listen
- [ExportService.cs](../XHub/Services/ExportService.cs): Export/Import lokaler Listendaten
- [SearchTextUtility.cs](../XHub/Services/SearchTextUtility.cs): Tokenisierung, Normalisierung und Suchhilfen
- [InitialsResolver.cs](../XHub/Services/InitialsResolver.cs): Kuerzelableitung aus Akten-/Dateinamen
- [ParticipantIndexService.cs](../XHub/Services/ParticipantIndexService.cs): Dateisystem-Scan und Indexaufbau
- [ParticipantSearchService.cs](../XHub/Services/ParticipantSearchService.cs): Suche
- [WordStaHost.cs](../XHub/Services/WordStaHost.cs): zentraler app-weiter STA-Worker fuer alle Word-Aktionen
- [WordService.cs](../XHub/Services/WordService.cs): Word COM, Dokumente, Bookmarks und ReadOnly-Fallback auf dem STA-Thread
- [AppUpdateService.cs](../XHub/Services/AppUpdateService.cs): GitHub-Release-Check, Snooze, Download und Updater-Start
- [DocxHeaderMetadataService.cs](../XHub/Services/DocxHeaderMetadataService.cs): Header-Metadaten direkt aus DOCX
- [WeeklyScheduleService.cs](../XHub/Services/WeeklyScheduleService.cs): Stundenplan-Parsing, Matching, Cache, Diagnose
- [AttendanceImportService.cs](../XHub/Services/AttendanceImportService.cs): textbasierter Listenimport
- Platzhalter-Services ohne produktive Funktion:
  - [OdooSyncService.cs](../XHub/Services/OdooSyncService.cs)
  - [ScheduleService.cs](../XHub/Services/ScheduleService.cs)
  - [DayResponsibilityService.cs](../XHub/Services/DayResponsibilityService.cs)

### Views / Controls
- [SettingsWindow.xaml](../XHub/Views/SettingsWindow.xaml): Einstellungen, inklusive Datenaktionen und direktem Log-Ordner-Zugriff
- [AppUpdateWindow.xaml](../XHub/AppUpdateWindow.xaml): modaler Update-Dialog im Scola-Muster
- [ParticipantDetailPanel.xaml](../XHub/Controls/ParticipantDetailPanel.xaml): eingebetteter Detailbereich im Hauptfenster
- [ParticipantDetailWindow.xaml.cs](../XHub/Views/ParticipantDetailWindow.xaml.cs): separates Detailfenster als historische/alternative View, aktuell nicht der Primaerpfad
- [ModuleSettingsWindow.xaml.cs](../XHub/Views/ModuleSettingsWindow.xaml.cs): Reihenfolge und Sichtbarkeit der Detailmodule
- weitere kleine Dialogfenster fuer Import, Texteingaben und Hinweise
- Die Statusleiste im Hauptfenster traegt Lauftext, Indexzustand und den subtilen Aktivitaetsindikator fuer den Index-Refresh
- Die sichtbare Versionsnummer laeuft jetzt wieder ueber den nativen Fenstertitel (`Acta vX.Y.Z`)
- `Refresh` und `Einstellungen` liegen in der normalen oberen Aktionsleiste neben `Details`
- Die eigentliche Spalten- und Splitterlogik im Hauptbereich bleibt bewusst bei der stabilen Standard-Grid-Steuerung ohne eigenes Auto-fit

## Hauptdatenfluesse

### 1. TN-Index
Dateisystem -> `ParticipantIndexService` -> `ParticipantIndexEntry[]` -> `ParticipantSearchService` -> UI

### 2. Lokale Listen
UI -> `ListRepository` -> `lists.json` / `lists.bak`

### 3. Odoo- und Header-Metadaten
Detailansicht -> `DocxHeaderMetadataService` -> DOCX ZIP/XML -> lokaler Cache -> UI

### 4. Stundenplan
Stundenplan-DOCX -> `WeeklyScheduleService` -> Wochen-Cache + Diagnose + `ParticipantMiniScheduleSummary` -> UI

### 5. Word
UI-Aktion -> Pfadauflösung im UI -> `WordStaHost.RunAsync(...)` -> `WordService` auf dediziertem STA-Thread -> bestehende oder neue Word-Instanz -> Dokument/Bookmark

### 6. App-Update
Start -> `AppUpdateService` -> GitHub `releases/latest` -> optional `AppUpdateWindow` -> eingebetteter `ActaUpdater.exe` -> EXE-Austausch -> Neustart

## Persistenz
Standardpfad:
- `%LOCALAPPDATA%\XHub`

Wichtige Dateien:
- `settings.json`
- `user-prefs.json`
- `lists.json`
- `header-metadata-cache.json`
- `weekly-schedule-cache.json`
- `update-state.json`
- `logs\app-YYYY-MM-DD.log`

Diagnose:
- unter `%LOCALAPPDATA%\XHub\diagnostics`
- wichtigste Datei: `weekly-schedule-diagnostics.json` auf Basis von [WeeklyScheduleDiagnosticsDocument.cs](../XHub/Models/WeeklyScheduleDiagnosticsDocument.cs)
- die Diagnose-Schreiblogik ist noch im Code vorhanden, aber nicht mehr ueber einen prominenten Haupt-Button erreichbar

## Externe Abhaengigkeiten
- WPF / .NET 8
- Word COM, spaet gebunden
- GitHub Releases API fuer den optionalen Update-Check
- Dateisystem und DOCX-Dateien als Hauptdatenquelle

## Architekturentscheidungen, die sichtbar im Code stecken
- keine starke MVVM-Aufteilung; `MainWindow.xaml.cs` ist bewusst ein zentraler Orchestrator
- Suchlogik arbeitet gegen einen vorbereiteten Index, nicht gegen das Dateisystem pro Eingabe
- DOCX-Inhalte werden bevorzugt direkt gelesen statt ueber sichtbares Word
- Matching bei Stundenplan/Odoo ist konservativ: Unsicherheit fuehrt eher zu leerer Anzeige

## Relevante historische Referenzen
- [HANDOVER_XHUB.md](../XHub/HANDOVER_XHUB.md): Grundkontext, aber teils veraltet
- [HANDOVER_UX_OVERHAUL.md](../XHub/HANDOVER_UX_OVERHAUL.md): wichtige UX-Historie
- [SCHEDULE_MATCHING_HANDOFF.md](../SCHEDULE_MATCHING_HANDOFF.md): Stundenplan-Logik und Edge Cases
- [WORD_LEAK_FIX_FOR_XHUB.md](../WORD_LEAK_FIX_FOR_XHUB.md): Word-Cleanup-Logik

## Offene Architekturrisiken
- viel Produktlogik sitzt in `MainWindow.xaml.cs`
- Stundenplan-Matching bleibt fragil, solange der echte Plan handgepflegt und uneinheitlich ist
- Word COM bleibt grundsaetzlich ein stoeranfaelliger Randbereich, jetzt aber seriell ueber einen zentralen STA-Host entkoppelt
- Repo-Root ist inzwischen auf Quellcode, Doku, Mockups und bewusst behaltene historische Handovers reduziert; lokale Build- und Publish-Artefakte sollen weiterhin ausserhalb des versionierten Zustands bleiben

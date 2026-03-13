# Architecture

## Grundform
XHub ist eine WPF-App mit pragmatischer Service-Struktur und zentraler UI-Orchestrierung im Codebehind. Die Architektur ist bewusst modular genug fuer Alltagserweiterungen, aber nicht auf abstrakte Enterprise-Schichten optimiert.

## Einstiegspunkte
- [App.xaml.cs](../XHub/App.xaml.cs): Bootstrap, Pfade, Theme, UI-Skalierung, Start
- [MainWindow.xaml](../XHub/MainWindow.xaml): Hauptlayout
- [MainWindow.xaml.cs](../XHub/MainWindow.xaml.cs): Hauptworkflow und UI-Steuerung

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
- [NavigatorWordService.cs](../XHub/Services/NavigatorWordService.cs): Word COM, Dokumente, Bookmarks, Fenstersteuerung
- [DocxHeaderMetadataService.cs](../XHub/Services/DocxHeaderMetadataService.cs): Header-Metadaten direkt aus DOCX
- [WeeklyScheduleService.cs](../XHub/Services/WeeklyScheduleService.cs): Stundenplan-Parsing, Matching, Cache, Diagnose
- [AttendanceImportService.cs](../XHub/Services/AttendanceImportService.cs): textbasierter Listenimport
- Platzhalter-Services ohne produktive Funktion:
  - [OdooSyncService.cs](../XHub/Services/OdooSyncService.cs)
  - [ScheduleService.cs](../XHub/Services/ScheduleService.cs)
  - [DayResponsibilityService.cs](../XHub/Services/DayResponsibilityService.cs)

### Views / Controls
- [SettingsWindow.xaml](../XHub/Views/SettingsWindow.xaml): Einstellungen
- [ParticipantDetailPanel.xaml](../XHub/Controls/ParticipantDetailPanel.xaml): eingebetteter Detailbereich im Hauptfenster
- [ParticipantDetailWindow.xaml.cs](../XHub/Views/ParticipantDetailWindow.xaml.cs): separates Detailfenster als historische/alternative View, aktuell nicht der Primaerpfad
- [ModuleSettingsWindow.xaml.cs](../XHub/Views/ModuleSettingsWindow.xaml.cs): Reihenfolge und Sichtbarkeit der Detailmodule
- weitere kleine Dialogfenster fuer Import, Texteingaben und Hinweise

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
UI-Aktion -> `NavigatorWordService` -> bestehende oder neue Word-Instanz -> Dokument/Bookmark

## Persistenz
Standardpfad:
- `%LOCALAPPDATA%\XHub`

Wichtige Dateien:
- `settings.json`
- `user-prefs.json`
- `lists.json`
- `header-metadata-cache.json`
- `weekly-schedule-cache.json`
- `logs\app-YYYY-MM-DD.log`

Diagnose:
- bevorzugt `diagnostics\` neben der App/Exe
- sonst Fallback nach AppData
- wichtigste Datei: `weekly-schedule-diagnostics.json` auf Basis von [WeeklyScheduleDiagnosticsDocument.cs](../XHub/Models/WeeklyScheduleDiagnosticsDocument.cs)

## Externe Abhaengigkeiten
- WPF / .NET 8
- Word COM, spaet gebunden
- Windows Forms FrameworkReference nur fuer Monitor-Erkennung
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
- Word COM bleibt grundsaetzlich ein stoeranfaelliger Randbereich
- Repo-Root enthaelt viele Artefakte und historische Einzeldateien, die fuer Git sauber ausgesiebt werden muessen

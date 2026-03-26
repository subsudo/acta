# START HERE: XHub

## Kurzkontext
`XHub` ist ein neues WPF-.NET-8-Projekt neben `AkteX`.

Es ist **kein** zweites `AkteX`, sondern ein:
- schneller Teilnehmer-Navigator
- persoenliches Listen-Tool
- aktennahes Detailwerkzeug

Hauptworkflow in V1:
1. Teilnehmer suchen
2. zu persoenlichen Listen hinzufuegen
3. Detailansicht nutzen
4. Ordner / Akte / BU / BI / BE direkt oeffnen

Nicht Hauptworkflow:
- Anwesenheitslisten-Import

## Lies diese Dateien zuerst
1. `<repo-root>\XHub\HANDOVER_XHUB.md`
2. `<repo-root>\XHub\App.xaml.cs`
3. `<repo-root>\XHub\Services\ParticipantIndexService.cs`
4. `<repo-root>\XHub\Services\ParticipantSearchService.cs`
5. `<repo-root>\XHub\Services\ListRepository.cs`
6. `<repo-root>\XHub\Services\NavigatorWordService.cs`
7. `<repo-root>\XHub\MainWindow.xaml`
8. `<repo-root>\XHub\MainWindow.xaml.cs`

## Wichtige Architekturentscheidungen
- lokale Persistenz in `%LOCALAPPDATA%\XHub`
- `lists.json` + `lists.bak`
- atomare JSON-Speicherung
- kein Live-Dateisystemscan pro Tastendruck
- stattdessen In-Memory-Index
- V1 bleibt Single-User und lokal
- externe Akten / Ordner werden read-only behandelt
- XHub schreibt in V1 nicht in Word
- Kuerzel werden nur aus klar definierter Dateinamensstelle gelesen

## Was aktuell schon implementiert ist
- eigenes Projekt `XHub`
- App-Start mit Config/UserPrefs
- Logging
- Suchindex ueber Teilnehmerordner
- Live-Suche
- persoenliche Listen
- Detailansicht mit Modulen
- Modulkonfiguration pro Liste
- optionaler Listenimport
- Export/Import fuer Listen
- Word-Aktionen fuer Akte / BU / BI / BE
- Settings-Fenster

## Was bewusst noch nicht umgesetzt ist
- Odoo-Integration
- Stundenplanlogik
- Tagesverantwortungsmodus
- Notizen / To-dos / Checklisten
- produktive Bildquelle
- Rollenmodell

## Lokaler Build
```powershell
dotnet build <repo-root>\XHub\XHub.csproj
```

## Lokaler Start
```powershell
dotnet run --project <repo-root>\XHub\XHub.csproj
```

## Wichtig fuer Folgearbeit
Wenn du XHub weiterentwickelst:
- halte den Fokus auf Suche + Listen + Detailansicht
- kippe die App nicht wieder in ein `AkteX`-artiges Listenparser-Tool
- uebernimm aus `AkteX` nur robuste Infrastruktur, nicht den gesamten Workflow

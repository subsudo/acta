# Handover: XHub (WPF, .NET 8) - Stand 2026-03-07

## 1) Produktidee und Abgrenzung
XHub ist ein neues, eigenstaendiges WPF-Desktopprojekt neben `AkteX`.

Zielbild:
- schneller Navigator fuer Teilnehmende
- persoenliche Listen pro Benutzer
- Detailansicht pro Teilnehmenden
- direkter Zugriff auf Ordner, Akte, BU, BI, BE
- spaeter erweiterbar fuer Bilder, Odoo, Stundenplaene und Tagesverantwortung

Wichtige Abgrenzung zu `AkteX`:
- `AkteX` ist listen-/anwesenheitsgetrieben
- `XHub` ist such- und listengetrieben
- der Listenimport existiert in `XHub` nur als untergeordneter Zusatz, nicht als Hauptworkflow

V1-Ziel:
- Single-User-App pro Windows-Profil
- lokale Persistenz in `%LOCALAPPDATA%\XHub`
- read-only gegen externe Akten-/Ordnerstrukturen
- keine produktive Odoo-Integration in V1
- keine Stundenplanlogik in V1
- keine Notizen/Checklisten in V1

## 2) Projektstruktur
Projektordner:
- `C:\Users\chris\Desktop\Verlaufsakten_App\XHub`

Wichtige Dateien:
- `XHub.csproj`
- `App.xaml`
- `App.xaml.cs`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`

Modelle:
- `Models\AppConfig.cs`
- `Models\UserPrefs.cs`
- `Models\SavedList.cs`
- `Models\SavedListItem.cs`
- `Models\ParticipantIndexEntry.cs`
- `Models\DetailModuleConfig.cs`
- `Models\DetailModuleKeys.cs`
- `Models\AttendanceImportResult.cs`
- `Models\IndexBuildResult.cs`
- `Models\ExportPackage.cs`

Services:
- `Services\AppLogger.cs`
- `Services\JsonStorage.cs`
- `Services\AppConfigService.cs`
- `Services\UserPrefsService.cs`
- `Services\ListRepository.cs`
- `Services\ExportService.cs`
- `Services\SearchTextUtility.cs`
- `Services\InitialsResolver.cs`
- `Services\ParticipantIndexService.cs`
- `Services\ParticipantSearchService.cs`
- `Services\AttendanceImportService.cs`
- `Services\NavigatorWordService.cs`

Vorbereitete Erweiterungs-Services:
- `Services\OdooSyncService.cs`
- `Services\ScheduleService.cs`
- `Services\DayResponsibilityService.cs`

Dialoge / Views:
- `Views\SettingsWindow.xaml`
- `Views\SettingsWindow.xaml.cs`
- `Views\TextPromptWindow.xaml`
- `Views\TextPromptWindow.xaml.cs`
- `Views\AttendanceImportWindow.xaml`
- `Views\AttendanceImportWindow.xaml.cs`
- `Views\ModuleSettingsWindow.xaml`
- `Views\ModuleSettingsWindow.xaml.cs`

Zusatzmaterial:
- `Assets\XHub.ico`
- `MockServer\...` (lokaler Testbestand mit Beispiel-TN und DOCX-Dateien)

## 3) Technische Basis
- Framework: WPF
- Target: `net8.0-windows`
- Projektname / Assembly / Title: `XHub`
- Word-Zugriff: COM late-bound via `dynamic`
- keine NuGet-Abhaengigkeit fuer Word-Interop
- visuelle Sprache orientiert sich grob an `AkteX`, aber mit anderer Informationsarchitektur

## 4) Persistenz / Speicherorte
Zur Laufzeit nutzt XHub:
- `%LOCALAPPDATA%\XHub\settings.json`
- `%LOCALAPPDATA%\XHub\user-prefs.json`
- `%LOCALAPPDATA%\XHub\lists.json`
- `%LOCALAPPDATA%\XHub\lists.bak`
- `%LOCALAPPDATA%\XHub\logs\app-YYYY-MM-DD.log`

Wichtige Prinzipien:
- keine Einstellungen neben der EXE
- atomare JSON-Speicherung via `JsonStorage.SaveAtomic(...)`
- Backup-Fallback fuer Listen ueber `lists.bak`
- Logging darf nie App-Funktionalitaet blockieren

## 5) Bootstrapping in `App.xaml.cs`
Verantwortung:
- LocalAppData-Pfade initialisieren
- Default-Config erzeugen
- `settings.json` laden und normalisieren
- `user-prefs.json` laden
- Theme anwenden
- MainWindow starten
- globale Exception-Handler registrieren

Default-Konfiguration aktuell:
- `ServerBasePath = K:\FuturX\20_TNinnen`
- `VerlaufsakteKeyword = Verlaufsakte`
- `WordBuBookmarkName = _Bildung`
- `WordBiBookmarkName = _Berufsintegration`
- `WordBeBookmarkName = _Beratung`
- `AutoRefreshHours = 0`
- Bildpfad vorbereitet, aber standardmaessig deaktiviert

## 6) Aktuelle Haupt-UI in `MainWindow`
Layout:
- obere Kopfzeile mit Suchfeld und Aktionsbuttons
- linke Spalte: persoenliche Listen
- mittlere Spalte:
  - aktuelle Liste
  - Teilnehmer in der Liste
  - Suchtreffer
- rechte Spalte:
  - Teilnehmer-Detailansicht mit Modulen
- untere Statusleiste mit Indexstatus

Top-Aktionen aktuell:
- `Aktualisieren`
- `Liste importieren`
- `Export`
- `Import`
- `Einstellungen`

Listenfunktionen aktuell:
- neue Liste
- Liste umbenennen
- Liste loeschen
- Module pro Liste konfigurieren
- Teilnehmer aus Liste entfernen
- Teilnehmer in Liste hoch/runter sortieren

Suchworkflow aktuell:
- Suche gegen In-Memory-Index
- Treffer erscheinen sofort unterhalb der aktuellen Liste
- Treffer koennen per `Zur Liste` hinzugefuegt werden
- Auswahl eines Treffers oder Listeneintrags aktualisiert die Detailansicht

## 7) Detailansicht / Modulkonzept
V1 verwendet ein einfaches, vordefiniertes Modulsystem.

Aktuelle Module:
- `overview`
  - Ordnerpfad
  - Aktenpfad
  - Quelle (Primaer / Sekundaer / Nicht im Index)
- `image`
  - Platzhalterkarte
  - keine produktive Bildquelle in V1
- `initials`
  - Kuerzel aus dem Dateinamen
- `actions`
  - `Ordner`
  - `Akte`
  - `BU`
  - `BI`
  - `BE`

Pro Liste konfigurierbar:
- Modul sichtbar / unsichtbar
- Modul-Reihenfolge

Wichtig:
- keine freien Felder
- keine benutzerdefinierten Modultypen
- keine Notizen / To-dos / Checklisten in V1

## 8) Listenpersistenz
`ListRepository` verwaltet `lists.json`.

Modell:
- `SavedList`
  - `Id`
  - `Name`
  - `SortOrder`
  - `Items`
  - `Modules`
- `SavedListItem`
  - `ParticipantKey`
  - `SortOrder`

Wichtig:
- `ParticipantKey` ist aktuell der Ordnerpfad
- das ist fuer V1 pragmatisch, aber spaeter nicht ideal, falls Ordner verschoben oder umbenannt werden
- dieses Thema ist ein moeglicher spaeterer Ausbaupunkt

Repository-Verhalten:
- erstellt Default-Liste bei leerem/fehlendem Bestand
- normalisiert Reihenfolgen
- normalisiert fehlende/ungueltige Modulkonfigurationen
- sichert vor dem Schreiben ein Backup

## 9) Suchindex und Suche
### `ParticipantIndexService`
Aufgabe:
- baut einen lokalen In-Memory-Index ueber Teilnehmerordner
- liest nur Top-Level-Ordner
- unterstuetzt Primaer- und optional Sekundaerpfad
- versucht pro Ordner eine passende DOCX mit dem Keyword `Verlaufsakte` zu finden
- extrahiert ein Kuerzel aus dem Dateinamen

Ergebnisstruktur:
- `ParticipantIndexEntry`
  - `ParticipantKey`
  - `DisplayName`
  - `FolderPath`
  - `DocumentPath`
  - `Initials`
  - `ImagePath`
  - `SourceLabel`
  - `SearchTokens`
  - `SearchTokensFallback`

Refresh-Logik:
- Voll-Rebuild beim Start
- manueller Refresh ueber Button
- optionaler Timer-Refresh ueber `AutoRefreshHours`
- neuer Index ersetzt den alten nur nach erfolgreichem Build

### `ParticipantSearchService`
Aufgabe:
- schnelle lokale Live-Suche ueber den Index
- keine Dateisystemsuche pro Tastendruck
- token-basiert, false-positive-arm
- Umlaut-Fallback ueber `SearchTextUtility.ReplaceUmlauts(...)`

Der Scoring-Ansatz ist bewusst einfach:
- Prefix auf DisplayName bevorzugt
- danach Token-Prefix-Matches
- danach Token-Contains-Matches

Import-Fall:
- `ResolveSingleImportedParticipant(...)` versucht einen eindeutigen Match fuer einen importierten Namen zu finden
- bei Mehrdeutigkeit wird kein Treffer erzwungen

## 10) Kuerzel-Logik
`InitialsResolver` liest aktuell Kuerzel nur aus einer klar definierten Stelle im Dateinamen.

Annahme in V1:
- Dateiname enthaelt `_Kuerzel`, z. B. `Verlaufsakte_MuAn.docx`
- nur das Suffix nach dem letzten `_` wird betrachtet
- nur alphanumerische Suffixe mit 2-8 Zeichen werden akzeptiert
- keine Heuristik ueber Ordnername oder Word-Inhalt

Das war bewusst so entschieden, um fragiles Verhalten zu vermeiden.

## 11) Optionaler Listenimport
`AttendanceImportService` implementiert den untergeordneten Listenimport.

Aktueller Stand:
- Importfenster vorhanden
- Nutzer gibt freien Rohtext ein
- Listenname kann gesetzt werden
- Parser nimmt pro Zeile primar den Inhalt vor dem ersten Tab
- ohne Tabs wird versucht, Statusmarker wie `Anwesend`, `Verspaetet`, `Abwesend (...)` vom Namen zu trennen
- danach wird pro Zeile ein eindeutiger Teilnehmer ueber den Suchservice gesucht

Wichtig:
- Import erstellt eine neue persoenliche Liste
- der Import ist bewusst Zusatzfunktion, nicht Hauptworkflow
- keine komplette `AkteX`-Parserlogik wurde uebernommen

## 12) Export / Import lokaler Nutzdaten
`ExportService` exportiert und importiert:
- Listen
- Reihenfolgen
- Modulkonfigurationen pro Liste

Nicht exportiert werden:
- globale App-Settings aus `settings.json`
- `user-prefs.json` (Theme, Fenstergroesse etc.)

Dateiformat:
- JSON
- Standard-Endung im UI: `.xhub.json`

## 13) Word-Integration
`NavigatorWordService` ist die XHub-Variante des Word-Zugriffs.

Unterstuetzt:
- `OpenDocument(...)`
- `OpenDocumentAtBookmark(...)`
- `FindVerlaufsakte(...)`
- `FindVerlaufsakteCandidates(...)`

Verhalten:
- DOCX wird ueber Word COM geoeffnet / fokussiert
- bei Bookmark-Aktionen wird an definierte Textmarken gesprungen
- Fokus / Foreground wird best effort gesetzt
- gesperrte Akten liefern klare Fehlermeldung
- wenn Word nicht vorhanden ist, kommt eine harte Nutzermeldung

Bookmarks aktuell:
- BU: `_Bildung`
- BI: `_Berufsintegration`
- BE: `_Beratung`

Wichtig:
- XHub schreibt in V1 nichts in Word
- nur Oeffnen und Springen
- damit bleibt der Character von XHub read-only bezueglich externer Akten

## 14) Einstellungen
`Views\SettingsWindow` bietet aktuell:
- Primaerpfad
- Sekundaerpfad + Aktivierung
- Dark / Light Theme
- Auto-Refresh-Intervall:
  - Aus
  - 2h
  - 4h
  - 8h
- vorbereiteten Bildpfad + Aktivierung

Noch nicht enthalten:
- Exportpfade
- Odoo-Einstellungen
- Stundenplanpfade im UI
- zentrale Vorlagen

## 15) Vorbereitete, aber absichtlich nicht umgesetzte Erweiterungen
Leere Service-Stubs vorhanden:
- `OdooSyncService`
- `ScheduleService`
- `DayResponsibilityService`

Bedeutung:
- Architekturgrenze ist bewusst gesetzt
- es gibt aber in V1 noch keine produktive Funktion dahinter

Nicht umgesetzt in V1:
- Odoo-Anbindung
- Stundenplan-Einlesen
- Wochenansicht
- Tagesverantwortungsmodus
- Notizen / To-dos / Checklisten
- Bilder aus produktiver Quelle
- zentrale Vorlagen / Baukasten-Vorlagen ueber Teams
- Rollenmodell / Adminrechte

## 16) Testdaten / Mocking
Im Projekt liegt ein einfacher lokaler Mock-Bestand:
- `XHub\MockServer\...`

Enthaelt u. a.:
- `Mueller Anna`
- `Meier Lea`
- `Meier Bern Tim`
- `Mohammad Ali Mostafa`
- `Mohammad Mohammad`
- `Mäder Lena`
- `Mäder Urban`
- usw.

Je Ordner liegt eine `Verlaufsakte_*.docx`.

Damit kann man XHub lokal testen, wenn man in den Einstellungen den Primaerpfad auf diesen Mock-Ordner setzt.

## 17) Bekannte Grenzen / technische Schulden
1. `ParticipantKey` ist aktuell der Ordnerpfad.
- fuer V1 ok
- spaeter evtl. durch stabileren technischen Key ersetzen

2. Kein dediziertes ViewModel/MVVM.
- `MainWindow.xaml.cs` ist aktuell der zentrale UI-Orchestrator
- besser als bei AkteX, aber mittelfristig immer noch ausbaubar

3. Keine Bilder in Produktion.
- nur Platzhalter
- Bildpfad ist nur vorbereitet

4. Keine Multi-Monitor-Sichtbarkeitslogik wie in `AkteX`.
- Fensterposition und -groesse werden gespeichert
- Fallback fuer unsichtbare Monitorkonstellationen ist aktuell noch nicht implementiert

5. Keine Notizen/Checklisten.
- war bewusst aus V1 ausgeschlossen
- deshalb fehlt auch noch ein lokales Metadatenmodell pro Teilnehmer

6. Listenimport ist bewusst einfach.
- keine vollstaendige `AkteX`-Parsingtiefe
- keine Odoo-Importlogik

7. Keine eigene Titelleiste / kein Custom WindowChrome.
- XHub nutzt aktuell das Standard-Windows-Fenster
- war fuer den schnelleren V1-Aufbau bewusst akzeptiert

## 18) Wichtige Designentscheidungen
Diese Entscheidungen sind bewusst gefallen und sollten nicht versehentlich rueckgaengig gemacht werden:

- XHub ist **kein** zweites AkteX
- der Hauptworkflow ist Suche + Listen + Detailansicht
- Listenimport bleibt untergeordnet
- kein Live-Dateisystemscan pro Tastendruck
- immer lokaler In-Memory-Index
- V1 bleibt Single-User und lokal
- V1 liest externe Daten read-only
- V1 schreibt nicht in Word
- Kuerzel nur aus klar definierter Dateinamensstelle
- Bildmodul nur vorbereitet
- Odoo nur spaeter

## 19) Syntaktischer / technischer Status
Letzter erfolgreicher Build:
```powershell
dotnet build C:\Users\chris\Desktop\Verlaufsakten_App\XHub\XHub.csproj
```

Ergebnis zum Zeitpunkt dieses Handovers:
- `0 Fehler`
- `0 Warnungen`

Lokaler Start:
```powershell
dotnet run --project C:\Users\chris\Desktop\Verlaufsakten_App\XHub\XHub.csproj
```

Es wurde noch **keine** portable/single-file EXE fuer XHub gebaut.

## 20) Sinnvolle Einstiegspunkte fuer die naechste KI
Wenn eine neue Codex-Instanz uebernehmen soll, ist diese Lesereihenfolge sinnvoll:

1. `App.xaml.cs`
2. `Models\AppConfig.cs`
3. `Models\SavedList.cs`
4. `Services\JsonStorage.cs`
5. `Services\ListRepository.cs`
6. `Services\ParticipantIndexService.cs`
7. `Services\ParticipantSearchService.cs`
8. `Services\NavigatorWordService.cs`
9. `MainWindow.xaml`
10. `MainWindow.xaml.cs`
11. `Views\SettingsWindow.xaml.cs`
12. dieses Handover

## 21) Empfohlene naechste Ausbauschritte
Wenn XHub weitergebaut wird, ist diese Reihenfolge sinnvoll:

1. funktionaler Test mit MockServer und echtem Pfad
2. UX-Runde fuer Layout, Suchfluss und Detailansicht
3. Multi-Monitor-/Fensterzustands-Absicherung wie in AkteX
4. produktive Bildquelle definieren und `ImageResolver` einfuehren
5. Importlogik verbessern oder an Odoo koppeln
6. Tagesverantwortungsmodus als eigener UI-Pfad
7. Stundenplan-Konzept erst dann, wenn Datenquelle klar standardisiert ist
8. Odoo-Anbindung ueber echten `OdooSyncService`

## 22) Bezug zu AkteX
XHub wurde mit viel Kontext aus AkteX gebaut, ist aber absichtlich kein Clone.

Weiterhin nuetzliche Referenzen im Schwesterprojekt `VerlaufsakteApp`:
- `HANDOVER.md`
- `BUGFIXES.md`
- `SEITENPROJEKT_XHUB_BRIEFING.md`
- `Services\WordService.cs`
- `Services\FolderMatcher.cs`
- `App.xaml.cs`

Wichtig fuer Folge-KIs:
- XHub soll Infrastrukturideen aus AkteX nutzen
- aber nicht schleichend wieder in ein listen-/parsergetriebenes AkteX 2 kippen
- der Fokus muss Suche, persoenliche Listen und Detailansicht bleiben

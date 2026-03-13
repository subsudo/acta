# Feature-Transfer nach AkteX

## Ziel

Diese Notiz beschreibt die in `XHub` bereits umgesetzte Funktionalität, die in `Verlaufsakte - AkteX` übernommen werden soll:

1. Odoo-Link still aus einer `docx`-Akte lesen
2. diesen Link in einem persistenten lokalen Cache ablegen
3. das TN-Kürzel aus dem Dokumentpfad ableiten
4. die Kürzel zusätzlich als Suchbasis verwenden

Die Beschreibung ist absichtlich technisch und umsetzungsnah gehalten, damit das Feature in einer anderen App ohne Rückgriff auf die gesamte XHub-Codebasis portiert werden kann.

## Fachlicher Umfang

Die Funktion liest keine Daten über Word COM aus und schreibt nie in die Akte zurück.

Es geht nur um:

- `docx`-Header rein lesend öffnen
- Hyperlink für Odoo extrahieren
- optional weitere Header-Metadaten extrahieren
- Ergebnis lokal cachen
- bei unveränderten Dateien nur den Cache wiederverwenden

## Warum diese Methode

Der saubere Ansatz ist:

- `docx` direkt als ZIP/XML lesen
- nicht Word starten
- nicht COM verwenden
- keine sichtbare Office-Instanz öffnen

Vorteile:

- deutlich robuster
- keine Word-Leaks
- kein Risiko für offene Word-Sitzungen
- rein read-only
- schneller skalierbar

## Relevante XHub-Bausteine

Die aktuelle Umsetzung in XHub verteilt sich auf diese Dateien:

- [DocxHeaderMetadataService.cs](/C:/Users/chris/Desktop/XHub/XHub/Services/DocxHeaderMetadataService.cs)
- [ParticipantIndexService.cs](/C:/Users/chris/Desktop/XHub/XHub/Services/ParticipantIndexService.cs)
- [InitialsResolver.cs](/C:/Users/chris/Desktop/XHub/XHub/Services/InitialsResolver.cs)
- [ParticipantIndexEntry.cs](/C:/Users/chris/Desktop/XHub/XHub/Models/ParticipantIndexEntry.cs)
- [App.xaml.cs](/C:/Users/chris/Desktop/XHub/XHub/App.xaml.cs)

Für AkteX reicht es, die fachliche Logik daraus nachzubauen. Ein 1:1-Kopieren der UI ist nicht nötig.

## Architekturüberblick

### 1. Indexierung der TN-Ordner

Die App baut zuerst einen Teilnehmerindex aus konfigurierten Basisordnern auf.

Pro Teilnehmer werden mindestens gesammelt:

- `ParticipantKey`
- `DisplayName`
- `FolderPath`
- `DocumentPath`
- `Initials`
- `SearchTokens`
- `SearchTokensFallback`

Wichtig:

- das Kürzel wird nicht aus dem Ordnernamen gelesen
- das Kürzel wird aus dem Dateinamen der Verlaufsakte abgeleitet

Beispiel:

- Dateiname: `Verlaufsakte_KoLu.docx`
- Kürzel: `KoLu`

### 2. Header-Metadaten-Service

Ein separater Service liest bei Bedarf Header-Metadaten aus einer `docx`.

Eingabe:

- voller Pfad zur `docx`

Ausgabe:

- Odoo-URL
- weitere Header-Metadaten, aktuell in XHub zusätzlich `CounselorInitials`

### 3. Persistenter Cache

Der Service prüft vor dem Lesen:

- gibt es einen Cache-Eintrag zu genau diesem Dokumentpfad?
- stimmen `LastWriteTimeUtc` und Dateigröße noch mit der aktuellen Datei überein?

Wenn ja:

- direkt Cache zurückgeben

Wenn nein:

- Header neu lesen
- Cache aktualisieren

## Cache-Strategie

### Speicherort

In XHub liegt der Cache unter:

- `%LOCALAPPDATA%\\XHub\\header-metadata-cache.json`
- Backup: `%LOCALAPPDATA%\\XHub\\header-metadata-cache.bak`

Für AkteX sollte derselbe Ansatz verwendet werden, aber mit eigenem App-Namen, zum Beispiel:

- `%LOCALAPPDATA%\\AkteX\\header-metadata-cache.json`
- `%LOCALAPPDATA%\\AkteX\\header-metadata-cache.bak`

Wichtig:

- nicht in normale Settings mischen
- eigener Cache-File
- eigener Backup-File

### Cache-Schlüssel

Der Cache wird über den vollständigen Dokumentpfad adressiert.

Zusätzlich werden pro Eintrag gespeichert:

- `DocumentPath`
- `LastWriteTimeUtc`
- `Length`
- `OdooUrl`
- weitere Felder nach Bedarf

### Cache-Gültigkeit

Ein Cache-Eintrag ist gültig, wenn:

- Pfad identisch
- `LastWriteTimeUtc` identisch
- `Length` identisch

Damit gilt:

- unveränderte Akten werden nicht neu gescannt
- geänderte Akten werden automatisch neu eingelesen
- neue Akten werden automatisch ergänzt

### Cache-Versionierung

XHub nutzt im Cache eine `Version`.

Aktuell:

- `CacheVersion = 2`

Das ist wichtig, damit alte Cacheformate bei einer späteren Änderung bewusst verworfen werden können.

Für AkteX sollte dieselbe Idee übernommen werden:

- Cache-Datei mit `Version`
- bei Versionsabweichung Cache ignorieren und neu aufbauen

## Cache-Dateiformat

Das aktuelle Modell in XHub ist:

```json
{
  "Version": 2,
  "Entries": [
    {
      "DocumentPath": "C:\\Path\\To\\Verlaufsakte_KoLu.docx",
      "LastWriteTimeUtc": "2026-03-10T09:12:30.0000000Z",
      "Length": 123456,
      "OdooUrl": "https://intern.futurx.ch/web#id=1435&action=565&model=op.student&view_type=form&cids=1&menu_id=396",
      "CounselorInitials": "JR"
    }
  ]
}
```

Für AkteX genügt mindestens:

```json
{
  "Version": 1,
  "Entries": [
    {
      "DocumentPath": "...",
      "LastWriteTimeUtc": "...",
      "Length": 123456,
      "OdooUrl": "..."
    }
  ]
}
```

Wenn AkteX später ebenfalls weitere Headerdaten braucht, sollte das Modell direkt erweiterbar angelegt werden.

## Odoo-Link-Erkennung

### Grundprinzip

Eine `docx` ist ein ZIP-Container. Die Header liegen typischerweise hier:

- `word/header1.xml`
- `word/header2.xml`
- usw.

Die Hyperlink-Beziehungen liegen typischerweise hier:

- `word/_rels/header1.xml.rels`
- `word/_rels/header2.xml.rels`
- usw.

### Ablauf

1. `docx` mit `ZipFile.OpenRead(...)` öffnen
2. alle `word/header*.xml`-Einträge einsammeln
3. je Header die passende `.rels`-Datei laden
4. Relationship-IDs auf echte Targets abbilden
5. im Header nach `w:hyperlink` suchen
6. erstes sinnvolles `http://`- oder `https://`-Ziel als Odoo-Link übernehmen

### Wichtiger Detailpunkt

In Word kann der Link aus zwei Teilen bestehen:

- `Target` in der `.rels`
- optional `anchor` am `w:hyperlink`

XHub setzt beides zusammen:

- wenn `anchor` vorhanden und `Target` noch kein `#` enthält:
  - `Target + "#" + anchor`
- wenn `Target` bereits `#` enthält:
  - `Target + anchor`

Das ist wichtig, weil bei echten Akten der Odoo-Link teilweise erst durch dieses Zusammenführen vollständig wird.

### Auswahlregel

In XHub wird der erste `http(s)`-Link im am besten bewerteten Header genommen.

Nicht berücksichtigt werden:

- `mailto:`
- leere Targets
- nicht auflösbare Relationship-IDs

## Auswahl des richtigen Headers

Eine Akte kann mehrere Header-Dateien enthalten. Nicht jede davon ist fachlich relevant.

Darum bewertet XHub alle Header und wählt den besten.

### Scoring-Idee in XHub

Positiv:

- enthält `Verlaufsakte`
- enthält Odoo-Link
- enthält Beratungsperson

Negativ:

- enthält `Abschlussbericht`
- enthält Platzhalter wie `Nachname`, `Vorname`, `XX`

Damit wird vermieden, dass versehentlich ein Vorlagen-Header oder ein Abschnitts-Header statt des echten Akten-Headers gewählt wird.

### Empfehlung für AkteX

Die Header-Bewertung sollte übernommen werden.

Mindestens:

- `Verlaufsakte` bevorzugen
- Platzhalter-Header abwerten
- Header mit echtem Odoo-Link bevorzugen

## Kürzel-Erkennung

### Quelle

Das TN-Kürzel wird in XHub aus dem Dateinamen der Verlaufsakte gelesen.

Beispiel:

- `Verlaufsakte_KoLu.docx`

Regel:

1. Dateiname ohne Endung
2. an `_` splitten
3. letzten Teil verwenden
4. nur akzeptieren, wenn er dem Regex `^[\\p{L}\\p{N}]{2,8}$` entspricht

### Warum nicht aus dem Ordnernamen

Der Ordnername ist fachlich nicht stabil genug für ein Kürzel.
Der Dateiname der Akte ist dafür in diesem System die zuverlässigere Quelle.

### Suchintegration

XHub hängt das Kürzel direkt in die Such-Tokens des Teilnehmers.

Praktisch:

- Token aus Ordnername
- Fallback-Token ohne Umlaute
- zusätzlich Kürzel in lowercase
- zusätzlich Umlaut-normalisierte Kürzel-Variante

Dadurch findet die Live-Suche:

- Name
- Namensfragmente
- Kürzel

## Ordner- und Dateifilter

Beim Port nach AkteX sollte geprüft werden, ob dieselben Ordnertypen ausgeschlossen werden müssen.

In XHub gibt es aktuell bereits Filter:

- in `LV` alle Ordner mit Präfix `00`, `01`, `02`, `03`, `05` ignorieren
- in `LB` Ordner ignorieren, die mit `_` beginnen

Diese Filter sitzen direkt in der Indexierung, nicht in der UI.

Das ist wichtig:

- technische Sammelordner dürfen gar nicht erst als TN im Index landen

## Wann der Header gelesen wird

In XHub wird der Header nicht beim Start für alle Akten erzwungen.

Stattdessen:

- Index baut zunächst Teilnehmerliste
- Header-Metadaten werden bei Bedarf geladen
- ab dann greift der persistente Cache

Das ist der sicherere und schnellere Ansatz.

Für AkteX gibt es zwei sinnvolle Varianten:

### Variante A: Lazy Loading

- Header erst lesen, wenn ein TN wirklich im Detail angezeigt wird

Vorteile:

- schneller App-Start
- minimale Last

Nachteil:

- erster Zugriff auf einen TN kann minimal später reagieren

### Variante B: Hintergrund-Anreicherung

- nach Indexaufbau still im Hintergrund schrittweise Header lesen

Vorteile:

- UI kann später sofort auf gecachte Daten zugreifen

Nachteil:

- etwas mehr Komplexität

Für AkteX würde ich zunächst Variante A empfehlen, falls dort nicht schon ein bestehender Hintergrundindex existiert.

## Fehlerverhalten

Das Verhalten in XHub ist bewusst defensiv:

- wenn Datei fehlt: leeres Ergebnis
- wenn ZIP/XML nicht lesbar: Warnung loggen, leeres Ergebnis
- wenn Cache nicht gespeichert werden kann: Warnung loggen, aber App nicht blockieren

Wichtig:

- niemals wegen eines defekten Headers die ganze App blockieren
- Metadaten sind Zusatzinformation, nicht kritischer Startpfad

## Read-only-Sicherheit

Diese Funktion ist read-only.

Es passiert:

- ZIP lesen
- XML lesen
- lokalen Cache schreiben

Es passiert nicht:

- Word starten
- Dokument speichern
- Metadaten in die Akte zurückschreiben
- Dateiinhalte ändern

Das sollte in AkteX unbedingt so bleiben.

## Minimaler Implementierungsplan für AkteX

### Schritt 1

Modell für Header-Metadaten definieren:

- `OdooUrl`
- optional weitere Header-Felder

### Schritt 2

Persistentes Cachemodell definieren:

- `Version`
- `Entries[]`
- `DocumentPath`
- `LastWriteTimeUtc`
- `Length`
- `OdooUrl`

### Schritt 3

Service `DocxHeaderMetadataService` nachbauen:

- `Read(documentPath)`
- Cache prüfen
- Header aus ZIP/XML lesen
- Cache aktualisieren

### Schritt 4

Resolver für Kürzel nachbauen:

- Dateiname ohne Endung
- `_`-Suffix extrahieren
- Regex-validieren

### Schritt 5

Teilnehmerindex erweitern:

- `DocumentPath`
- `Initials`
- Such-Tokens um Kürzel ergänzen

### Schritt 6

UI oder Fachlogik anreichern:

- Odoo-Button nur anzeigen, wenn Link vorhanden
- Suche auch auf Kürzel laufen lassen

## Empfohlene Klassen in AkteX

Sinnvolle Port-Struktur:

- `Services/DocxHeaderMetadataService.cs`
- `Services/InitialsResolver.cs`
- `Models/HeaderMetadata.cs`
- `Models/HeaderMetadataCacheDocument.cs`
- `Models/HeaderMetadataCacheEntry.cs`
- Erweiterung des bestehenden TN-Index-Modells um:
  - `DocumentPath`
  - `Initials`
  - `OdooUrl` oder lazy-loaded Metadaten

## Wichtige Übernahmeentscheidungen

### Unbedingt übernehmen

- ZIP/XML statt Word COM
- persistenter Cache mit Backup
- Gültigkeitsprüfung über Zeitstempel + Dateigröße
- Header-Scoring
- Kürzel aus Dokumentpfad
- Cache-Versionierung

### Optional für V1

- Beratungsperson
- zusätzliche Header-Metadaten
- Hintergrund-Vorindexierung

## Testfälle für AkteX

Mindestens diese Fälle testen:

1. Akte mit gültigem Odoo-Link im Header
2. Akte ohne Odoo-Link
3. Akte mit mehreren Headern, davon nur einer relevant
4. Akte mit Platzhalter-Header
5. Akte geändert, Cache muss neu invalidieren
6. Akte unverändert, Cache muss sofort greifen
7. Dateiname ohne gültiges Kürzel
8. Dateiname mit gültigem Kürzel wie `Verlaufsakte_KoLu.docx`

## Kurzfazit

Für AkteX sollte das Feature als Kombination aus drei getrennten Bausteinen portiert werden:

1. robuster `docx`-Header-Leser
2. persistenter lokaler Cache
3. Kürzel-Resolver aus dem Dokumentpfad

Das ist in XHub bereits stabil genug gelöst, um als Vorlage für eine zweite App zu dienen.

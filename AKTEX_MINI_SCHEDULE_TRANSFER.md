# AkteX Mini-Stundenplan Transfer Spec

## Zweck
Diese Datei beschreibt, wie der kompakte Mini-Stundenplan aus `Acta`/`XHub` in `AkteX` uebernommen werden soll.

Ziel ist **keine Neuinterpretation**, sondern eine moeglichst genaue Uebernahme von:
- Datenquelle
- Parsing-Logik
- Matching-Regeln
- UI-Verhalten
- Edge-Case-Behandlung

Wichtig: Der Stundenplan in `AkteX` soll **dieselbe fachliche Logik** verwenden wie in `Acta`, damit Verhalten, Bugs, Korrekturen und Diagnose vergleichbar bleiben.

---

## Kurzfassung

In `AkteX` soll bei Doppelklick auf einen Teilnehmer in der Liste ein kompakter Tray unterhalb des Eintrags ausfahren. Dieser Tray zeigt rechtsbuendig den Mini-Stundenplan im gleichen visuellen Stil wie in `Acta`.

Die Logik dahinter soll:
- aus einem `KW_*.docx`-Wochenplan lesen
- nur die **aktuelle ISO-Kalenderwoche** akzeptieren
- direkt aus DOCX/XML lesen, nicht ueber sichtbares Word
- konservativ matchen
- lieber leer bleiben als falsch zuordnen

---

## Referenzdateien in Acta/XHub

Die Umsetzung in `AkteX` sollte sich primar an diesen Dateien orientieren:

- [WeeklyScheduleService.cs](./XHub/Services/WeeklyScheduleService.cs)
- [ParticipantMiniSchedule.cs](./XHub/Models/ParticipantMiniSchedule.cs)
- [ParticipantDetailPanel.xaml](./XHub/Controls/ParticipantDetailPanel.xaml)
- [ParticipantDetailPanel.xaml.cs](./XHub/Controls/ParticipantDetailPanel.xaml.cs)
- [AppConfig.cs](./XHub/Models/AppConfig.cs)
- [SettingsWindow.xaml](./XHub/Views/SettingsWindow.xaml)
- [SettingsWindow.xaml.cs](./XHub/Views/SettingsWindow.xaml.cs)
- [WeeklyScheduleDiagnosticsDocument.cs](./XHub/Models/WeeklyScheduleDiagnosticsDocument.cs)
- [SCHEDULE_MATCHING_HANDOFF.md](./SCHEDULE_MATCHING_HANDOFF.md)

Fuer optische Referenz ist der aktuelle Mini-Stundenplan in `ParticipantDetailPanel.xaml` massgeblich.

---

## Zielbild in AkteX

### Interaktion
- In der Teilnehmerliste bzw. Arbeitsliste: **Doppelklick auf den Namen oder den ganzen Listeneintrag**
- Daraufhin klappt **unterhalb des Eintrags** ein Tray aus
- Dieser Tray ist **rechtsbuendig** im Eintrag bzw. im Listenbereich verankert
- Der Tray zeigt nur den Mini-Stundenplan, keine grosse Detailansicht

### Verhalten
- Ein zweiter Doppelklick auf denselben Teilnehmer kann den Tray wieder schliessen
- Bei Doppelklick auf einen anderen Teilnehmer:
  - bisheriger Tray zu
  - neuer Tray auf
- Wenn kein sicherer Stundenplan ermittelt werden kann:
  - **keine Fehlermeldung im Tray**
  - stattdessen die kompakte, leicht ausgegraute leere Tabellenstruktur

### Nicht Ziel
- Kein eigenes Popup-Fenster
- Kein grossflaechiger Detaildialog
- Kein sichtbares Diagnose-UI fuer normale Nutzer

---

## Konfigurationsanforderungen fuer AkteX

Der Stundenplanpfad muss **eigenstaendig** konfigurierbar sein und darf **nicht** an die normalen Such-/Serverpfade gekoppelt werden.

### Neue Konfiguration
Es braucht in `AkteX` einen eigenen Pfad wie:

```csharp
public string ScheduleRootPath { get; set; } = string.Empty;
```

### Anforderungen
- eigener Eintrag in den Einstellungen
- eigene Persistenz in der lokalen Config
- klar getrennt von:
  - LV-/TN-Pfad
  - LB-Pfad
  - Start-/Austrittspfad

### UI-Empfehlung fuer AkteX
- Bezeichnung etwa:
  - `Stundenplan (DOCX oder Ordner)`
- erlaubt:
  - direkter Pfad auf eine einzelne private lokale Stundenplan-DOCX
  - oder Ordner mit `KW_*.docx`

---

## Auswahlregel fuer die Wochenplan-Datei

Diese Regel ist fachlich wichtig und soll in `AkteX` **genau gleich** sein:

### Wenn `ScheduleRootPath` eine Datei ist
- genau diese Datei verwenden

### Wenn `ScheduleRootPath` ein Ordner ist
- alle `*.docx` im Ordner pruefen
- nur Dateien mit `KW_...` im Namen gelten als Kandidaten
- es wird **nur** die Datei der **aktuellen ISO-Kalenderwoche** akzeptiert
- wenn die aktuelle Woche fehlt: **kein Stundenplan**

### Wichtig
**Kein Fallback auf naechste Woche.**

Das ist in `Acta` bewusst so umgesetzt:

```csharp
return candidates
    .Where(candidate => candidate.Week == currentWeek && candidate.Year == currentYear)
    .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
    .Select(candidate => candidate.Path)
    .FirstOrDefault();
```

Quelle:
- [WeeklyScheduleService.cs](./XHub/Services/WeeklyScheduleService.cs)

---

## Datenmodell, das in AkteX uebernommen werden sollte

Empfehlung: Das Modell praktisch 1:1 uebernehmen.

Relevante Typen:

```csharp
public enum ParticipantMiniScheduleState
{
    Hidden,
    Unavailable,
    Ready
}

public enum ParticipantMiniScheduleHalfDay
{
    Morning,
    Afternoon
}

public enum ParticipantMiniScheduleCellStatus
{
    None,
    External,
    Dispensed
}
```

```csharp
public sealed class ParticipantMiniScheduleEntry
{
    public string Group { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
}
```

```csharp
public sealed class ParticipantMiniScheduleCell
{
    public string DayKey { get; set; } = string.Empty;
    public ParticipantMiniScheduleHalfDay HalfDay { get; set; }
    public List<ParticipantMiniScheduleEntry> Entries { get; set; } = new();
    public ParticipantMiniScheduleCellStatus Status { get; set; }
    public bool HasSupplementalDaz { get; set; }
}
```

Empfohlene Uebernahme:
- [ParticipantMiniSchedule.cs](./XHub/Models/ParticipantMiniSchedule.cs)

---

## Parsing-Regeln fuer das Wochenplan-Dokument

Die Parsing-Logik in `Acta` ist auf das konkrete Format der privaten lokalen Stundenplan-DOCX abgestimmt.

### Grundannahme
Der Plan wird aus Tabellenstruktur + Paragraphen gelesen. Pro Slot werden Bloecke erkannt.

### Gueltige Gruppen
Diese Gruppen werden aktuell als echte Unterrichtsarten erkannt:

- `BI`
- `BU`
- `MO`
- `LB`
- `PR`
- `WIT`
- `KONV`
- `IND`
- `BU LV`
- `DAZ*`

Codebasis:

```csharp
if (normalized.StartsWith("DAZ", StringComparison.OrdinalIgnoreCase))
{
    group = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
    return true;
}

if (normalized is "BI" or "BU" or "MO" or "LB" or "PR" or "WIT" or "KONV")
{
    group = normalized;
    return true;
}
```

### Administrative Bloecke, die ignoriert werden
- leere Gruppe
- `ADMIN`
- `LB ABEND`

Aktuell in `Acta`:

```csharp
return string.IsNullOrWhiteSpace(block.Group)
       || block.Group.Contains("ADMIN", StringComparison.OrdinalIgnoreCase)
       || string.Equals(block.Group, "LB ABEND", StringComparison.OrdinalIgnoreCase);
```

### Lehrerkuerzel
Lehrerparagraphen werden erkannt, wenn:
- nicht Zeit
- nicht Raum
- 1 bis 3 Tokens
- jedes Token max. 2 Buchstaben

Die Ausgabe wird normalisiert zu Grossbuchstaben, z. B.:
- `ee` -> `EE`
- `E E` -> `EE`

### Raum
Raeume werden mit dieser Logik erkannt:

```csharp
private static readonly Regex RoomRegex =
    new(@"\b([UB])\s*(\d+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

Das bedeutet:
- `U4` -> `U4`
- `U 4` -> `U4`
- `B1` -> `B1`

---

## Matching-Regeln fuer Teilnehmende

Das Matching ist der kritischste Teil. Hier soll `AkteX` die `Acta`-Logik moeglichst exakt uebernehmen.

### Grundsatz
**Lieber leer als falsch.**

Wenn die Zuordnung nicht sicher ist:
- kein Raten
- kein aggressives Fuzzy Matching
- lieber `Unavailable`
- UI zeigt dann nur die leere, ausgegraute Struktur

### Aliasbildung
Die Aliaslogik basiert auf Name-Tokens.

Aktuelle Regeln in `Acta`:
- global eindeutige robuste Einzeltokens duerfen als fuehrender Alias dienen
- robuste Einzeltokens mit mindestens 3 Zeichen bleiben als Kandidaten verfuegbar
- voller mehrteiliger Name wird aufgenommen
- fuer fuehrende Tokens werden Kombinationen erzeugt wie:
  - `jan m`
  - `luna a`
  - `mohammad ar`

Relevant:

```csharp
if (!string.IsNullOrWhiteSpace(primaryToken) && globallyUniqueSingleAliases.Contains(primaryToken))
{
    result.Add(primaryToken);
}

foreach (var token in tokens.Where(token => token.Length >= 3))
{
    result.Add(token);
}

result.Add(string.Join(" ", tokens));
result.Add($"{tokens[i]} {tokens[j][0]}");
result.Add($"{tokens[i]} {tokens[j]}");
```

### Globale Eindeutigkeit
Ein nackter Einzelalias soll nur dann wirklich sicher sein, wenn er im gesamten Teilnehmerbestand eindeutig ist.

Das betrifft z. B.:
- `Jan`
- `Luna`
- `Mohammad`

### Gruppenfilter
Wichtig fuer `LB`:
- `LB`-Bloecke duerfen nur `LB`-Teilnehmende ziehen
- `LB`-Teilnehmende duerfen aber trotzdem in normalen Gruppen erscheinen

Das ist die aktuelle, bewusst vorsichtige Regel:

```csharp
var isLbGroup = string.Equals(group, "LB", StringComparison.OrdinalIgnoreCase)
    || string.Equals(group, "LB ABEND", StringComparison.OrdinalIgnoreCase);
var isLbParticipant = _statusTags.TryGetValue(participantKey, out var statusTag)
    && string.Equals(statusTag, "LB", StringComparison.OrdinalIgnoreCase);

return !isLbGroup || isLbParticipant;
```

### Ambiguitaet
Wenn eine Zeile theoretisch auf einen Teilnehmer passen **koennte**, aber nicht sicher aufgeloest werden kann:
- Zeile als `ambiguous`
- Teilnehmer bleibt eher leer

### Harte Sicherheitsregeln
Diese Regeln sind fachlich wichtig:

#### 1. Maximal 1 regulaerer Treffer pro Halbtag
`DAZ` ist der einzige Zusatzfall, der parallel vorkommen darf.

Wenn ein Teilnehmer in einem Halbtag mehr als einen **nicht-DAZ** Treffer sammelt:
- gesamter Mini-Stundenplan => `Unavailable`
- Meldung intern: `Nicht eindeutig zugeordnet`

In Code:

```csharp
var hasMultipleMatchesPerHalfDay = nonSupplementalMatches
    .GroupBy(match => $"{match.DayKey}|{match.HalfDay}", StringComparer.OrdinalIgnoreCase)
    .Any(group => group.Count() > 1);
```

#### 2. Maximal 10 regulaere Treffer pro Woche
Wenn mehr als 10 nicht-DAZ-Treffer zusammenkommen:
- ebenfalls `Unavailable`

```csharp
if (nonSupplementalMatches.Count > MaxDisplayMatchesPerWeek)
```

#### 3. Kein aktueller Wochenplan => nichts anzeigen
- wenn Datei fehlt
- wenn KW nicht passt
- wenn Parser nichts Lesbares extrahiert

### Typische Edge Cases

#### `Mohammad`
Sehr wichtig. Diese Faelle duerfen nicht zu false positives fuehren.

Beispiele:
- `Mohammad` allein -> zu unspezifisch
- `Mohammad A` -> oft immer noch zu unspezifisch
- `Mohammad Ar` -> kann eindeutig werden

Die Regel fuer `AkteX`:
- nackte oder zu kurze uneindeutige Zusatze sperren
- erst eindeutige Zusatzformen zulassen

#### `Luna`
Wenn es zwei `Luna`-Teilnehmende gibt:
- `Luna` allein sollte eher blockiert werden
- `Luna A` oder `Luna F` darf greifen, wenn eindeutig

#### `Jan`
Wenn ein Teilnehmer `Jan` als fuehrenden Vornamen hat und ein anderer `Jan` nur als spaeteren Namensteil:
- `Jan` soll nicht blind auf beide matchen
- fuehrende, natuerliche Aliasformen sind wichtiger

#### Uebernamen / Nicknames
Fall `Ursina Sina Rhiana Camenzind` -> `Sina`

Nicht-fuehrende Einzeltokens koennen relevant sein, wenn:
- sie global eindeutig sind
- sie genug Substanz haben
- sie in der Praxis echte Stundenplanform sind

Das ist fachlich wichtig, wenn `AkteX` denselben Bestand nutzt.

#### `Sara` vs `Sarah`
Der aktuelle `Acta`-Stand ist hier bewusst noch konservativ. Keine breite fuzzy Suche.

Empfehlung fuer `AkteX`:
- erstmal nicht kreativ werden
- lieber spaeter bewusst um kleine Schreibvarianten erweitern

---

## Status-Sonderfaelle im Mini-Stundenplan

### `ext`
Wenn die Zeile im Plan als extern/gruen markiert ist:
- keine normale Unterrichtsanzeige
- keine Lehrperson
- kein Zimmer
- nur kleines zentriertes `ext`

### `disp`
Wenn krank/dispensiert:
- ebenfalls keine normale Unterrichtsanzeige
- nur kleines zentriertes `disp`

### `DAZ`
`DAZ` ist kein Hauptblock, sondern ein Zusatz.

Wenn ein Teilnehmer regulaeren Unterricht **und** DAZ im selben Halbtag hat:
- Hauptblock bleibt sichtbar
- darunter klein `+DAZ`
- Zellhoehe bleibt gleich

Wichtig:
- `DAZ` darf keine regulaere Hauptzuordnung verdrängen

---

## Visuelle Spezifikation fuer AkteX

Diese Beschreibung soll moeglichst nah an `Acta` bleiben.

### Grundaufbau
- 5 Spalten:
  - `Mo`, `Di`, `Mi`, `Do`, `Fr`
- 2 Inhaltsreihen:
  - Vormittag
  - Nachmittag
- dazwischen ein schmaler Mittagsstreifen

### Kopfzeile
- kleine Tageskuerzel
- leicht dunkler Header-Hintergrund im Dark Mode
- klare Grid-Linien

### Zelle ohne Status
Pro Zelle:
- oben mittig: Unterrichtsart (`BI`, `BU`, `PR`, `MO`, ...)
- darunter links klein: LP-Kuerzel
- darunter rechts klein: Raum
- ggf. darunter klein und heller: `+DAZ`

### Zelle mit `ext` oder `disp`
- keine normale Unterrichtsanzeige
- nur kleines zentriertes Badge
- `ext`: pastellig gruen
- `disp`: pastellig rot

### Bei Unsicherheit / Kein Match
- gleiche Tabellenstruktur
- Inhalt leer bzw. `-`
- visuell leicht ausgegraut
- **keine Fehlermeldung im Tray**

### Tray-Verhalten in AkteX
Empfohlen:
- Tray direkt unter dem Listeneintrag
- rechtsbuendig
- mit ruhigem Innenabstand
- kein grosses Modal
- kein Layoutsprung der gesamten Seite

---

## Empfohlene technische Umsetzung in AkteX

### 1. Konfiguration erweitern

```csharp
public sealed class AppConfig
{
    public string ScheduleRootPath { get; set; } = string.Empty;
}
```

### 2. Einstellungen erweitern
- eigenes Textfeld oder Dateipfad-Auswahl fuer `Stundenplan`
- getrennt von den Such-/Serverpfaden

### 3. Modelle uebernehmen
- `ParticipantMiniScheduleSummary`
- `ParticipantMiniScheduleCell`
- `ParticipantMiniScheduleEntry`
- Status-Enums

### 4. Service uebernehmen
Am besten:
- `WeeklyScheduleService` aus `Acta` als Basis kopieren
- in `AkteX` namespace/integration anpassen
- Matchinglogik nicht “vereinfachen”

### 5. Caching uebernehmen
Empfohlen:
- JSON-Cache fuer geparstes Wochen-Dokument
- Schluessel:
  - `LastWriteTimeUtc`
  - Dateigroesse

### 6. Tray-Komponente bauen
Empfohlene Architektur:
- kleine UserControl-Komponente nur fuer den Mini-Stundenplan
- auf Doppelklick im Listenitem ein-/ausblenden

### 7. Keine Word-Abhaengigkeit
Der Mini-Stundenplan darf in `AkteX` ebenso **ohne sichtbares Word** arbeiten.

---

## Beispiel: Einfache Einbindung im Listen-Workflow

### ViewModel-/Codebehind-Idee

```csharp
private ParticipantIndexEntry? _expandedScheduleParticipant;

private void ParticipantList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (ParticipantListBox.SelectedItem is not ParticipantIndexEntry entry)
        return;

    _expandedScheduleParticipant =
        _expandedScheduleParticipant?.ParticipantKey == entry.ParticipantKey
            ? null
            : entry;

    ParticipantListBox.Items.Refresh();
}
```

### Daten anreichern

```csharp
var weeklyScheduleService = new WeeklyScheduleService(cachePath, cacheBackupPath);

entry.MiniSchedule = weeklyScheduleService.GetParticipantSchedule(
    App.Config.ScheduleRootPath,
    entry,
    allParticipants);
```

### Tray im ItemTemplate

```xml
<StackPanel>
    <!-- normaler Listenbalken -->

    <Border Visibility="{Binding IsScheduleTrayOpen, Converter={StaticResource BoolToVisibilityConverter}}"
            HorizontalAlignment="Right"
            Margin="0,6,0,0">
        <local:MiniScheduleTrayControl DataContext="{Binding MiniSchedule}" />
    </Border>
</StackPanel>
```

Die konkrete Tray-Verkabelung kann in `AkteX` anders aussehen. Wichtig ist:
- rechtsbuendig
- kompakt
- nicht modal

---

## Diagnose und Debug-Empfehlung fuer AkteX

Auch wenn `AkteX` die Diagnose nicht prominent im UI haben soll:
- die technische Diagnose-Schreiblogik lohnt sich
- sie ist fuer Matching-Bugs extrem hilfreich

Empfohlene Uebernahme:
- `WeeklyScheduleDiagnosticsDocument`
- optionaler WriteDiagnostics-Pfad
- Diagnose ablegen in:
  - bevorzugt `diagnostics\` neben der Exe
  - sonst AppData

Wichtig:
- nicht als grosses Nutzer-Feature
- eher als versteckte Support-/Debug-Hilfe

---

## Was in AkteX explizit nicht anders gemacht werden sollte

Damit dieselben Fehler nicht neu entstehen:

- **kein** Fallback auf naechste Kalenderwoche
- **keine** aggressive Fuzzy-Suche fuer Namen
- **keine** automatische Anzeige bei Ambiguitaet
- `DAZ` nur als Zusatz, nicht als Hauptblock
- `LB`-Gruppen nur fuer `LB`-Teilnehmende
- aber `LB`-Teilnehmende nicht pauschal aus normalen Gruppen ausschliessen

---

## Minimaler Transferplan

1. `ScheduleRootPath` in `AkteX` einfuehren
2. `ParticipantMiniSchedule`-Modelle uebernehmen
3. `WeeklyScheduleService` aus `Acta` als Grundlage portieren
4. kompaktes Tray-Control in `AkteX` erstellen
5. Doppelklick-Interaktion in der Teilnehmerliste verdrahten
6. Diagnose optional mit uebernehmen
7. mit realer privater Stundenplan-DOCX und Dummybestand pruefen

---

## Empfehlung fuer die umsetzende KI

Wenn eine andere KI `AkteX` umbaut, sollte sie:
- zuerst diese Datei lesen
- danach die genannten Referenzdateien in `Acta`
- dann **kleine, nachvollziehbare Schritte** machen
- und die Matchinglogik nicht “vereinfachen”, nur weil sie komplex wirkt

Diese Funktion ist fachlich heikel. Die Qualitaet haengt nicht an schoenem XAML, sondern an:
- konservativer Aliaslogik
- korrekter Wochenauswahl
- sauberer Behandlung von `DAZ`, `ext`, `disp`
- und daran, dass Unsicherheit still leer bleibt

---

## Offene AkteX-spezifische Entscheidungen

Diese Punkte muessen in `AkteX` noch lokal entschieden werden:

- genaues Verhalten des Trays bei erneutem Doppelklick
- ob nur ein Tray gleichzeitig offen sein darf
- ob der Wochenplan beim Oeffnen des Trays lazy geladen wird oder schon beim Listenaufbau
- ob die Diagnose fest eingebaut oder nur per verstecktem Supportweg schreibbar ist

Empfehlung:
- lazy laden
- nur ein Tray gleichzeitig offen
- Diagnose technisch behalten, aber nicht prominent anzeigen

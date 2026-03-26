# AkteX Mini-Stundenplan Umsetzungsspezifikation

## Zweck
Diese Datei beschreibt, wie ein kompakter Mini-Stundenplan in `AkteX` eingebaut werden soll.

Sie ist bewusst **standalone** geschrieben:
- ohne Verweise auf lokale Repo-Pfade
- ohne Annahme, dass der implementierende Agent Zugriff auf `Acta/XHub` hat
- mit genug fachlichem und technischem Kontext, damit die Funktion verlässlich umgesetzt werden kann

Ziel ist eine **produktnahe, konservative und robuste** Umsetzung.

---

## Referenzmaterial im Zielrepo

Diese Datei soll idealerweise **zusammen mit dem echten Referenz-Stundenplan** verwendet werden.

### Empfohlene Ablage
Im Zielrepo nebeneinander ablegen:

- `AKTEX_MINI_SCHEDULE_TRANSFER_STANDALONE.md`
- private lokale Stundenplan-DOCX

### Erwartung an die umsetzende KI
Die KI soll:
- zuerst diese Markdown-Datei lesen
- danach **genau die danebenliegende private Stundenplan-DOCX** auswerten
- Parsing und Matching nicht rein theoretisch bauen, sondern gegen dieses echte Dokument absichern

### Warum das wichtig ist
Viele Probleme hängen an der echten Dokumentform:
- Reihenfolge von Gruppe / LP / Raum
- Schreibweise von Namen
- DAZ-Zusätze
- Statusmarkierungen
- konkrete Uneindeutigkeiten bei Aliasen

Ohne das echte Referenzdokument ist die Gefahr gross, dass die Logik zu generisch oder zu optimistisch gebaut wird.

---

## Kurzbeschreibung des Features

In `AkteX` soll bei **Doppelklick auf einen Teilnehmer in der Liste** unterhalb des jeweiligen Eintrags ein kompakter Tray ausfahren.

Dieser Tray soll:
- **rechtsbuendig** ausgerichtet sein
- den **Mini-Stundenplan** der Person zeigen
- optisch kompakt und ruhig bleiben
- keine grosse Detailansicht oder Popup-Logik verwenden

Wenn die Zuordnung des Stundenplans nicht sicher ist, soll:
- **keine Fehlermeldung** im Tray erscheinen
- stattdessen nur eine ruhige, leere bzw. ausgegraute Stundenplanstruktur sichtbar sein

---

## Fachliches Zielbild

Die Funktion soll:
- aus einem echten Wochenstundenplan im DOCX-Format lesen
- pro Teilnehmer einen reduzierten 5x2-Stundenplan ableiten
- konservativ matchen
- lieber **nichts anzeigen** als **falsch anzeigen**

Diese Funktion ist fachlich heikel. Der kritische Punkt ist **nicht** das Zeichnen der Tabelle, sondern:
- das Parsing des Wochenplans
- die Namensauflösung
- die Behandlung von Ambiguitäten
- die Begrenzung unrealistischer Treffer

---

## UI-Zielbild in AkteX

### Interaktion
- Doppelklick auf einen Listeneintrag oder den Namen
- darunter klappt ein Tray aus
- Doppelklick auf denselben Teilnehmer kann den Tray wieder schliessen
- Doppelklick auf einen anderen Teilnehmer:
  - bisheriger Tray zu
  - neuer Tray auf

### Position
- Tray direkt **unterhalb** des geklickten Teilnehmers
- **rechtsbuendig**
- nicht als modal, nicht als eigenes Fenster

### Inhalt des Trays
- 5 Spalten: `Mo`, `Di`, `Mi`, `Do`, `Fr`
- 2 Reihen:
  - Vormittag
  - Nachmittag
- dazwischen ein schmaler Mittagsstreifen

### Pro Zelle
- oben mittig: Unterrichtsart (`BI`, `BU`, `MO`, `PR`, `LB`, ...)
- unten links klein: Lehrperson
- unten rechts klein: Zimmer
- falls vorhanden: darunter klein `+DAZ`

### Sonderstatus
- `ext`:
  - zentriertes kleines Badge
  - keine normale Unterrichtsanzeige
- `disp`:
  - zentriertes kleines Badge
  - keine normale Unterrichtsanzeige

### Unsicher / kein Match
- keine Fehlermeldung
- keine Warnbox
- nur leere bzw. leicht ausgegraute Tabellenstruktur

---

## Optische Spezifikation

Die UI soll bewusst kompakt und ruhig sein.

### Grundcharakter
- kleine, nüchterne Tabelle
- keine farbige Überladung
- keine grossen Labels
- gute Lesbarkeit auf engem Raum

### Kopfzeile
- kleine Tageskürzel
- leicht abgesetzter Hintergrund
- Grid-Struktur klar sichtbar

### Inhalt
- Unterrichtsart mittig und etwas kräftiger
- Lehrperson und Zimmer klein, aber lesbar
- `+DAZ` kleiner und heller

### Statusbadges
- `ext`: kleines pastelliges grünes Badge
- `disp`: kleines pastelliges rotes Badge
- nur hinter dem Text hinterlegt, nicht ganze Zelle vollflächig

### Verhalten bei Unsicherheit
- Zelle bleibt ruhig
- kein Text wie `Nicht eindeutig zugeordnet`
- keine laute Fehlerkommunikation in der UI

---

## Konfigurationsanforderung

`AkteX` braucht einen **eigenen Stundenplanpfad**, der komplett unabhängig von den normalen Such-/Serverpfaden ist.

### Neue Konfigurationsproperty

```csharp
public string ScheduleRootPath { get; set; } = string.Empty;
```

### UI in den Einstellungen
Es braucht ein eigenes Feld mit etwa dieser Bezeichnung:

- `Stundenplan (DOCX oder Ordner)`

### Wichtig
Dieser Pfad ist **nicht**:
- LV/TN-Pfad
- LB-Pfad
- Startpfad
- Austrittspfad

Er ist eine **separate Datenquelle** nur für den Mini-Stundenplan.

---

## Erlaubte Eingaben für den Stundenplanpfad

### Fall 1: Direkter DOCX-Pfad
Wenn `ScheduleRootPath` auf eine einzelne Datei zeigt:
- genau diese Datei verwenden

### Fall 2: Ordnerpfad
Wenn `ScheduleRootPath` ein Ordner ist:
- darin nach `*.docx` suchen
- nur Dateien mit `KW_...` im Namen als Kandidaten betrachten

Beispiele:
- private lokale Stundenplan-DOCX
- alternative private lokale Stundenplan-DOCX
- `KW-11.docx`
- `KW 11.docx`

---

## Wochenauswahlregel

Diese Regel ist **fachlich wichtig**:

Wenn ein Ordner verwendet wird, dann gilt:
- nur die Datei der **aktuellen ISO-Kalenderwoche** ist gültig
- wenn sie nicht existiert: **kein Stundenplan**

### Wichtig
**Kein Fallback auf nächste Woche.**

Beispiel:
- aktuelle Woche = KW 11
- im Ordner liegt nur `KW_12.docx`
- Ergebnis: **kein Stundenplan**

Diese Strenge ist bewusst, damit keine falschen Daten angezeigt werden.

---

## Parsing-Grundlage

Der Stundenplan soll **direkt aus der DOCX-Struktur** gelesen werden, nicht über sichtbares Word.

### Warum
- robuster
- cachebar
- keine sichtbare Word-Instanz nötig
- besser für Hintergrundlogik

### Empfohlene Technik
- DOCX als ZIP öffnen
- WordprocessingML auslesen
- Tabellenstruktur und Paragraphen analysieren

---

## Unterrichtsarten / Gruppen

Diese Gruppen sollen als gültige Unterrichtsarten erkannt werden:

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

### Zusätzliche Normalisierungen
- `IND.` -> `IND`
- `BULV` -> `BU LV`
- `LBABEND` -> `LB ABEND`

### Administrative / nicht anzuzeigende Blöcke
Folgende Gruppen gelten nicht als normaler Block:
- leer
- `ADMIN`
- `LB ABEND`

---

## Lehrer- und Raumerkennung

### Lehrer
Lehrerzeilen sind typischerweise:
- 1 bis 3 Tokens
- jedes Token maximal 2 Buchstaben
- keine Zeitzeile
- keine Raumzeile

Beispiele:
- `EE`
- `MB`
- `VZ`
- `E E` -> soll zu `EE` normalisiert werden

### Raum
Räume sollen auch in lockerer Form erkannt werden.

Beispiele:
- `U4`
- `U 4`
- `B1`

Raumnormalisierung:
- Buchstabe gross
- Zahl direkt dahinter

Also:
- `u 4` -> `U4`

---

## Datenmodell

Empfohlen ist ein kleines Modell in dieser Form:

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

public sealed class ParticipantMiniScheduleEntry
{
    public string Group { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
}

public sealed class ParticipantMiniScheduleCell
{
    public string DayKey { get; set; } = string.Empty;
    public ParticipantMiniScheduleHalfDay HalfDay { get; set; }
    public List<ParticipantMiniScheduleEntry> Entries { get; set; } = new();
    public ParticipantMiniScheduleCellStatus Status { get; set; }
    public bool HasSupplementalDaz { get; set; }
}

public sealed class ParticipantMiniScheduleSummary
{
    public ParticipantMiniScheduleState State { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ParticipantMiniScheduleCell> Cells { get; set; } = new();
}
```

---

## Matching-Grundsatz

### Wichtigster Satz
**Lieber leer als falsch.**

Wenn die Zuordnung nicht sicher ist:
- nicht raten
- nicht fuzzy reparieren
- lieber gar nichts anzeigen

Das ist die zentrale Produktregel.

---

## Alias- und Namenslogik

Der Stundenplan ist handgemacht und verwendet oft:
- Vornamen
- Vorname + Buchstabe
- Kürzungsformen
- teils Uebernamen

Deshalb braucht es Aliaslogik, aber sie muss **konservativ** bleiben.

### Aliasarten, die sinnvoll sind
- eindeutiger führender Vorname
- voller mehrteiliger Name
- `Vorname + Anfangsbuchstabe eines weiteren Namensteils`
- `Vorname + ganzer weiterer Namensteil`

Beispiele:
- `Jan`
- `Jan M`
- `Luna A`
- `Mohammad Ar`
- `Sina`

### Gefährliche Aliasarten
- zu kurze uneindeutige Kürzel
- häufige Vornamen ohne Zusatz
- mehrere ähnliche Personen mit gleichem Einstieg

Beispiele:
- `Mohammad`
- `Mohammad A`
- `Luna` bei 2 verschiedenen `Luna`

---

## Regeln für Ambiguität

### 1. Doppelte Namen brauchen eindeutige Zusatzkennung

Wenn zwei Teilnehmende denselben relevanten Vornamen haben, darf der nackte Vorname nicht mehr genügen.

Beispiel:
- `Luna Aeschlimann`
- `De Oliveira Ferreira Luna`

Dann gilt:
- `Luna` allein = nicht eindeutig
- `Luna A` = erlaubt, wenn eindeutig
- `Luna D` oder `Luna F` = erlaubt, wenn eindeutig

### 2. Häufige Namen wie `Mohammad`
Diese sind die wichtigsten Edge Cases.

Beispiel:
- `Mohammad Arif`
- `Mohammad Iqbal`
- `Mohammad Sharif`
- `Mohammad Mansour`

Dann gilt:
- `Mohammad` = nicht eindeutig
- `Mohammad A` = oft ebenfalls nicht eindeutig
- `Mohammad Ar` = kann eindeutig werden
- `Mohammad Iq` = kann eindeutig werden

Regel:
- nur anzeigen, wenn der Zusatz wirklich eindeutig ist

### 3. Nicht-führende Einzeltokens / Uebernamen
Das darf unterstützt werden, aber nur vorsichtig.

Beispiel:
- `Ursina Sina Rhiana Camenzind`
- im Stundenplan steht `Sina`

Dann kann `Sina` als Alias erlaubt sein, wenn:
- global eindeutig
- im Bestand plausibel
- nicht zu kurz/mehrdeutig

---

## Explizite Namens-Edge-Cases mit gewünschtem Verhalten

Dieser Abschnitt soll der umsetzenden KI die kritischen echten Problemfälle so klar wie möglich vorgeben.

### `Mohammad`

Typische Situation:
- mehrere Teilnehmende mit führendem `Mohammad`
- der Stundenplan enthält oft nur:
  - `Mohammad`
  - `Mohammad A`
  - manchmal eindeutiger: `Mohammad Ar`, `Mohammad Iq`, `Mohammad Ma`, `Mohammad Sh`

Gewünschtes Verhalten:
- `Mohammad` allein:
  - **kein Match**, sobald mehrere `Mohammad` existieren
- `Mohammad A`:
  - **kein Match**, wenn mehr als eine Person passt
- `Mohammad Ar` / `Mohammad Iq` / `Mohammad Ma` / `Mohammad Sh`:
  - Match **nur**, wenn im aktuellen Bestand eindeutig

Merksatz:
- Eindeutigkeit schlägt Länge
- ein Buchstabe reicht nur dann, wenn er wirklich eindeutig ist

### `Luna`

Typische Situation:
- zwei Personen mit `Luna` als relevanter Form
- eine wird z. B. als `Luna A` eindeutig
- die andere vielleicht als `Luna D` oder `Luna F`

Gewünschtes Verhalten:
- `Luna` allein:
  - **kein Match**, wenn mehr als eine `Luna`-Person im Bestand existiert
- `Luna A`:
  - Match, wenn das Zusatzmerkmal nur auf eine Person passt
- `Luna D` / `Luna F`:
  - ebenfalls nur bei echter Eindeutigkeit

Wichtig:
- das nackte `Luna` darf nicht weiter als Einzelalias “herumschweben”, sobald es kollidiert

### `Jan`

Typische Situation:
- eine Person hat `Jan` als eigentlichen Vornamen
- eine andere trägt `Jan` nur als späteren Namensteil

Gewünschtes Verhalten:
- `Jan` soll nicht blind auf beide gehen
- führende Aliasformen sind stärker als nachgelagerte Namensteile
- `Jan M` darf greifen, wenn dadurch genau eine Person eindeutig wird

### `Sina`

Typische Situation:
- offizieller Name enthält `Sina` nur als späteren Namensteil
- im Stundenplan steht aber nur `Sina`

Gewünschtes Verhalten:
- soll matchbar sein, wenn `Sina` global eindeutig ist
- aber nicht als allgemeine Öffnung für beliebige nicht-führende Tokens missverstehen

### `Sara` vs `Sarah`

Gewünschtes Verhalten:
- aktuell konservativ bleiben
- keine breite fuzzy Logik automatisch aktivieren
- Schreibvarianten nur dann bewusst ergänzen, wenn echte Fälle klar belegt sind

### `Bagheri`

Typische Situation:
- Nachname kommt bei mehr als einer Person vor
- der Stundenplan schreibt evtl. nur `Bagheri`

Gewünschtes Verhalten:
- Nachname allein ist zunächst nicht sicher
- Kontext kann helfen, aber nur wenn fachlich sauber
- keine blinde Auflösung nur über den Nachnamen

### Kurze Namen wie `Tom`, `Ali`

Gewünschtes Verhalten:
- auch 3-Zeichen-Namen müssen als echte Aliasform funktionieren können
- aber nur bei globaler Eindeutigkeit
- keine starre Mindestlänge von 4

### `DAZ`

Gewünschtes Verhalten:
- nie als Hauptblock, wenn schon normaler Unterricht da ist
- nur `+DAZ` als Zusatz
- darf Hauptunterricht nicht verdrängen

### `ext` und `disp`

Gewünschtes Verhalten:
- Status verdrängt die normale Darstellung vollständig
- in solchen Zellen:
  - keine Gruppe
  - keine Lehrperson
  - kein Zimmer

---

## Harte Sicherheitsregeln

Diese Regeln sind zentral und sollen **genau so** umgesetzt werden:

### Regel A: Maximal 1 regulärer Treffer pro Halbtag

Wenn ein Teilnehmer in einem Halbtag mehr als **einen nicht-DAZ-Haupttreffer** bekommt:
- gesamter Mini-Stundenplan ungültig
- Ergebnis: `Unavailable`

Warum:
- fachlich ist pro Halbtag genau ein Hauptsetting vorgesehen
- Ausnahme `DAZ` ist nur Zusatz

### Regel B: Maximal 10 reguläre Treffer pro Woche

Wenn ein Teilnehmer mehr als 10 reguläre Treffer in der Woche sammelt:
- ebenfalls ungültig

Warum:
- mehr als 10 Halbtage sind fachlich nicht plausibel

### Regel C: `DAZ` ist nur Zusatz

Wenn `DAZ` zusätzlich im selben Halbtag vorkommt:
- Hauptblock bleibt
- `+DAZ` wird als Zusatz markiert
- `DAZ` darf den Hauptunterricht nicht verdrängen

### Regel D: `ext` und `disp` verdrängen normale Anzeige

Wenn extern:
- nur `ext`
- keine Gruppe
- keine LP
- kein Zimmer

Wenn krank/dispensiert:
- nur `disp`
- keine Gruppe
- keine LP
- kein Zimmer

---

## Gruppenfilter / LB-Regel

Diese Logik ist wichtig:

### `LB`-Gruppen
`LB`-Blöcke dürfen nur `LB`-Teilnehmende ziehen.

### Aber
`LB`-Teilnehmende dürfen trotzdem in normalen Unterrichtsblöcken vorkommen.

Also:
- `LB`-Gruppe -> nur LB-TN
- `BI/BU/MO/PR/...` -> LB-TN sind nicht pauschal ausgeschlossen

Diese Regel ist absichtlich so, weil in der Praxis Lehrbegleitungsleute trotzdem normalen Unterricht besuchen können.

---

## Statusfarben aus dem Stundenplan

Im Parser kann Formatierung aus Word relevant sein.

### Grün markiert
=> `ext`

### Rot markiert / krank / dispensiert
=> `disp`

### Wichtig
Eine grüne Markierung macht einen normalen Hauptblock **nicht automatisch zu DAZ**.
`DAZ` muss als echte Gruppenlogik erkannt werden, nicht nur über Farbe.

---

## Beispiel für Kernlogik

### Wochenplandatei auflösen

```csharp
private static string? ResolveScheduleDocumentPath(string schedulePath)
{
    if (string.IsNullOrWhiteSpace(schedulePath))
        return null;

    if (File.Exists(schedulePath))
        return schedulePath;

    if (!Directory.Exists(schedulePath))
        return null;

    var candidates = Directory.GetFiles(schedulePath, "*.docx", SearchOption.TopDirectoryOnly)
        .Select(ParseWeekCandidate)
        .Where(candidate => candidate is not null)
        .Cast<WeekFileCandidate>()
        .ToList();

    if (candidates.Count == 0)
        return null;

    var today = DateTime.Today;
    var currentWeek = ISOWeek.GetWeekOfYear(today);
    var currentYear = ISOWeek.GetYear(today);

    return candidates
        .Where(candidate => candidate.Week == currentWeek && candidate.Year == currentYear)
        .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
        .Select(candidate => candidate.Path)
        .FirstOrDefault();
}
```

### Harte Begrenzung pro Halbtag / Woche

```csharp
var nonSupplementalMatches = matches
    .Where(match => !IsDazGroup(match.Group))
    .ToList();

var hasMultipleMatchesPerHalfDay = nonSupplementalMatches
    .GroupBy(match => $"{match.DayKey}|{match.HalfDay}", StringComparer.OrdinalIgnoreCase)
    .Any(group => group.Count() > 1);

if (hasMultipleMatchesPerHalfDay)
{
    return Unavailable("Nicht eindeutig zugeordnet");
}

if (nonSupplementalMatches.Count > 10)
{
    return Unavailable("Nicht eindeutig zugeordnet");
}
```

### `DAZ` als Zusatz

```csharp
if (blockSupplemental)
{
    cell.HasSupplementalDaz = true;
    continue;
}
```

### Hauptblock übernehmen

```csharp
cell.Entries.Add(new ParticipantMiniScheduleEntry
{
    Group = block.Group,
    Teacher = block.Teacher,
    Room = block.Room,
    IsExternal = blockExternal
});
```

---

## Diagnose / Debug-Empfehlung

Auch wenn die Funktion in `AkteX` nicht als sichtbares Debug-Feature auftauchen soll, lohnt sich intern eine Diagnosefunktion.

Empfohlen:
- Diagnose-Datei als JSON schreiben
- mit:
  - erkannten Slots
  - Unterrichtsblöcken
  - Teilnehmerzeilen
  - Tokens
  - Kandidaten
  - finalen Matches

### Ablage
- bevorzugt `diagnostics\` neben der App/Exe
- sonst in AppData

### Grund
Bei Matchingfehlern ist diese Diagnose der wichtigste Hebel.

---

## Empfohlene Umsetzung in AkteX

### Schritt 1
Neuen Konfigurationspfad `ScheduleRootPath` einführen

### Schritt 2
Kleines Modell für `ParticipantMiniScheduleSummary` usw. anlegen

### Schritt 3
Einen `WeeklyScheduleService` aufbauen:
- DOCX lesen
- Woche auswählen
- Blöcke parsen
- konservativ matchen

### Schritt 4
Tray-Control für die Teilnehmerliste bauen

### Schritt 5
Bei Doppelklick den Tray öffnen/schließen

### Schritt 6
Mit echten Wochenplan-Dateien und Problemnamen testen

---

## Was ausdrücklich vermieden werden soll

- kein Fallback auf nächste KW
- keine breite fuzzy Namenssuche
- keine aggressive Raterlogik
- keine Fehlermeldung direkt im Tray
- kein grosses Modal für diese Funktion

---

## Wichtige Edge Cases, die die andere KI kennen muss

### 1. `Mohammad`-Cluster
Zu viele ähnliche Namen. Nur eindeutige Zusätze erlauben.

### 2. `Luna`
Wenn zwei `Luna` existieren, reicht `Luna` allein nicht mehr.

### 3. `Jan`
Nicht jeder `Jan` im Namen ist automatisch dieselbe Art Alias.

### 4. `Sina`
Uebername/Nickname kann im Plan real verwendet werden.

### 5. `Sara` vs `Sarah`
Noch nicht aggressiv fuzzy lösen. Erst konservativ bleiben.

### 6. `Bagheri`
Nachname allein kann mehrdeutig sein. Gruppen- oder Kontextfilter können helfen, aber nur wenn fachlich sauber.

### 7. `DAZ`
Wiederkehrende Quelle für Fehlinterpretationen. Immer nur Zusatz.

### 8. `ext` und `disp`
Status verdrängt normale Anzeige vollständig.

### 9. Private Stundenplan-DOCX aktiv mitlesen
Wenn das Referenzdokument zusammen mit dieser Markdown-Datei vorliegt, muss die KI es aktiv lesen und gegen ihre Regeln prüfen. Theorie allein reicht hier nicht.

---

## Minimaler Abschluss-Check für die implementierende KI

Vor Abgabe prüfen:

1. Wird nur die aktuelle KW verwendet?
2. Bleibt die Anzeige leer statt falsch bei Ambiguität?
3. Wird `DAZ` nur als Zusatz angezeigt?
4. Gibt es nie zwei reguläre Haupttreffer im selben Halbtag?
5. Wird >10 pro Woche geblockt?
6. Sind `ext` und `disp` korrekt als reine Statuszellen dargestellt?
7. Ist der Tray rechtsbündig und kompakt?

---

## Zusammenfassung in einem Satz

`AkteX` soll einen kompakten, rechtsbuendigen Mini-Stundenplan-Tray bekommen, der dieselbe konservative DOCX-Parsing- und Matchinglogik nutzt wie `Acta`, mit klaren Regeln fuer Ambiguitaet, `DAZ`, `ext`, `disp`, aktuelle Kalenderwoche und ruhige UI ohne falsche Automatik.

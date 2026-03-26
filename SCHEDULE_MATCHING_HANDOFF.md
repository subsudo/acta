# Stundenplan-Matching: Aktuelle Problematik und offene Herausforderungen

## Kontext

In `XHub` wurde ein neues Feature eingebaut, das aus einem echten Word-Stundenplan im DOCX-Format einen sehr reduzierten Mini-Stundenplan pro Teilnehmendem ableiten soll.

Quelle:
- private lokale Stundenplan-DOCX im Workspace

Ziel im UI:
- Pro TN eine kleine 5x2-Uebersicht im Detailbereich
- Spalten: `Mo`, `Di`, `Mi`, `Do`, `Fr`
- Zeilen: intern `VM` und `NM`, aber ohne Beschriftung
- Pro Feld:
  - Hauptinfo: Unterrichtsart, z. B. `BU`, `BI`, `MO`
  - Zusatz: LP-Kuerzel und Zimmer
- Wenn keine sichere Zuordnung moeglich ist:
  - lieber kein Eintrag oder ein dezenter Hinweis
  - nicht raten

Das Feature ist implementiert, aber die Zuordnung der TN zum Stundenplan ist noch nicht zuverlaessig genug.

## Was bereits funktioniert

### DOCX-Parsing

Der Stundenplan wird direkt aus der DOCX-Struktur gelesen, ohne Word zu oeffnen.

Der Parser erkennt bereits brauchbar:
- Wochentage
- Halbtag-Slots
- viele Unterrichtsbloecke
- Gruppenkuezel
- oft auch LP und Zimmer
- Farbinformationen

### Diagnosemodus

Es gibt bereits einen Diagnose-Export:
- Datei:
  - `%LOCALAPPDATA%\XHub\weekly-schedule-diagnostics.json`
- Backup:
  - `%LOCALAPPDATA%\XHub\weekly-schedule-diagnostics.bak`

Die Diagnose enthaelt:
- erkannte Slots
- erkannte Bloecke
- rohe TN-Zeilen
- Tokenisierung
- Match-Kandidaten
- aufgeloeste bzw. nicht aufgeloeste Zuordnungen

## Aktueller Diagnosebefund

Stand aus `weekly-schedule-diagnostics.json`:

- `Status = Ready`
- `RequestedPath = <private lokale Stundenplan-DOCX>`
- `Slots = 10`
- `Participants = 10`
- `Ready = 0`

Das bedeutet:
- der Stundenplan wird gelesen
- aber aktuell bekommt keiner der Test-TN einen sauberen, vollstaendigen Stundenplan

## Wichtigste Probleme

### 1. Lehrer- und Zimmerzeilen werden teils als TN-Zeilen fehlinterpretiert

Beispiel aus der Diagnose:
- `Group = BI`
- `Teacher = ""`
- `Room = ""`
- stattdessen tauchen in den Teilnehmerzeilen auf:
  - `E E`
  - `U 4`

Das zeigt:
- auseinandergezogene Kuerzel wie `E E` werden nicht robust genug zu `EE` normalisiert
- `U 4` wird nicht robust genug zu `U4` normalisiert

### 2. TN-Namen im Stundenplan sind oft nur Vornamen oder abgekuerzte Varianten

Typische Rohzeilen aus der Diagnose:
- `Mohammad M`
- `Luna A`
- `Cedrik`
- `Leonardo`
- `Milla`
- `Mykhailo`
- `Khaled`

Das Problem:
- die TN-Stammdaten in XHub basieren auf Ordnernamen / vollstaendigen Namen
- der Stundenplan verwendet oft nur Vornamen
- bei Mehrdeutigkeit kommt ein Zusatzbuchstabe dazu
- manchmal sind zwei Personen in einer Zeile zusammengezogen

### 3. Mehrdeutigkeit ist real und darf nicht falsch geloest werden

Mehrere Test-TN landen aktuell auf:
- `Nicht eindeutig zugeordnet`

Das ist fachlich richtig im Sinn von defensivem Verhalten.
Aber die Logik muss besser unterscheiden:
- wann ein Match sicher genug ist
- wann wirklich Ambiguitaet vorliegt

### 4. Segmentierung mehrteiliger Namenszeilen ist noch nicht ausreichend

Beispiele fuer problematische Faelle:
- zwei Namen in einer Zeile
- Namen nur durch Leerzeichen getrennt
- Namen mit Zusatzbuchstaben
- handgeschriebene / manuell gepflegte Schreibweisen

Die vorhandene Logik reicht hier noch nicht aus, um aus einer Rohzeile zuverlaessig mehrere TN zu rekonstruieren.

## Fachliche Rahmenbedingungen

### Unterrichtsarten

Wichtige Faecher / Gruppen:
- `BI`
- `BU`
- `MO`
- `LB`
- `BU LV`
- `WIT`
- `PR`
- `IND`
- `DAZ` / `DAZ-A`

### Sonderregeln

- `IND` kann ohne LP sein
- `DAZ` soll im Mini-Stundenplan aktuell ohne LP dargestellt werden
- `Admin` und interne Hinweise wie `VZ:GL` sollen ignoriert werden

### Farbregeln

Farben werden technisch gelesen. Fachlich gelten derzeit:
- rot markiert:
  - TN ist in Auszeit
  - soll im Mini-Stundenplan nicht angezeigt werden
- gruen markiert:
  - TN ist extern / Schnuppern
  - aktuell wird das konservativ eher ausgeblendet statt besonders beschriftet

## Was bereits im Code vorhanden ist

Relevante Dateien:

- `XHub/Services/WeeklyScheduleService.cs`
- `XHub/Models/WeeklyScheduleDiagnosticsDocument.cs`
- `XHub/Controls/ParticipantDetailPanel.xaml`
- `XHub/Controls/ParticipantDetailPanel.xaml.cs`
- `XHub/MainWindow.xaml.cs`

Der Parser, Cache und Diagnosepfad existieren bereits.
Es geht aktuell vor allem um:
- Robustheit der Erkennung
- bessere Normalisierung
- bessere Zuordnung von TN

## Was geloest werden soll

Die Hauptaufgabe ist nicht mehr das Lesen der DOCX-Datei an sich, sondern die Zuordnung:

### Ziel

Aus dem echten Stundenplan sollen TN robust gefunden werden, auch wenn im Stundenplan nur steht:
- Vorname
- Vorname plus Zusatzbuchstabe
- mehrere TN in einer Zeile
- leicht uneinheitliche Schreibweise

### Gewuenschtes Verhalten

- lieber konservativ bleiben
- bei echter Unsicherheit keine falsche Anzeige
- eindeutige Faelle sicher erkennen
- LP/Zimmer zuverlaessig von TN-Zeilen unterscheiden

## Naheliegende naechste Verbesserungen

Die andere KI soll vor allem auf diese Punkte schauen:

### A. Vorverarbeitung / Normalisierung

- `E E` -> `EE`
- `U 4` -> `U4`
- aehnlich auseinandergezogene Kuerzel robust zusammenziehen

### B. Aliaslogik fuer TN

Fuer jeden TN sollten aus den bekannten Stammdaten sinnvolle Aliasvarianten ableitbar sein:
- voller Name
- erster Vorname
- Vorname + Zusatzbuchstabe
- normalisierte Varianten ohne Umlaute / Sonderzeichen

### C. Segmentierung von Rohzeilen

Wenn eine Zeile mehrere Namen enthaelt, sollte versucht werden:
- gegen die bekannte TN-Liste zu segmentieren
- nicht blind wortweise zu matchen
- bei mehreren plausiblen Segmentierungen lieber `ambiguous` zurueckzugeben

### D. Confidence / Sicherheitslogik

Es waere sinnvoll, Matching nicht binaer zu behandeln, sondern mit Sicherheitsstufen:
- sicher
- wahrscheinlich
- mehrdeutig
- kein Match

## Erwartete Hilfe der anderen KI

Gesucht ist keine neue UI-Idee, sondern Unterstuetzung bei:

1. robusterem Parsing der Unterrichtsbloecke
2. besserer Trennung von `Gruppe / LP / Zimmer / TN`
3. besserem TN-Matching gegen reale, unvollstaendige Stundenplan-Namen
4. einer defensiven Strategie fuer Mehrdeutigkeit

## Wichtig

- Es soll weiterhin **ohne Word COM** gearbeitet werden
- Parsing soll direkt aus DOCX/XML erfolgen
- bei Unsicherheit lieber nichts anzeigen als etwas Falsches
- der bestehende Diagnose-Export soll als Datengrundlage verwendet werden

## Bereits umgesetzte Fixes in XHub

Nach der ersten Diagnose und einer Rueckmeldung von Claude wurden in `WeeklyScheduleService.cs` bereits diese Aenderungen umgesetzt:

1. **Teacher-Erkennung mit Leerzeichen gehaertet**
   - `IsTeacherParagraph()` nutzt nicht mehr die robuste Tokenfilterung, die Ein-Buchstaben-Tokens entfernt.
   - Dadurch werden LP-Zeilen wie `E E` jetzt grundsaetzlich als moegliche Teacher-Zeilen erkannt.
   - `NormalizeTeacher()` zieht solche Zeilen jetzt ebenfalls sauber zu `EE` zusammen.

2. **Teacher/Room-Reihenfolge im Block-Parser gelockert**
   - `ParseBlocks()` ist nicht mehr strikt nur `Teacher` dann `Room`.
   - Es gibt jetzt eine kleine Metadaten-Phase, die `Time`, `Teacher`, `Room` und administrative Zeilen flexibler konsumiert.
   - Ziel: auch Faelle abfangen, in denen `U4` vor `EE` steht.

3. **Cache-Version erhoeht**
   - `CacheVersion` wurde von `1` auf `2` erhoeht.
   - Damit wird sichergestellt, dass alte Stundenplan-Parsergebnisse nicht aus dem bestehenden Cache wiederverwendet werden.

4. **Aliaslogik erweitert**
   - `BuildAliases()` arbeitet jetzt mit einer lockeren Tokenisierung, damit auch einbuchstabige Namenszusatz-Tokens wie `A` oder `K` nicht verloren gehen.
   - Das soll Faelle wie `Luna A` oder `Ali K` besser abdecken.

5. **Resolver weniger strikt gemacht**
   - `Resolve()` arbeitet nicht mehr nur nach dem Muster "volle Zeile eindeutig oder nichts".
   - Es gibt jetzt einen internen `ResolutionPath`, der Teilmatches mit begrenztem Ueberspringen von ignorable Tokens erlauben kann.
   - Ziel: Zeilen mit Rest-Metadaten wie LP-Artefakten nicht sofort komplett zu verwerfen.

## Was noch nicht bestaetigt ist

Diese Aenderungen sind bereits **im Code** und der Build ist gruen, aber die Wirkung auf das echte Matching wurde noch nicht erneut ueber die Diagnose-Datei bewertet.

Das heisst:
- Die aktuelle Diagnose aus `weekly-schedule-diagnostics.json` stammt noch aus dem Stand **vor** diesen Fixes.
- Vor der naechsten Analyse muss XHub neu gestartet werden.
- Danach sollte die Stundenplan-Diagnose erneut geschrieben werden.

## Empfohlener naechster Schritt fuer die andere KI

1. XHub mit dem aktuellen Code starten
2. Diagnose erneut schreiben
3. neue `weekly-schedule-diagnostics.json` auswerten
4. pruefen, ob sich insbesondere diese Punkte verbessert haben:
   - `E E` / `EE` nicht mehr als Teilnehmerzeile
   - `Teacher` in Bloecken mit `Room` vor `Teacher`
   - `Luna A`, `Ali K` und aehnliche Kurzformen
   - Rueckgang von `Nicht eindeutig zugeordnet`

Wenn das Matching weiterhin schwach bleibt, ist der wahrscheinlich naechste Hebel:
- bessere Kontextauflosung bei kollidierenden Aliases
- intelligentere Segmentierung mehrteiliger Zeilen
- klarere Trennung zwischen echten TN-Namen und Rest-Metadaten in derselben Zeile

# Scrollbar Bugfix Handoff

## Problem

In XHub wird der vertikale Scrollbalken im Hauptbereich weiterhin falsch dargestellt.

Statt eines schmalen, integrierten vertikalen Scrollbalkens erscheint rechts nur ein kleiner grauer "Knubbel" bzw. ein kurzer horizontal wirkender Thumb.

Der Benutzer sieht dadurch nicht zuverlässig, dass der Bereich scrollbar ist.

## Zielbild

Gesucht ist eine **ruhige, schlanke, gut sichtbare Dark-/Light-kompatible Custom-Scrollbar**:

- klar als Scrollbalken erkennbar
- vertikal korrekt gerendert
- schmal
- mit leicht gerundetem Thumb
- Track dunkel bzw. dezent grau
- keine Standard-Windows-Pfeilbuttons
- optisch integriert, aber **sichtbar genug**

Der Benutzer hat explizit gesagt:

- der Balken darf nicht verschwinden
- der Balken darf nicht wie ein "Knubbel" aussehen
- es muss klar erkennbar sein, dass weiter unten noch Inhalt vorhanden ist

## Reproduktion

1. App starten
2. Eine längere `Temporäre Liste` mit genügend vielen Teilnehmern anzeigen
3. Im mittleren Listenbereich rechts auf den Scrollbereich achten
4. Aktuelles Fehlbild:
   - statt eines normalen vertikalen Balkens sieht man rechts nur einen kleinen grauen kurzen Thumb
   - der Thumb wirkt eher horizontal als vertikal
   - insgesamt sieht das defekt aus

## Relevante Dateien

- [App.xaml](C:/Users/chris/Desktop/XHub/XHub/App.xaml)
- [MainWindow.xaml](C:/Users/chris/Desktop/XHub/XHub/MainWindow.xaml)
- [ParticipantDetailPanel.xaml](C:/Users/chris/Desktop/XHub/XHub/Controls/ParticipantDetailPanel.xaml)

Der globale Scrollbar-Stil liegt aktuell in:

- [App.xaml](C:/Users/chris/Desktop/XHub/XHub/App.xaml)

Der Haupt-Scrollbereich, bei dem der Fehler sichtbar ist, liegt in:

- [MainWindow.xaml](C:/Users/chris/Desktop/XHub/XHub/MainWindow.xaml)

## Was bereits versucht wurde

Es gab mehrere Iterationen auf dem globalen `ScrollBar`-Style in `App.xaml`.

### Bereits geändert / ausprobiert

1. sehr subtiler Minimalstil
   - schmale Scrollbar
   - fast unsichtbare Spur
   - kaum sichtbarer Thumb
   - Ergebnis: Benutzer sah praktisch keinen Scrollbalken

2. sichtbarer gemacht
   - breiter
   - dunklere Spur
   - sichtbarer Thumb
   - Ergebnis: immer noch falsche Form / "Knubbel"

3. `Track`-Bindings ergänzt
   - `Minimum`
   - `Maximum`
   - `Value`
   - `ViewportSize`
   - Ergebnis: kein Compile-Problem, aber visuell weiterhin falsch

4. `Track.Orientation="{TemplateBinding Orientation}"` ergänzt
   - Verdacht war, dass der Thumb wegen fehlender Orientierung falsch gerendert wird
   - Build war sauber
   - Benutzer meldet: weiterhin praktisch gleiches Fehlbild

5. anschließend kompletter Austausch des Scrollbar-Templates gegen eine dunklere, integrierte Variante
   - eigener Thumb-Style
   - eigener Button-Style
   - eigener ScrollBar-Style
   - Build weiterhin sauber
   - Benutzer meldet trotzdem: Problem weiterhin vorhanden

## Aktueller Stand in App.xaml

Der derzeitige Scrollbar-Bereich in [App.xaml](C:/Users/chris/Desktop/XHub/XHub/App.xaml) wurde mehrfach verändert. Die letzte Richtung war:

- `MinimalScrollBarButtonStyle`
- `MinimalScrollBarThumbStyle`
- globaler `Style TargetType="ScrollBar"`

Bitte **nicht blind weiterflicken**, sondern die tatsächliche Ursache prüfen.

## Mögliche Ursachen

Die wahrscheinlichsten Ursachen sind aus meiner Sicht:

### 1. Das globale ScrollBar-Template ist zwar formal gültig, aber für WPF-ScrollViewer/Track praktisch falsch

Möglicherweise fehlen noch entscheidende Template-Details, die WPF für die korrekte Thumb-Geometrie braucht.

### 2. Der betroffene Scrollbereich benutzt nicht den Stil so, wie angenommen

Es könnte sein, dass:

- ein anderer ScrollBar-Stil greift
- der ScrollViewer selbst ungewöhnliche Größenrestriktionen hat
- der Thumb wegen Layout/Viewport-Größe extrem klein berechnet wird
- der sichtbare Bereich gar nicht der erwartete ScrollViewer ist

### 3. Die Geometrie des Tracks ist falsch

Der "Knubbel" könnte darauf hindeuten, dass:

- die vertikale Scrollbar zwar vorhanden ist,
- aber der Track/Thumb in einer Layout-Konstellation landet, in der Höhe/Breite falsch gegeneinander ausgespielt werden

### 4. Die globale Style-Anwendung ist zu grob

Möglicherweise wäre es robuster:

- nicht global jede ScrollBar zu überschreiben
- sondern gezielt nur für die Haupt-ScrollViewer einen ScrollViewer-/ScrollBar-Stil zu setzen

## Gewünschte Analyse

Bitte prüfen:

1. **Warum** im mittleren Listenbereich rechts nur dieser kurze graue Thumb erscheint
2. ob der aktuelle Custom-Scrollbar-Template technisch korrekt ist
3. ob in `MainWindow.xaml` der betroffene ScrollViewer zusätzliche Layout-Einschränkungen hat
4. ob ein sauberer WPF-`ScrollViewer`-Style mit eigenem `ScrollBar`-Style besser wäre als der aktuelle globale Eingriff
5. ob es sinnvoller ist, den Stil lokal statt global anzuwenden

## Gewünschte Zielumsetzung

Bitte auf einen stabilen, klaren Zustand bringen:

- sichtbarer vertikaler Balken
- schlanker, gerundeter Thumb
- dezenter Track
- keine "Knubbel"-Darstellung
- keine Standard-Windows-Optik
- Layout darf dabei nicht brechen

## Wichtig

- Das Problem ist **nicht** der Build. Der Build ist aktuell sauber.
- Das Problem ist **rein visuell/funktional im laufenden UI**.
- Der Benutzer hat mehrfach bestätigt, dass das aktuelle Ergebnis weiterhin falsch aussieht.

## Letzter Nutzerbefund

Sinngemäß:

> "Es sieht voll scheisse aus, rechts von Jan ist nur so ein grauer Knubbel."

Das ist die Realität des aktuellen Stands. Bitte deshalb wirklich die Ursache finden und nicht nur Farben/Breiten weiter anpassen.

# Decisions

## Aktuelle Leitentscheidungen

### 1. XHub bleibt lokal-first und single-user
Die App speichert Konfiguration, Listen, Logs und Caches lokal. Es gibt keine zentrale Serverlogik als Voraussetzung fuer den Kernworkflow.

**Folge:** robust offline bzw. dateisystemnah, aber bewusst keine Mehrbenutzerplattform.

### 2. Suche arbeitet ueber einen vorbereiteten In-Memory-Index
Keine Live-Dateisystemsuche pro Tastendruck.

**Folge:** schnelle Suche, aber Index-Refresh ist eigener Schritt.

### 3. Die temporaere Liste ist ein stiller Arbeitsbereich
Sie wird nicht wie eine klassische gespeicherte Liste behandelt. Kein staendiger Warnfluss beim Wechsel.

**Folge:** schnellerer Arbeitsfluss; Persistenz nur, wenn der Nutzer bewusst speichert.

### 4. DOCX-Inhalte moeglichst ohne Word lesen
Odoo-Link, Header-Metadaten und Stundenplan werden direkt ueber DOCX/XML verarbeitet, nicht ueber sichtbares Word.

**Folge:** weniger COM-Risiko, besser cachebar, robuster fuer Hintergrundlogik.

### 5. Word bleibt fuer Dokumentöffnen und Bookmark-Spruenge zustaendig
Word COM wird nur dort genutzt, wo das reale Dokumentverhalten gebraucht wird.

**Folge:** Word bleibt kritischer Integrationspunkt, aber nicht Datenquelle fuer alles.

### 6. Word-Fensterposition wird nicht mehr frei gemerkt
Die fruehere Idee “alte Bounds wiederherstellen” wurde verworfen.

Aktueller Stand:
- Option `Word maximiert oeffnen`
- optionaler Zielmonitor
- Fallback auf Hauptbildschirm
- wenn deaktiviert: keine aktive Positionssteuerung

**Folge:** weniger “smarte”, aber vorhersagbarere Word-Oberflaeche.

### 7. Stundenplan-Matching bleibt konservativ
Lieber nichts anzeigen als falsch zuordnen.

Wichtige Fachregeln, die bereits in die Logik eingeflossen sind:
- keine unrealistischen Wochen-/Halbtagstreffer
- doppelte Namen brauchen eindeutige Unterscheidung
- DAZ, `ext`, `disp` sind Sonderfaelle

**Folge:** niedrigere False-Positive-Rate, aber nicht jeder theoretisch moegliche Match wird angezeigt.

### 8. Diagnostics bleiben vorerst im Produkt
Diagnose fuer Stundenplan und Debug-Kontext wurden bewusst noch nicht entfernt, aber die UI-Exponierung wurde reduziert.

Aktueller Stand:
- Diagnose-Schreiblogik bleibt im Code
- kein prominenter Diagnose-Button mehr in der Hauptoberflaeche
- Logs sind direkt ueber die Einstellungen erreichbar

**Folge:** Realtests und AI-gestuetzte Fehlersuche bleiben moeglich, ohne die Hauptoberflaeche unnoetig nach Debug aussehen zu lassen; spaeter weiter aufraeumen.

### 9. Dokumentation soll kuenftig in `docs/` konsolidiert werden
Historische Handover-Dateien bleiben erhalten, aber der aktuelle Projektkontext soll fuer weitere AI-/GitHub-Arbeit primar in `docs/` gepflegt werden.

**Folge:** alte Dateien dienen als Referenz, neue Wahrheit liegt gebuendelt.

### 10. Index-Auto-Refresh bleibt konfigurierbar und optional
`AutoRefreshHours` existiert bewusst als einfache Betriebsoption, nicht als komplexer Hintergrunddienst.

**Folge:** der Index kann bei Bedarf automatisch erneuert werden, bleibt aber eine lokale, kontrollierbare Funktion.

### 10a. Der `Start`-Pfad bleibt flach und kontrollierbar
Beim `Start`-Bestand sollen sowohl direkte TN-Ordner auf erster Ebene als auch genau eine Zwischenebene tiefer unterstuetzt werden. Eine tiefere rekursive Suche ist bewusst nicht gewollt.

Aktueller Stand:
- `Start\TN`
- `Start\Container\TN`
- Erkennung erfolgt konservativ ueber das Vorhandensein einer passenden Verlaufsakte im Ordner
- tiefer als zwei Ebenen wird nicht indexiert

**Folge:** der reale Mischbestand wird abgedeckt, ohne die Startstruktur in eine unkontrollierte Rekursion zu oeffnen.

### 11. Acta startet nur noch als eine Instanz
Mehrfachstarts durch Doppelklicks auf die Exe sollen keine zweite App oeffnen.

Aktueller Stand:
- erste Instanz bleibt aktiv
- weitere Starts beenden sich sofort
- die laufende Instanz wird nach vorne geholt

**Folge:** weniger Verwirrung bei verteilten Tests und im Alltag, besonders wenn Nutzer mehrfach auf das Icon klicken.

### 12. Fensterpositionen werden nur wiederverwendet, wenn sie noch sichtbar sind
Gespeicherte Fensterkoordinaten duerfen bei geaenderten Multi-Monitor-Setups nicht dazu fuehren, dass Acta ausserhalb des sichtbaren Bereichs startet.

Aktueller Stand:
- beim Schliessen werden normale Fenster-Bounds bzw. `RestoreBounds` gespeichert
- beim Start werden gespeicherte Bounds gegen die aktuell vorhandenen Screens geprueft
- wenn sie nicht mehr sinnvoll sichtbar sind, faellt Acta auf den Hauptbildschirm zurueck

**Folge:** robusteres Verhalten zwischen Laptop-only, Docking und wechselnden Monitor-Anordnungen.

### 13. UI-Skalierung wurde nach unten erweitert
Die App soll deutlich kompakter gestellt werden koennen, ohne dass bestehende Nutzer nach dem Update ploetzlich unerwartet kleiner starten.

Aktueller Stand:
- 5 Stufen statt 4
- neuer Default fuer neue Nutzer ist die kompaktere Stufe `2`
- zwei kleinere Stufen wurden nach unten ergaenzt
- bestehende gespeicherte 4-Stufen-Werte werden einmalig auf die neue Skala gemappt

**Folge:** bessere Nutzbarkeit auf kleineren Screens und mehr Reserven nach unten, ohne bestehende Installationen optisch unnoetig zu brechen.

### 14. Word bleibt vorerst synchron, wird aber minimal gehaertet
Die bestehende Word-Integration soll vorerst ohne `WordStaHost` oder groesseren Umbau stabiler werden.

Aktueller Stand:
- `docs.Open()` wird gegen bekannte Lock-COMExceptions gehaertet
- Word-Aktionen aus Hauptfenster und Detailfenster teilen sich einen globalen Busy-Guard
- keine zweite Word-Aktion waehrend eine laeuft
- technische Fehler beim ReadOnly-/Sperrstatus werden konservativ behandelt statt still als Erfolg interpretiert
- vor Word-Aktionen gibt es bewusst nur kleines sofort sichtbares Feedback (`Öffne Dokument...` + Wait-Cursor), aber weiterhin keinen separaten Word-Hintergrundworker

**Folge:** weniger doppelte Word-Aufrufe und sauberere fachliche Fehlermeldungen, ohne den Word-Workflow grundlegend umzubauen.

### 15. Native Windows-Titelleiste ist wieder bevorzugt
Die sichtbare Produktversion soll oben links sichtbar bleiben, aber ohne dafuer eine eigene Titelleiste und zusaetzliche Fensterlogik mitzuschleppen.

Aktueller Stand:
- `MainWindow` nutzt wieder die native Windows-Titelleiste
- der Fenstertitel lautet sichtbar `Acta vX.Y.Z`
- `Refresh` und `Einstellungen` liegen in der normalen oberen Aktionsleiste
- die fruehere Auto-fit- und Sonderlogik fuer kompakte Spalten-/Fensterbreiten bleibt entfernt
- der ruhige graue Arbeits-Header unterhalb der Titelleiste bleibt bestehen

**Folge:** Die App behaelt die sichtbare Versionsnummer und das natuerliche Windows-Fensterverhalten, ohne auf den ruhigeren Header-Look im Arbeitsbereich zu verzichten.

### 16. Auto-Update folgt dem bewährten Scola-Muster
Der GitHub-Updater wird fuer Acta nicht neu entworfen, sondern moeglichst baugleich zum bestehenden Scola-Flow uebernommen.

Aktueller Stand:
- Check gegen `subsudo/acta` via `releases/latest`
- nur stabile Releases, keine Drafts und keine Pre-Releases
- sichtbares Release-Asset fuer Nutzer bleibt exakt `Acta.exe`
- ein eingebetteter externer `ActaUpdater.exe` ersetzt die laufende EXE erst nach App-Ende
- `Später` unterdrueckt dieselbe Version fuer 7 Tage
- Update-Zustaende und Downloads bleiben bewusst unter `%LOCALAPPDATA%\XHub`

**Folge:** gleiches Update-Verhalten wie Scola, ohne neue Updater-Architektur und ohne Migration des bestehenden lokalen XHub-AppData-Bestands.

## Noch offene Entscheidungen

### Stundenplan-Zuverlaessigkeit
Noch nicht final entschieden ist, welche Mindesttrefferquote fuer produktive Aktivierung als ausreichend gilt und welche Namenskonventionen im echten Plan verbindlich werden.

### Odoo-Link-Retry-Verhalten
Fachlich sinnvoll ist, temporäre DOCX-Lesefehler nicht als “kein Link” festzuschreiben. Das ist als Richtung klar, aber noch im echten Bestand weiter abzusichern.

### Umgang mit historischen Root-Dateien
Die Dateien bleiben erhalten. Spaeter muss fuer Git nur noch entschieden werden, welche davon bewusst versioniert bleiben und welche eher Arbeitsmaterial ausserhalb der Kernquellen sind.

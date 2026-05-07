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

### 6. Word verwaltet seine Fenster selbst
Acta platziert Word-Fenster nicht mehr aktiv.

Aktueller Stand:
- keine Option `Word maximiert oeffnen` mehr
- kein Zielmonitor fuer Word mehr
- `EnsureWordUiState(...)` setzt nur Sichtbarkeit und Vordergrund
- Word entscheidet selbst ueber Monitor, Groesse und Position

**Folge:** weniger fragile Multi-Monitor-/DPI-Logik und stabileres reales Word-Verhalten.

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

### 8a. Wochenplan-Cache bleibt konservativ gegen Dateilocks
Ein temporaer gesperrtes `KW_*.docx` darf keinen bereits gueltigen Mini-Stundenplan-Cache mit leerem Inhalt ersetzen.

Aktueller Stand:
- erfolgreiche Reads aktualisieren den Cache wie bisher
- temporaere Lesefehler behalten den letzten gueltigen Cache bei
- fehlgeschlagene Reads machen den Cache nicht neu "frisch"
- nach einer Frist wird erneut versucht, den Wochenplan zu lesen

**Folge:** kurze Locks auf dem Wochenplan fuehren nicht mehr dazu, dass der Mini-Stundenplan fuer den Rest der Woche leer bleibt.

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

### 12. Das Hauptfenster startet kontrolliert frisch auf dem Primary-Monitor
Die gemerkte Fensterposition ist als Hauptstrategie verworfen.

Aktueller Stand:
- `WindowLeft` und `WindowTop` werden nicht mehr aktiv genutzt oder gespeichert
- das Hauptfenster startet bei jedem Start frisch auf dem Primary-Monitor
- Berechnung erfolgt ueber `SystemParameters.WorkArea` in WPF-DIPs
- horizontal zentriert, vertikal leicht oberhalb der Mitte
- `WindowWidth` und `WindowHeight` bleiben nur als geklammerter Groessenwunsch erhalten

**Folge:** robusteres Verhalten bei DPI-Wechseln, Docking und wechselnden Monitor-Anordnungen ohne Off-screen-Schattenlogik.

### 13. UI-Skalierung wurde nach unten erweitert
Die App soll deutlich kompakter gestellt werden koennen, ohne dass bestehende Nutzer nach dem Update ploetzlich unerwartet kleiner starten.

Aktueller Stand:
- 5 Stufen statt 4
- neuer Default fuer neue Nutzer ist die kompaktere Stufe `2`
- zwei kleinere Stufen wurden nach unten ergaenzt
- bestehende gespeicherte 4-Stufen-Werte werden einmalig auf die neue Skala gemappt

**Folge:** bessere Nutzbarkeit auf kleineren Screens und mehr Reserven nach unten, ohne bestehende Installationen optisch unnoetig zu brechen.

### 14. Word nutzt zwei klar getrennte Pfade: nativer Open fuer `Akte`, COM-Bookmark ueber STA-Host fuer `BU`/`BI`/`BE`/`LB`
Die fruehere synchrone UI-nahe COM-Ausfuehrung wurde fuer Bookmark-Aktionen durch einen app-weiten `WordStaHost` ersetzt; das normale Oeffnen ueber `Akte` laeuft bewusst Word-nativ.

Aktueller Stand:
- genau ein `WordStaHost` pro App-Prozess
- genau eine `WordService`-Instanz auf einem dedizierten STA-Thread
- Hauptfenster und separates Detailfenster nutzen denselben Host
- `Akte` oeffnet ueber Shell/Word-Dateizuordnung und ueberlaesst Sperren vollstaendig Word
- `BU`/`BI`/`BE`/`LB` versuchen zuerst COM + Bookmark; bei Sperre oder ReadOnly-Fall wird ohne Bookmark an den nativen Word-Pfad abgegeben
- `DocumentLockedException` bleibt der typstabile interne Signalpfad fuer den Wechsel vom COM-Bookmark-Pfad auf das native Oeffnen
- der fruehere Acta-ReadOnly-Dialog wurde bewusst entfernt
- der globale Busy-Guard bleibt zusaetzlich aktiv; fuer nativen Shell-Open nur als kurzer Cooldown gegen Doppelklicks

**Folge:** weniger Thread-/COM-Stoerfaelle bei Word, wieder nativer Word-Sperrdialog fuer gesperrte Akten und ein klar akzeptierter Edge Case: im Lock-Fall entfällt der Bookmark-Sprung.

### 15. Word-Eintraege sind optionale Schreibaktionen, nicht Bookmark-Spruenge
`Eintrag BU`, `Eintrag BI`, `Eintrag BE` und `Eintrag LB` sind eigene QuickActions. Sie laufen ueber denselben zentralen `WordStaHost`, lesen Clipboard vor dem Dispatch im UI-Thread und schreiben nur in validierte 4-Spalten-Verlaufstabellen.

**Folge:** Die normalen `BU`/`BI`/`BE`/`LB`-Sprungbuttons bleiben unveraendert; Schreibaktionen sind bewusst separat sichtbar schaltbar und fail-closed bei gesperrten Akten.

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

### 17. Teilnehmerhinweise teilen sich Scola-Keys und JSON
Hinweise sind kurze kuratierte Arbeitsmarker, keine lokalen Notizen und kein freies Tag-System.

Aktueller Stand:
- Acta nutzt dieselbe `participant-hints.json` wie Scola
- der Key ist der kanonisierte DOCX-Aktenpfad
- gemappte Laufwerke werden nach UNC aufgeloest; bei Fehlern wird kein lokaler Ersatzkey geschrieben
- `active` ist in Kachel und Detail sichtbar, `done` nur im Editor
- Speichern nutzt Datei-Hash-Konfliktschutz statt blindem Ueberschreiben

**Folge:** Hinweise bleiben zwischen Scola und Acta kompatibel, solange beide Apps die gleiche Kanonisierungs- und JSON-Logik pflegen.

## Noch offene Entscheidungen

### Stundenplan-Zuverlaessigkeit
Noch nicht final entschieden ist, welche Mindesttrefferquote fuer produktive Aktivierung als ausreichend gilt und welche Namenskonventionen im echten Plan verbindlich werden.

### Odoo-Link-Retry-Verhalten
Fachlich sinnvoll ist, temporäre DOCX-Lesefehler nicht als “kein Link” festzuschreiben. Das ist als Richtung klar, aber noch im echten Bestand weiter abzusichern.

### Umgang mit historischen Root-Dateien
Die Dateien bleiben erhalten. Spaeter muss fuer Git nur noch entschieden werden, welche davon bewusst versioniert bleiben und welche eher Arbeitsmaterial ausserhalb der Kernquellen sind.

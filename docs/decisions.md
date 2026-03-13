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
Diagnose fuer Stundenplan und Debug-Kontext wurden bewusst noch nicht entfernt.

**Folge:** Realtests und AI-gestuetzte Fehlersuche bleiben moeglich; spaeter aufraeumen.

### 9. Dokumentation soll kuenftig in `docs/` konsolidiert werden
Historische Handover-Dateien bleiben erhalten, aber der aktuelle Projektkontext soll fuer weitere AI-/GitHub-Arbeit primar in `docs/` gepflegt werden.

**Folge:** alte Dateien dienen als Referenz, neue Wahrheit liegt gebuendelt.

### 10. Index-Auto-Refresh bleibt konfigurierbar und optional
`AutoRefreshHours` existiert bewusst als einfache Betriebsoption, nicht als komplexer Hintergrunddienst.

**Folge:** der Index kann bei Bedarf automatisch erneuert werden, bleibt aber eine lokale, kontrollierbare Funktion.

## Noch offene Entscheidungen

### Stundenplan-Zuverlaessigkeit
Noch nicht final entschieden ist, welche Mindesttrefferquote fuer produktive Aktivierung als ausreichend gilt und welche Namenskonventionen im echten Plan verbindlich werden.

### Odoo-Link-Retry-Verhalten
Fachlich sinnvoll ist, temporäre DOCX-Lesefehler nicht als “kein Link” festzuschreiben. Das ist als Richtung klar, aber noch im echten Bestand weiter abzusichern.

### Umgang mit historischen Root-Dateien
Die Dateien bleiben erhalten. Spaeter muss fuer Git nur noch entschieden werden, welche davon bewusst versioniert bleiben und welche eher Arbeitsmaterial ausserhalb der Kernquellen sind.

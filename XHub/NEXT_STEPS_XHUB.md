# NEXT STEPS: XHub

## Ziel der Datei
Diese Datei priorisiert die naechsten sinnvollen Ausbauarbeiten fuer `XHub`.

Sie ist absichtlich pragmatisch:
- zuerst Stabilitaet und echter Nutzwert
- danach UX/Qualitaet
- erst spaeter groessere Erweiterungen wie Odoo oder Stundenplaene

## Prioritaet 1: Sofort nutzbar machen
### 1. Mock- und Realtest durchfuehren
Ziel:
- pruefen, ob XHub mit dem lokalen MockServer und einem echten TN-Pfad sauber funktioniert

Pruefen:
- Indexaufbau
- Suche mit mehrteiligen Namen
- Listen erstellen / speichern / wieder laden
- Akte / BU / BI / BE oeffnen
- Import einer kleinen Anwesenheitsliste

Warum zuerst:
- der aktuelle Stand baut sauber, aber die echte Nutzbarkeit wurde noch nicht durch eine komplette manuelle Runde bestaetigt

### 2. Multi-Monitor- und Fensterzustand robuster machen
Ziel:
- Verhalten wie in `AkteX`: Fenster soll auch bei Monitorwechsel sichtbar bleiben

Umsetzen:
- Sichtbarkeitspruefung beim Start
- Fallback auf primaeren Monitor, wenn das gespeicherte Fenster ausserhalb aller sichtbaren Arbeitsflaechen liegt

Warum frueh:
- das ist ein echter Alltagsfall
- in `AkteX` war das bereits relevant

### 3. UX-Runde fuer Hauptlayout
Ziel:
- das jetzige V1-Layout auf echte Benutzbarkeit trimmen

Pruefen / verbessern:
- Breitenverteilung linke / mittlere / rechte Spalte
- visuelle Hierarchie zwischen aktueller Liste und Suchtreffern
- Button-Abstaende und Kartenabstaende
- Suchfeld und Listenaktionen
- Detailansicht kompakter oder ruhiger machen

Warum frueh:
- die Architektur steht, jetzt entscheidet die Oberflaeche ueber den Alltagsnutzen

## Prioritaet 2: Kernfunktion vertiefen
### 4. Bildmodul produktiv anbinden
Ziel:
- statt Platzhalter echte Teilnehmerbilder anzeigen koennen

Empfehlung:
- separaten `ImageResolver` einfuehren
- Bildpfad aus Settings nutzen
- eindeutige Dateinamen-Konvention definieren
- bei fehlendem Bild weiterhin Platzhalter

Noch nicht machen:
- keine komplexe Heuristik ueber viele Ordnerquellen

### 5. Listenimport verbessern
Ziel:
- der optionale Import soll robuster werden, ohne zum Hauptworkflow zu werden

Moegliche Verbesserungen:
- mehr Zeilenformate tolerieren
- bessere Rueckmeldung fuer nicht gematchte Namen
- Dubletten klarer behandeln
- Importvorschau vor dem Erstellen der Liste

Wichtig:
- nicht die komplette `AkteX`-Parserkomplexitaet kopieren
- XHub bleibt suchgetrieben

### 6. Kuerzel-Logik absichern
Ziel:
- pruefen, ob die Dateinamensregel fuer Kuerzel in der Praxis wirklich stabil genug ist

Wenn nein:
- Quelle fachlich neu definieren
- keine Heuristik ueber Word-Inhalt bauen, solange keine klare Regel existiert

## Prioritaet 3: Lokale Arbeitsfunktion erweitern
### 7. Notizen / To-dos / Checklisten als optionales V2-Modul
Ziel:
- XHub als persoenliches Beratungswerkzeug staerken

Empfehlung:
- pro Teilnehmer pro Liste lokale Metadaten einfuehren
- getrennt von Word und getrennt von Stamm-/Indexdaten
- zunaechst einfache Notiz + Checkbox-Liste

Wichtig:
- lokal speichern
- in Export/Import integrieren
- nicht in externe Akten schreiben

### 8. Guardrails und Recovery sichtbarer machen
Ziel:
- Benutzern klarer zeigen, wie lokal gespeichert, exportiert und abgesichert wird

Moegliche Schritte:
- Hinweistext im Settings-Fenster
- Button zum Oeffnen des Log-Ordners
- evtl. kleiner Hinweis auf Backup-/Exportfunktion

## Prioritaet 4: Groessere Erweiterungen
### 9. Tagesverantwortungsmodus entwerfen
Ziel:
- eigener Arbeitsmodus fuer Tagesverantwortung

Noch nicht direkt implementieren, sondern zuerst definieren:
- welche Felder werden gebraucht
- welche Liste wird gepflegt
- wie funktioniert Ankunftserfassung
- wie spielt optionaler Listenimport hinein

Empfehlung:
- als eigener UI-Pfad, nicht einfach in die bestehende Hauptansicht hineinquetschen

### 10. Stundenplan-Konzept erst nach Datenklaerung
Ziel:
- spaeter Wochenuebersicht ueber Zuteilungen

Wichtig:
- nicht heuristisch auf bestehende Word-Stundenplaene losgehen
- zuerst klaeren:
  - Dateiformat
  - Ablageschema
  - Namensschema
  - Vorname/Initialen-Problematik

Technische Empfehlung fuer spaeter:
- wenn moeglich nicht ueber sichtbar geoeffnetes Word lesen
- besser strukturierte DOCX-Auswertung oder noch besser eine standardisierte Quelle

### 11. Odoo-Integration als spaetere Sync-Schicht
Ziel:
- TN-Stammdaten spaeter aus Odoo lesen koennen

Empfehlung:
- ueber `OdooSyncService` als klare Integrationsgrenze
- read-only beginnen
- niemals Suche pro Tastendruck gegen Odoo
- stattdessen lokaler Sync / Cache / Rebuild

## Konkrete empfohlene Reihenfolge fuer die naechste Instanz
1. XHub starten und gegen MockServer testen
2. echten Pfad pruefen
3. Fenster-/Monitor-Logik absichern
4. UI/UX-Runde fuer Suche, Listen und Detailansicht
5. Bildmodul produktiv anbinden
6. Listenimport verbessern
7. erst danach ueber Notizen / Tagesmodus / Stundenplaene entscheiden

## Was vorerst nicht priorisiert werden sollte
- Rollenmodell
- zentrale Vorlagen fuer Teams
- Odoo sofort anbinden
- Stundenplaene heuristisch aus alten Word-Dateien lesen
- freie Felddefinitionen im Baukastensystem
- komplexes Kanban-/Board-Modell

## Leitsatz fuer die weitere Entwicklung
`XHub` soll zuerst ein sehr gutes kleines Werkzeug werden:
- schnell
- sicher
- lokal robust
- klar in der Bedienung

Nicht zuerst ein grosses System.

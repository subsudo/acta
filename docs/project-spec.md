# Project Spec

## Kurzbeschreibung
XHub ist ein lokales Einzelplatz-Werkzeug fuer die Arbeit mit Teilnehmenden. Der sichtbare Produktname ist aktuell `Acta`. Die App kombiniert schnelle Suche, eine temporaere Arbeitsliste, gespeicherte Listen, eine kompakte Detailansicht und direkte Word-/Ordnerzugriffe.

## Zielbild
- schnell Teilnehmende finden
- schnell zwischen temporaerer Liste und gespeicherten Listen arbeiten
- relevante Aktionen ohne Umweg ausfuehren
- keine grossen Hintergrundsysteme oder Serverprozesse
- lokale, kontrollierbare Datenhaltung

## Kernfunktionen
- Live-Suche ueber einen vorbereiteten In-Memory-Index
- temporaere Liste als stiller Arbeitsbereich
- gespeicherte Listen pro Benutzer
- Detailbereich mit:
  - Kuerzel
  - Name
  - Foto
  - Odoo-Link
  - Beratungsperson
  - Mini-Stundenplan
- Word-Aktionen:
  - Akte
  - Ordner
  - BU / BI / BE / weitere konfigurierte Verlinkungen

## Hauptworkflows
1. Teilnehmende suchen und zur temporaeren Liste hinzufuegen
2. temporaere Liste bei Bedarf als gespeicherte Liste sichern
3. aus Suche, Liste oder Detailansicht direkt in Ordner / Akte / Word-Bookmarks springen
4. im Detailbereich Zusatzinfos wie Odoo-Link oder Mini-Stundenplan pruefen

## Datenquellen
- TN-Ordner im Dateisystem, fachlich getrennt nach:
  - `LvBasePath` fuer Lehrvorbereitung / Hauptbestand
  - `LbBasePath` fuer Lehrbegleitung
  - `StartBasePath` fuer Start-Eintraege
  - `ExitBasePath` fuer Austritte
- optional zusaetzlich:
  - `ServerBasePath` als Alt-/Rueckwaertskompatibilitaetswert
  - `SecondaryServerBasePath` als zusaetzlicher Suchpfad
- DOCX-Akten in den TN-Ordnern
- optional weiterer Wochenstundenplan als DOCX
- Start-Elternpfad `01_Start` wird gezielt auf `012_Erstgespräch` und `013_start` reduziert; andere Unterordner werden nicht indexiert
- Austrittsarchiv unter `ExitBasePath`, wenn dort `031_im Austritt` plus A-Z-Archivordner vorhanden sind; A-Z wird nur bei aktivierter Archivsuche on demand gelesen

## Konfigurationsrealitaet
- Der produktiv wichtigste Pflichtpfad ist aktuell `LvBasePath`.
- Weitere Pfade beeinflussen Tags, Zusatzbestande und Matchinglogik.
- Der Index aktualisiert sich fix alle vier Stunden plus kleinem Jitter im Hintergrund; der manuelle Refresh bleibt fuer Sofortbedarf.
- Archivsuche speichert nur den lokalen Aktiv-Zustand; der Archivindex selbst bleibt pro Session im Speicher.
- Die App arbeitet nicht sinnvoll, wenn kein TN-Basispfad konfiguriert ist.

## Nicht-Ziele / bewusst nicht priorisiert
- kein grosses Rollen-/Rechtesystem
- keine Cloud-first-Architektur
- keine starke MVVM-Ueberarchitektur als Selbstzweck
- keine automatische Vollintegration in Odoo
- keine aggressive Heuristik, wenn Zuordnungen fachlich unsicher sind

## Produktregeln
- lieber leer lassen als etwas Falsches anzeigen
- lokale Robustheit ist wichtiger als elegante Theorie
- Diagnose- und Debug-Hilfen duerfen voruebergehend im Produkt bleiben, solange sie reale Absicherung ermoeglichen

## Aktuelle Annahmen
- XHub ist Single-User pro Windows-Profil
- die App arbeitet primar auf Windows mit installierter Word-Umgebung
- der Stundenplan bleibt ein Zusatzfeature; Kernworkflow ist weiterhin Suche + Listen + Word-Zugriff

## Offene Punkte
- Stundenplan-Matching ist bereits nuetzlich, aber noch nicht final abgesichert
- Odoo-Link-Erkennung ist vorhanden, aber im echten Bestand noch nicht in allen Faellen stabil genug
- einige historische Docs beschreiben aeltere Produktgrenzen, die heute nicht mehr voll gelten
- Modulkonfiguration im Detailbereich ist vorhanden, aber noch nicht zentral dokumentiert

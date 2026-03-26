# Acta

Acta ist eine lokale WPF/.NET-8-Desktop-App fuer den schnellen Arbeitsalltag mit Teilnehmenden, Akten, persoenlichen Listen und einer kompakten Detailansicht. Der Fokus liegt auf robuster, kontrollierbarer Alltagsfunktion statt auf grosser Systemarchitektur.

Hinweis: Der sichtbare Produktname ist aktuell `Acta`, waehrend interner Projektordner, Namespace und technische Altbezeichnungen weiterhin `XHub` heissen.

## Projektstand
- Hauptprojekt: [XHub](./XHub)
- Aktueller Kontext liegt ab jetzt primaer in [docs](./docs)
- Bestehende Handover-, Debug- und Transfer-Dateien im Repo bleiben erhalten und dienen als historische Referenz

## Schnellstart
```powershell
cd XHub
dotnet build
dotnet run
```

## Ersteinrichtung / Vorbedingungen
- In den Einstellungen muss mindestens ein gueltiger TN-Basispfad gesetzt sein, typischerweise `Lehrvorbereitung (LV)`.
- Fuer Word-Aktionen muss auf dem Rechner eine nutzbare Word-Installation vorhanden sein.
- Fuer das Stundenplan-Feature muss `Stundenplan (DOCX oder Ordner)` gesetzt sein.
- Fuer lokale Tests nur mit eigenen anonymisierten oder privaten lokalen Testdaten arbeiten; solche Daten gehoeren nicht ins oeffentliche Repo.

## Was Acta aktuell tut
- Teilnehmende aus Ordnerstrukturen indexieren
- Live-Suche ueber Namen und Kuerzel
- temporaere und gespeicherte Listen verwalten
- Akten, Ordner und Word-Bookmarks oeffnen
- Odoo-Link und Header-Metadaten aus DOCX lesen
- Mini-Stundenplan pro TN aus einem echten Wochenplan-DOCX ableiten

## Wichtige Ordner
- [XHub](./XHub): eigentlicher App-Code
- [docs](./docs): neue Arbeitsdokumentation
- `publish-single-onefile-*`: gebaute Exe-Artefakte, nicht Quelle

## Lesereihenfolge fuer Weiterarbeit
1. [docs/project-spec.md](./docs/project-spec.md)
2. [docs/architecture.md](./docs/architecture.md)
3. [docs/status.md](./docs/status.md)
4. [docs/decisions.md](./docs/decisions.md)
5. danach erst historische Dateien wie [HANDOVER_UX_OVERHAUL.md](./XHub/HANDOVER_UX_OVERHAUL.md) oder [SCHEDULE_MATCHING_HANDOFF.md](./SCHEDULE_MATCHING_HANDOFF.md)

## Wichtige Hinweise
- Historische Markdown-Dateien im Root und im Projektordner wurden bewusst nicht geloescht.
- Einige davon enthalten aeltere Aussagen oder historische Platzhalterpfade. Fuer den aktuellen Stand gilt primaer die Doku in `docs/`.
- Die Stundenplan-Diagnose ist technisch noch vorhanden, aber aktuell nicht mehr als prominenter Haupt-Button in der Oberflaeche sichtbar.
- Log-Dateien lassen sich direkt ueber die Einstellungen oeffnen, damit verteilte Tests einfacher rueckmeldbar bleiben.

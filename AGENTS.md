# AGENTS.md

## Lesereihenfolge
1. [README.md](./README.md)
2. [docs/project-spec.md](./docs/project-spec.md)
3. [docs/architecture.md](./docs/architecture.md)
4. [docs/status.md](./docs/status.md)
5. [docs/decisions.md](./docs/decisions.md)
6. danach nur bei Bedarf historische Dateien wie:
   - [XHub/HANDOVER_UX_OVERHAUL.md](./XHub/HANDOVER_UX_OVERHAUL.md)
   - [XHub/HANDOVER_XHUB.md](./XHub/HANDOVER_XHUB.md)
   - [SCHEDULE_MATCHING_HANDOFF.md](./SCHEDULE_MATCHING_HANDOFF.md)
   - [WORD_LEAK_FIX_FOR_XHUB.md](./WORD_LEAK_FIX_FOR_XHUB.md)

## Arbeitsprinzip
- Zuerst Kontext lesen, dann handeln.
- Keine großen Umbauten ohne klaren fachlichen Grund.
- Kleine, nachvollziehbare Änderungen bevorzugen.
- Bestehende Muster respektieren, nicht neu designen, wenn es nicht nötig ist.

## Hard Constraints
- Lokale, kontrollierbare Lösungen bevorzugen.
- Robuste Alltagsfunktion wichtiger als Perfektion.
- Lieber leere Anzeige als falsche Automatik.
- Historische Markdown-Dateien nicht löschen; aktueller Kontext liegt aber primär in `docs/`.
- Keine unbegründete Reorganisation der Projektstruktur.

## Current Preferences
- Produkt- und workfloworientiert arbeiten.
- Modular genug, aber ohne Überarchitektur.
- UX zählt; sichtbare Änderungen bewusst und ruhig halten.
- Word-Verhalten soll vorhersehbar sein, nicht “smart”.
- Stundenplan-Matching konservativ halten.
- Konfigurationslogik beachten: `AppConfig` hat mehrere Pfadkategorien, nicht nur einen TN-Basispfad.

## Exploration Allowed
- Reale Handover-, Bugfix-, Diagnose- und Testdateien aktiv auswerten.
- Nur lokale, private oder anonymisierte Testartefakte für Absicherung nutzen; solche Dateien gehören nicht ins öffentliche Repo.
- Debug-/Diagnosehilfen dürfen vorübergehend im Produkt bleiben, wenn sie echte Verifikation ermöglichen.

## Doku mitpflegen
- Bei relevanten Änderungen die Dateien in `docs/` aktualisieren.
- Historische Dateien nur ergänzen, wenn sie bewusst weiter als Transfer-/Handover-Dokument dienen sollen.

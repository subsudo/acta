# Handover: XHub UX-Overhaul — Stand 2026-03-08

## Zusammenfassung
Kompletter UX-Umbau von XHub. Das Layout wurde von einem 2-Spalten-Layout (Hauptbereich + versteckte 46px-Sidebar) zu einem modernen 3-Spalten-Layout umgebaut. Farbschema von Braun auf Teal/Petrol gewechselt. Button-Overload beseitigt. Detail-Ansicht von separatem Fenster zu eingebettetem Panel migriert. Neuer Listen-Flow mit "Neue Liste"-Button und Warnung bei ungespeicherten Aenderungen.

## Was geaendert wurde

### 1. Farbschema (App.xaml + App.xaml.cs)
- Accent-Farbe: `#9D8770` (Braun) → `#14B8A6` (Teal) fuer Dark, `#0D9488` fuer Light
- Hintergruende dunkler/kontrastierter: WindowBg `#2A2D33` → `#1E2028`
- Borders dezenter: `#535A66` → `#3E4254`
- Neue Brush `Brush.AccentSubtle` (`#1A3A38` dark / `#CCFBF1` light) fuer subtile Hervorhebungen
- Primary Buttons: weisse Schrift auf Teal statt weiss auf Braun
- Success/Warning/Error/Info modernisiert (Tailwind-Palette)

### 2. Neues 3-Spalten-Layout (MainWindow.xaml)
```
┌──────────────────────────────────────────────────────┐
│  [☰] [Teilnehmer suchen...              ] [⟳] [⚙]  │
├──────────┬───────────────────────┬───────────────────┤
│ LISTEN   │  ARBEITSBEREICH      │  DETAIL-PANEL     │
│ 220px    │  flex                │  320px            │
│          │                      │                   │
│ + Neue   │  Listenname [Badge]  │  Name + Kuerzel   │
│   Liste  │  Subtitle            │  Quick-Actions    │
│          │                      │  Module           │
│ ──────── │  TN 1          [×]  │                   │
│ Liste 1  │  TN 2          [×]  │                   │
│ Liste 2  │  TN 3          [×]  │                   │
│ Liste 3  │                      │                   │
│          │  [Empty State]       │  [Empty State]    │
│ ──────── │                      │                   │
│ Import   │              [Leeren]│                   │
│          │              [Save]  │                   │
├──────────┴───────────────────────┴───────────────────┤
│  Status                              Index: 142 TN  │
└──────────────────────────────────────────────────────┘
```

**Links (Listen-Panel):**
- Ein-/ausklappbar via ☰-Toggle in der TopBar
- Zustand wird in `UserPrefs.IsListPanelCollapsed` persistiert
- "Arbeitsflaeche" als eigener Button oben (immer sichtbar)
- "+" Button erstellt direkt eine neue leere Liste
- Aktive Liste visuell hervorgehoben (AccentSubtle-Background + Accent-Border rechts)
- Listeneintraege mit Teilnehmer-Count ("3 TN")
- Drei-Punkte-Button ("⋯") pro Liste fuer Kontextmenue: Umbenennen, Loeschen, Module konfigurieren
- "Liste importieren" unten im Panel (vorher war es prominent in der TopBar)
- Empty State wenn keine Listen vorhanden

**Mitte (Arbeitsbereich):**
- Header: Listenname + "Gespeichert"-Badge + Subtitle mit Count
- Buttons "Leeren" / "Speichern" nur sichtbar wenn Items vorhanden
- Teilnehmer-Zeilen: nur Name + Kuerzel + [×]-Button zum Entfernen
- Empty State mit Icon und Hinweistext wenn leer
- Klick auf Teilnehmer zeigt Detail rechts

**Rechts (Detail-Panel):**
- Neues UserControl `Controls\ParticipantDetailPanel`
- Ersetzt das separate `ParticipantDetailWindow` (Window bleibt als Datei bestehen, wird nicht mehr instanziert)
- Quick-Action-Buttons (Ordner, Akte, BU, BI, BE, LB, E BU, E BI) nur hier
- Module (Uebersicht, Bild, Kuerzel) wie bisher
- Empty State wenn nichts ausgewaehlt

### 3. Button-Logik vereinfacht
**Vorher:** Bis zu 9 Buttons pro Zeile (Ordner, Akte, BU, BI, BE, LB, E BU, E BI, Hinzufuegen/×) — sowohl in Suchergebnissen als auch in der Arbeitsliste.

**Nachher:**
- **Suchergebnisse:** Nur `[+ Hinzufuegen]` Button. Klick auf Namen oeffnet Detail.
- **Arbeitsliste:** Nur `[×]` Button. Klick auf Namen oeffnet Detail.
- **Detail-Panel:** Alle Quick-Actions als Button-Reihe.

Der `PreviewMouseLeftButtonDown`/`_suppressNextDetailOpen`-Workaround wurde vollstaendig eliminiert, da Buttons und Selektion nicht mehr kollidieren.

Die `QuickActionVisibilityConverter` wird im MainWindow nicht mehr benoetigt (kein `converters`-Namespace-Import mehr). Sie wird aber weiterhin vom Detail-Panel indirekt genutzt ueber `App.Config.VisibleQuickActions`.

### 4. Listen-Flow ueberarbeitet
**Neuer Flow "Neue Liste erstellen":**
1. "+" Button in der Listen-Sidebar
2. Name eingeben (TextPromptWindow)
3. Leere Liste wird erstellt, gespeichert und sofort aktiviert
4. Teilnehmer ueber Suche hinzufuegen

**Bestehender Flow bleibt:**
- Suche → Hinzufuegen → Arbeitsflaeche fuellen → "Als Liste speichern"

**Warnung bei ungespeicherten Aenderungen:**
- `_hasUnsavedChanges`-Flag trackt ob Aenderungen seit letztem Speichern gemacht wurden
- Bei Listenwechsel (Sidebar-Klick), "Arbeitsflaeche"-Klick, oder Import: Bestaetigungsdialog
- Optionen: Speichern / Verwerfen / Abbrechen
- "Leeren"-Button warnt ebenfalls, wenn ungespeicherte Aenderungen vorhanden

### 5. Suchfeld mit Placeholder
- `SearchPlaceholder` TextBlock ("Teilnehmer suchen...") wird ein-/ausgeblendet je nach Suchfeld-Inhalt
- Placeholder verschwindet sobald Text eingegeben wird
- Kein WPF-Adorner, sondern einfaches TextBlock-Overlay mit `IsHitTestVisible="False"`

### 6. Neue Styles
- `GhostButtonStyle`: Transparenter Hintergrund, kein Border. Fuer ⟳/⚙/⋯ Buttons.
- `SidebarListItemStyle`: Kompaktere Items fuer die Sidebar mit Accent-Border rechts bei Selektion.
- `ParticipantListItemStyle`: Karten-Style fuer Teilnehmer im Arbeitsbereich und Suchergebnissen.
- `PrimaryButtonStyle`: Weisse Schrift auf Teal, eigene Hover/Disabled-Trigger.

## Neue Dateien

### Controls/ParticipantDetailPanel.xaml + .cs
- UserControl mit identischer Funktionalitaet wie das alte `ParticipantDetailWindow`
- `UpdateParticipant(entry, modules)` API bleibt gleich
- `Clear()` Methode zum Zuruecksetzen auf Empty State
- `CurrentParticipant` Property fuer Lesezugriff
- Quick-Action-Buttons werden in `RebuildActions()` dynamisch erstellt
- Module (Uebersicht, Bild, Kuerzel) werden in `RebuildModules()` dynamisch erstellt
- Bild-Modul nutzt `Brush.AccentSubtle` als Hintergrund statt `Brush.PanelBg`
- Modul-Titel nutzen `SecondaryText` + kleinere Schrift (13px) fuer dezentere Hierarchie

## Geaenderte Dateien

| Datei | Aenderung |
|---|---|
| `App.xaml` | Neue Teal-Farbpalette, neue `Brush.AccentSubtle` |
| `App.xaml.cs` | `ApplyTheme()` mit neuen Farbwerten + AccentSubtle |
| `Models/UserPrefs.cs` | `IsListPanelCollapsed` Property hinzugefuegt |
| `MainWindow.xaml` | Komplett neues 3-Spalten-Layout |
| `MainWindow.xaml.cs` | Neue Listen-Logik, Detail inline, Unsaved-Warning |

## Unveraenderte Dateien
- Alle Services (ListRepository, ParticipantIndexService, ParticipantSearchService, etc.)
- Alle Models ausser UserPrefs
- Views/SettingsWindow, Views/TextPromptWindow, Views/ModuleSettingsWindow, Views/AttendanceImportWindow
- Views/ParticipantDetailWindow (bleibt als Datei, wird aber nicht mehr instanziert)
- Converters/QuickActionVisibilityConverter

## Entfernte Funktionalitaet
- Separates `ParticipantDetailWindow` wird nicht mehr geoeffnet (Code bleibt, aber nicht referenziert)
- `_detailWindow`-Field und alle zugehoerigen Methoden entfernt
- `InlineActionButton_OnPreviewMouseLeftButtonDown` / `_suppressNextDetailOpen` entfernt
- `QuickActionButton_OnClick` und inline Quick-Action-Buttons in Listen/Suchergebnissen entfernt
- `ToggleListsSidebarButton_OnClick` und alte Sidebar-Toggle-Logik ersetzt durch neuen `ToggleListPanelButton_OnClick`
- `VisibleQuickActions`-Property bleibt im MainWindow fuer `App.Config`, wird aber nicht mehr per XAML-Binding in Listenzeilen genutzt

## Technischer Status
Build-Ergebnis nach Overhaul:
```
0 Fehler
0 Warnungen
```

---

## Nachfolgende Aenderungen (2026-03-08, Session 2)

### SettingsWindow komplett neu gestaltet (`Views/SettingsWindow.xaml`)

Das SettingsWindow wurde zweimal ueberarbeitet und hat jetzt ein kompaktes, klares Layout:

**Layout-Struktur:**
- **Header-Bar** (oben, `PanelBg`-Hintergrund, 1px-Border unten): Titel "Einstellungen" links + `[Abbrechen]` / `[Speichern]` rechts — kein Footer-Card mehr
- **PFADE**: Volle Breite, Zeilenform — Label (180px fix) | TextBox (*). Kein Card-Wrapper, kein WrapPanel-Chaos.
- **Kachel-Reihe**: `Grid` mit 3 gleichen `*`-Spalten und 12px-Luecke — DARSTELLUNG | INDEX | DATEN — jede als `TileCardStyle`-Border
- **VERLINKUNGEN**: Volle Breite als eigene `TileCardStyle`-Border mit WrapPanel der Chip-Checkboxes

**Entfernte Styles:** `SettingsCardStyle`, `FieldCardStyle`, `SectionTitleStyle`, `BodyHintStyle`, `FieldTitleStyle` — alle nicht mehr benoetigt.

**Neue/beibehaltene Styles:**
- `TileCardStyle` (neu): `PanelBg`, `CornerRadius=10`, `Padding=14,12` — fuer die 4 Kacheln
- `SectionEyebrowStyle`: `10px` (war 11px), SemiBold, Accent-Farbe
- `FieldLabelStyle`: `12px` (war 13px), SecondaryText
- `PrimaryButtonStyle` (neu): Echter `ControlTemplate` mit Hover/Press-States in Accent-Farbe — fuer den Speichern-Button
- `ActionCheckBoxStyle`, `ToggleSwitchStyle`: unveraendert uebernommen

**Fenstergroesse:** `720x680`, `MinWidth=560`, `MinHeight=520` (war 940x740).

**Alle benannten Elemente bleiben identisch** — `SettingsWindow.xaml.cs` wurde inhaltlich nicht veraendert.

---

### Dunkle Titelleiste via Windows DWM API (`App.xaml.cs` + alle Windows)

Alle WPF-Fenster haben jetzt eine dunkle Titelleiste im Dark-Mode.

**Implementierung in `App.xaml.cs`:**
```csharp
[DllImport("dwmapi.dll")]
private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

public static void ApplyDarkTitleBar(Window window, bool isDark)
{
    var hwnd = new WindowInteropHelper(window).Handle;
    if (hwnd == IntPtr.Zero) return;
    int value = isDark ? 1 : 0;
    DwmSetWindowAttribute(hwnd, 20, ref value, Marshal.SizeOf(value)); // Windows 10 20H1+
    DwmSetWindowAttribute(hwnd, 19, ref value, Marshal.SizeOf(value)); // aeltere Builds
}
```

In `ApplyTheme()` wird am Ende ueber alle offenen Fenster iteriert:
```csharp
foreach (Window w in Current.Windows)
    ApplyDarkTitleBar(w, isDark);
```

**`OnSourceInitialized` in allen Windows (fuer den Start-Zeitpunkt, bevor HWND vorhanden):**
- `MainWindow.xaml.cs`
- `Views/SettingsWindow.xaml.cs`
- `Views/TextPromptWindow.xaml.cs`
- `Views/AttendanceImportWindow.xaml.cs`
- `Views/ModuleSettingsWindow.xaml.cs`

Jedes ruft `App.ApplyDarkTitleBar(this, App.UserPrefs.IsDarkTheme)` auf.

**Zwei Faelle abgedeckt:**
1. App-Start / Fenster oeffnet → `OnSourceInitialized` setzt Titelleiste beim ersten Rendern
2. Theme-Wechsel in Einstellungen → `ApplyTheme()` aktualisiert alle offenen Fenster

## Bekannte Einschraenkungen / offene Punkte
1. **Kein Drag & Drop** fuer Listenreihenfolge in der Sidebar oder Teilnehmer-Reihenfolge im Arbeitsbereich
2. **Keine Keyboard-Shortcuts** (Ctrl+N, Ctrl+S etc.) — waere sinnvoller naechster Schritt
3. **Kein animierter Sidebar-Toggle** — Panel wird hart ein-/ausgeblendet
4. **ParticipantDetailWindow.xaml bleibt als Datei** — kann spaeter entfernt werden, wenn sicher ist dass es nicht mehr gebraucht wird
5. **StatusBar-Meldungen** koennten spaeter durch Toasts ersetzt werden
6. **QuickActionVisibilityConverter** wird im MainWindow nicht mehr direkt referenziert — der Namespace-Import wurde entfernt. Kann spaeter aufgeraeumt werden, falls auch das Detail-Panel ihn nicht braucht.

## Einstiegspunkte fuer die naechste KI
Lesereihenfolge:
1. `HANDOVER_XHUB.md` (Gesamtkontext)
2. Dieses Dokument (UX-Overhaul)
3. `MainWindow.xaml` (neues Layout verstehen)
4. `MainWindow.xaml.cs` (neue Logik, bes. Listen-Flow und Unsaved-Warning)
5. `Controls/ParticipantDetailPanel.xaml.cs` (Detail-UserControl)
6. `App.xaml.cs` (`ApplyTheme()` fuer Farbschema)

---

## Nachfolgende Aenderungen (2026-03-08, Session 3)

### Detailbereich rechts jetzt ein-/ausklappbar

Der rechte Detailbereich wurde von einer permanent sichtbaren Spalte zu einem bewusst steuerbaren Sidepanel weiterentwickelt.

**Neues Verhalten:**
- App startet standardmaessig mit eingeklappter Detailansicht (UserPrefs.IsDetailPanelCollapsed = true als Default)
- Oben rechts im Hauptfenster gibt es einen klaren Details-Button zum Ein-/Ausblenden
- Im Detailbereich selbst gibt es zusaetzlich einen kleinen Schliessen-Button (×) im Header
- Wenn der Detailbereich offen ist, aktualisiert ein Klick auf einen Teilnehmer weiterhin direkt die Inhalte rechts
- Wenn der Detailbereich geschlossen ist, merkt sich die App die Auswahl intern, oeffnet den Bereich aber nicht automatisch
- Beim Oeffnen vergroessert sich das Hauptfenster bei Bedarf automatisch auf eine sinnvolle Mindestbreite fuer die Detailansicht
- Ohne geoeffneten Detailbereich darf das Hauptfenster schmaeler sein

**Neue MainWindow-Elemente:**
- ToggleDetailPanelButton
- DetailPanelSplitterColumn
- DetailPanelColumn
- DetailPanelSplitter
- DetailPanelBorder
- CloseDetailPanelButton

**Neue MainWindow-Logik:**
- _isDetailPanelOpen als Laufzeitstatus
- UpdateDetailPanelState() fuer Breite, Sichtbarkeit und Buttonzustand
- ShowParticipantDetails(entry) aktualisiert die Inhalte nur dann, wenn der Bereich bereits sichtbar ist
- RefreshDetailPanel() arbeitet nur noch, wenn der Bereich sichtbar ist

**Neue UserPrefs-Property:**
- UserPrefs.IsDetailPanelCollapsed

Damit ist die Detailansicht jetzt bewusst manuell steuerbar, ohne die Hauptliste dauernd optisch aufzublasen.


## Nachfolgende Aenderungen (2026-03-08, Session 4)

### Status-Tags fuer Teilnehmer

Die Teilnehmerkacheln in Suche und Arbeitsliste koennen jetzt einen kompakten Status-Badge vor dem Namen anzeigen.

**Neues Verhalten:**
- Genau ein Badge pro Teilnehmer
- Verwendete Kurzformen: `LV`, `LB`, `ST`, `AU`
- Anzeige nur, wenn in den Einstellungen `Tags anzeigen` aktiviert ist
- Stil bewusst ruhig: kleine hellgraue Box, dezente Kontur, feste Mindestbreite fuer saubere Ausrichtung

**Pfadlogik:**
- Der fruehere Pfad `Teilnehmende` wird nun als Lehrvorbereitung (`LV`) behandelt
- `LvBasePath` ist der Basisordner fuer den Teilnehmerindex
- `LbBasePath`, `StartBasePath` und `ExitBasePath` erzeugen die Badges `LB`, `ST` und `AU`
- Zur Rueckwaertskompatibilitaet wird ein vorhandener alter `ServerBasePath` beim Laden nach `LvBasePath` uebernommen

**Betroffene Stellen:**
- `ParticipantIndexEntry.StatusTag`
- `ParticipantIndexService` baut den Status direkt beim Indexieren
- `MainWindow.xaml` zeigt den Badge in Suche und Arbeitsliste
- `SettingsWindow` hat unter `Darstellung` den Toggle `Tags anzeigen`

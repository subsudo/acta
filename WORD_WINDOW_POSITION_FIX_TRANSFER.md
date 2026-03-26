# Word Window Position Fix Transfer

Stand: 2026-03-11

Dieses Dokument beschreibt den Bedarf, die Analyse, die umgesetzte Loesung und die offenen Grenzen fuer die Word-Fensterpositions-Logik in `AkteX`.

Es soll einer anderen KI oder Person ermoeglichen, den Fix schnell zu verstehen, nachzuvollziehen und bei Bedarf weiterzuentwickeln.

## Problem / Bedarf

Der Nutzer beobachtet seit laengerem, dass Word-Dokumente, die aus `AkteX` geoeffnet oder angesprungen werden, nicht konsistent an derselben Stelle erscheinen.

Beobachtetes Verhalten:
- Word-Fenster oeffnen sich scheinbar "irgendwo"
- das Verhalten wirkt aus Nutzersicht zufaellig
- nach manuellem Verschieben oder Aendern der Groesse bleibt diese Position nicht verlaesslich erhalten
- im Multi-Monitor-Alltag fuehrt das zu stoerendem Nachjustieren

Gewuenschtes Verhalten:
- Wenn ein Word-Fenster ueber `AkteX` geoefnet oder fokussiert wird, soll es moeglichst dort erscheinen, wo der Nutzer das letzte relevante Word-Fenster hatte.
- Wenn Word zuletzt maximiert war, soll dieser Zustand ebenfalls wiederhergestellt werden.
- Falls die gespeicherten Bounds auf dem aktuellen Desktop nicht mehr sinnvoll sichtbar sind (z. B. externer Monitor nicht mehr vorhanden), darf die App diese alte Position nicht blind wiederverwenden.

## Analyse

Wichtige Einschaetzung:
- Die Position eines Word-Fensters wird nicht allein von `AkteX` bestimmt.
- Es ist ein Zusammenspiel aus:
  - Word selbst
  - der aktuell laufenden Word-Instanz
  - Windows-Fensterverwaltung
  - und eventuellen Nachkorrekturen durch `AkteX`

Daraus folgt:
- Eine 100% deterministische Kontrolle ueber jede Oeffnung ist nicht realistisch.
- Eine robuste `best effort`-Loesung ist aber moeglich.

Der praktikable Ansatz ist:
1. letzte sinnvolle Word-Bounds speichern
2. diese bei spaeteren Word-Aktionen wieder anwenden
3. nur dann anwenden, wenn sie auf den aktuell verfuegbaren Monitoren noch sinnvoll sichtbar sind

## Umgesetzte Aenderungen

### 1. UserPrefs erweitert

Datei:
- `VerlaufsakteApp/Models/UserPrefs.cs`

Hinzugefuegte Felder:
- `WordWindowLeft`
- `WordWindowTop`
- `WordWindowWidth`
- `WordWindowHeight`
- `WordWindowWasMaximized`

Bedeutung:
- Speichern die zuletzt bekannte Word-Fensterposition bzw. den maximierten Zustand.

## 2. Wiederanwendung der Bounds beim Oeffnen/Fokussieren

Datei:
- `VerlaufsakteApp/Services/WordService.cs`

In `EnsureWordUiState(dynamic app)` wurde die Reihenfolge auf folgenden Ablauf gebracht:
1. `app.Visible = true`
2. nach Moeglichkeit `app.UserControl = true`
3. `TryApplyPreferredWindowBounds(app)`
4. `TryBringWordToForeground(app)`

Ziel:
- Sobald Word aus `AkteX` heraus sichtbar gemacht wird, werden bekannte Bounds moeglichst sofort wieder angewendet.

### 3. Neue Hilfsmethoden in WordService

Neu eingefuehrt bzw. verdrahtet:
- `TryApplyPreferredWindowBounds(dynamic app)`
- `TryCaptureWindowBounds(dynamic app)`
- `TryGetWordTargetWindow(dynamic app)`
- `TrySetWordWindowState(dynamic app, int state)`
- `IsWindowRectSufficientlyVisible(Rect windowRect)`

#### `TryApplyPreferredWindowBounds(...)`
Logik:
- liest die gespeicherten Word-Bounds aus `App.UserPrefs`
- wenn `WordWindowWasMaximized == true`, wird Word wieder maximiert
- sonst werden `Left/Top/Width/Height` angewendet
- aber nur, wenn das Zielrechteck auf dem aktuellen virtuellen Desktop noch ausreichend sichtbar ist

#### `TryCaptureWindowBounds(...)`
Logik:
- liest die aktuellen Word-Bounds vom Ziel-Fenster aus
- erkennt, ob Word maximiert ist
- speichert entweder:
  - maximierten Zustand
  - oder normale Bounds
- speichert nur dann normale Bounds, wenn das Fenster aktuell sinnvoll sichtbar ist

#### `IsWindowRectSufficientlyVisible(...)`
Logik:
- nimmt den aktuellen virtuellen Desktop (`SystemParameters.VirtualScreen*`)
- berechnet die sichtbare Schnittmenge mit dem Fensterrechteck
- betrachtet die Bounds als brauchbar, wenn mindestens 20% sichtbar sind

Ziel:
- alte, nun off-screen liegende Fensterpositionen nicht wiederherstellen

### 4. Capture an den relevanten Fokus-Stellen eingebaut

Die Bounds werden nicht permanent live verfolgt, sondern an den Stellen gespeichert, an denen `AkteX` erfolgreich mit Word arbeitet.

Derzeit ist `TryCaptureWindowBounds(app)` eingebaut in:
- `FocusDocument(...)`
- `FocusRangeAtTop(...)` im Erfolgsfall
- `FocusRangeAtTop(...)` auch im Fallback-Pfad

Praktische Bedeutung:
- Oeffnet der Nutzer eine Akte ueber `AkteX`, springt zu BU/BI/BE oder fuehrt eine vergleichbare Word-Aktion aus, dann speichert `AkteX` danach die aktuelle Word-Position.

## Was dieser Fix bewusst nicht tut

Die App verfolgt Word-Fensterbewegungen nicht live.

Das heisst:
- Wenn der Nutzer Word manuell verschiebt
- und danach keine weitere `AkteX`-Word-Aktion mehr ausloest,
- wird diese neue Position noch nicht gespeichert.

Gespeichert wird also nicht bei jedem Drag oder Resize in Word selbst, sondern:
- immer dann, wenn `AkteX` Word erneut fokussiert oder dorthin navigiert.

Das ist eine bewusste Vereinfachung.

## Warum dieser Ansatz gewaehlt wurde

Vorteile:
- klein genug fuer die bestehende Architektur
- greift nicht invasiv in Word ein
- robust genug fuer den praktischen Alltag
- reduziert Off-Screen-Probleme bei Monitorwechseln
- unterscheidet korrekt zwischen normalen Bounds und maximiertem Zustand

Nicht umgesetzt wurde bewusst:
- permanentes Polling des Word-Fensters
- ein eigener nativer Hook auf Word-Window-Moves
- Echtzeit-Synchronisation bei jedem manuellen Verschieben

Diese Varianten waeren aufwendiger und stoeranfaelliger.

## Wichtige Grenzen / offene Punkte

### 1. Best-effort, nicht absolute Kontrolle
Word und Windows behalten einen Teil der Kontrolle ueber das finale Fensterverhalten.

Gerade bei:
- mehreren offenen Word-Fenstern
- mehreren Word-Instanzen
- maximierten Fenstern
- Foreground-/Focus-Eigenheiten von Windows

kann das Ergebnis noch leicht variieren.

### 2. Speicherung nur bei AkteX-Aktionen
Wenn ein Nutzer Word manuell verschiebt und danach keine weitere `AkteX`-Aktion startet, bleibt die neue Position ungespeichert.

### 3. ActiveWindow vs. Application-Fenster
Die Logik arbeitet bevorzugt ueber `app.ActiveWindow`, faellt sonst auf `app` zurueck.

Das ist pragmatisch, aber in exotischen Word-Situationen nicht vollstaendig perfekt.

## Empfehlung fuer weitere KI / Weiterentwicklung

Wenn das Verhalten weiter verbessert werden soll, sind die naechsten sinnvollen Ausbaustufen:

1. Nur speichern, wenn sich die Bounds tatsaechlich geaendert haben
- vermeidet unnoetige `SaveUserPrefs()`-Schreibvorgaenge

2. Optional expliziter nachfassen beim normalen `Akte oeffnen`
- falls dort subjektiv noch mehr Streuung beobachtet wird als bei BU/BI/BE

3. Optional letzte erfolgreiche Word-Bounds getrennt nach maximiert/nicht maximiert feinjustieren
- aktuell reicht ein gemeinsames Modell

4. Nur wenn wirklich noetig: spaetere Untersuchung, ob ein Event-/Hook-basierter Ansatz ueberhaupt Mehrwert bringt
- aktuell nicht empfohlen

## Relevante Dateien

- `<repo-root>/VerlaufsakteApp/Models/UserPrefs.cs`
- `<repo-root>/VerlaufsakteApp/Services/WordService.cs`

## Build-Status

Nach dem Einbau wurde erfolgreich gebaut:

```powershell
$env:DOTNET_CLI_HOME='<repo-root>\\.dotnet-cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build .\VerlaufsakteApp\VerlaufsakteApp.csproj
```

Ergebnis:
- `0 Fehler`
- `0 Warnungen`

## Kurzfassung fuer andere KI

Der Fix fuehrt eine persistente `best effort`-Fensterpositionslogik fuer Word ein:
- letzte Word-Bounds in `UserPrefs` speichern
- beim naechsten Word-Zugriff wieder anwenden
- maximierten Zustand ebenfalls merken
- Off-Screen-Bounds ueber Sichtbarkeitspruefung verwerfen
- Speicherung nur an echten `AkteX`-Word-Aktionspunkten, nicht live bei jedem Word-Drag

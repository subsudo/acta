# Word-Leak-Schutz fuer XHub

## Ziel
XHub verwendet wie AkteX Word-COM-Automation. Dabei darf die App nach einer Aktion keine unerwarteten, ungespeicherten Word-Dokumente wie `Dokument 6` offen stehen lassen.

Diese Anleitung beschreibt die Schutzlogik aus AkteX so, dass eine andere KI sie in XHub nachbauen und an die dortige Struktur anpassen kann.

## Das Problem
Beim Arbeiten mit Word ueber COM gibt es zwei typische Fehlerbilder:

1. Die App startet oder verwendet eine Word-Instanz, oeffnet das Zieldokument, und Word laesst daneben ein leeres, ungespeichertes Dokument offen.
2. Eine Aktion scheitert mitten im Ablauf, und dadurch bleibt entweder das gerade geoeffnete Zieldokument oder ein transientes Blankodokument offen.

Das fuehrt zu Verwirrung beim Benutzer und erhoeht das Risiko fuer weitere Word-/Lock-Probleme.

## Grundidee des Fixes
Die Schutzlogik besteht aus vier Teilen:

1. Word-Instanz bewusst erzeugen oder bewusst anhaengen.
2. Vor der Aktion die Anzahl bereits vorhandener ungespeicherter Dokumente merken.
3. Nach dem Oeffnen bzw. nach der Aktion alle neu entstandenen ungespeicherten, nicht-zielbezogenen Dokumente wieder schliessen.
4. Bei Fehlern das von der App geoeffnete Zieldokument und ggf. die selbst gestartete Word-Instanz gezielt aufraeumen.

Wichtig: Nicht einfach pauschal alle ungespeicherten Dokumente schliessen. Es duerfen nur die Dokumente geschlossen werden, die **durch diese Aktion neu entstanden** sind.

## Zielverhalten
Fuer jede Word-Aktion in XHub soll gelten:

- Wenn Word schon laeuft, wird wenn moeglich an die vorhandene Instanz angehaengt.
- Wenn das Zieldokument bereits offen ist, wird es wiederverwendet.
- Wenn die App ein Dokument selbst oeffnet und die Aktion spaeter fehlschlaegt, wird genau dieses Dokument wieder geschlossen.
- Wenn Word durch die Aktion ein oder mehrere leere, ungespeicherte Dokumente erzeugt, werden nur diese neu entstandenen Dokumente wieder geschlossen.
- Wenn die App selbst eine Word-Instanz nur fuer diese Aktion gestartet hat und die Aktion fehlschlaegt, darf diese Instanz wieder beendet werden.

## Empfohlene Struktur
Die XHub-Implementierung sollte eine kleine Huelle fuer die Word-Instanz verwenden, z.B.:

```csharp
private readonly record struct WordApplicationHandle(dynamic App, bool WasCreatedHere, int InitialUnsavedDocumentCount);
```

Diese Huelle transportiert drei Informationen:
- `App`: das COM-Objekt der Word-Instanz
- `WasCreatedHere`: wurde Word von der App neu gestartet?
- `InitialUnsavedDocumentCount`: wie viele ungespeicherte Dokumente waren **vor der Aktion** schon offen?

## Schritt 1: Word-Instanz erzeugen oder anhaengen
Die Methode `CreateOrAttachWordApplication()` sollte:

1. Zuerst versuchen, sich an eine laufende Word-Instanz zu haengen.
2. Wenn das nicht geht, eine neue Instanz starten.
3. Danach die aktuelle Anzahl ungespeicherter Dokumente erfassen.
4. Ein `WordApplicationHandle` zurueckgeben.

Wichtig:
- Wenn eine **neue** Instanz gestartet wird, soll der Baseline-Wert fuer `InitialUnsavedDocumentCount` in XHub genauso defensiv behandelt werden wie in AkteX.
- Die Idee ist: ein beim Start von Word automatisch entstehendes Blankodokument darf spaeter **nicht** als legitimes Alt-Dokument geschont werden.
- In AkteX wurde dieser Punkt spaeter nachgeschaerft, weil sonst genau der Leak-Schutz ausgehoben wurde.

Praxisregel fuer die andere KI:
- Beim Anhaengen an eine bereits laufende Instanz ist die Baseline wichtig.
- Beim Erzeugen einer neuen Instanz darf ein Word-Start-Blankodokument **nicht** unangetastet bleiben, wenn es nicht das Zieldokument ist.

## Schritt 2: Zieldokument wiederverwenden oder oeffnen
Eine Methode nach dem Muster `OpenOrGetDocument(app, docPath, out openedHere)` sollte:

1. Alle aktuell offenen Dokumente der Word-Instanz durchsuchen.
2. Wenn das Ziel bereits offen ist, dieses Dokument zurueckgeben und `openedHere = false` setzen.
3. Wenn es nicht offen ist:
   - vor dem Oeffnen Dateisperre pruefen
   - dann ueber COM oeffnen
   - `openedHere = true`

Wichtig:
- Der Vergleich muss ueber den vollqualifizierten Dokumentpfad laufen.
- Das verhindert doppelte Oeffnungen desselben Dokuments.

## Schritt 3: Neue transiente Blankodokumente schliessen
Diese Logik ist der Kern des Fixes.

Eine Methode nach dem Muster `CloseTransientEmptyDocuments(app, targetDocPath, initialUnsavedDocumentCount)` soll:

1. Alle offenen Dokumente durchgehen.
2. Das eigentliche Zieldokument anhand des Pfads immer ausnehmen.
3. Alle ungespeicherten Dokumente sammeln.
4. Ausrechnen, wie viele davon **neu** entstanden sind:
   - `documentsToClose = unsavedNow - initialUnsavedDocumentCount`
   - aber nie kleiner als `0`
5. Nur genau diese neu entstandenen ungespeicherten Dokumente mit `Close(false)` schliessen.

Wichtig:
- "ungespeichert" bedeutet hier technisch: `doc.Path` ist leer.
- Nicht ueber Dokumentnamen wie `Dokument 1`, `Dokument 6` gehen.
- Nicht versuchen, "leer" ueber Inhalt zu erkennen.
- Die robuste Heuristik ist: kein Dateipfad + nicht das Zieldokument.

## Schritt 4: Fehlerfall sauber aufraeumen
Jede Word-Aktion sollte ein `try/finally` haben mit mindestens diesen Schutzregeln:

### Fall A: Aktion scheitert und das Dokument wurde von der App selbst geoeffnet
Dann:
- `TryCloseDocument(doc)` aufrufen
- aber nur, wenn dieses Dokument nicht schon vorher offen war

### Fall B: Aktion scheitert und die Word-Instanz wurde fuer diese Aktion neu gestartet
Dann:
- `TryQuitWordApplication(app)` aufrufen
- damit keine einsame Hilfsinstanz offen bleibt

### Fall C: Aktion laeuft erfolgreich
Dann:
- die selbst gestartete Instanz **nicht** einfach beenden, wenn der Benutzer das Dokument weitersehen oder bearbeiten soll
- aber die transienten Blankodokumente muessen trotzdem entfernt sein

## Empfohlenes Ablaufmuster pro Word-Aktion
Das folgende Muster sollte in XHub fuer alle relevanten Word-Aktionen gelten:

```csharp
AppLogger.Info("Word.Action start ...");

if (!IsWordAvailable)
{
    throw new InvalidOperationException("Microsoft Word wurde nicht gefunden");
}

if (!File.Exists(docPath))
{
    throw new FileNotFoundException("Dokumentdatei nicht gefunden", docPath);
}

dynamic? app = null;
dynamic? doc = null;
var shouldQuitCreatedApp = false;
var openedHere = false;
var operationSucceeded = false;

try
{
    var wordApp = CreateOrAttachWordApplication();
    app = wordApp.App;
    shouldQuitCreatedApp = wordApp.WasCreatedHere;

    doc = OpenOrGetDocument(app, docPath, out openedHere);
    CloseTransientEmptyDocuments(app, docPath, wordApp.InitialUnsavedDocumentCount);

    EnsureWordUiState(app);
    EnsureDocumentNotLocked(doc, openedHere);

    // hier XHub-spezifische Aktion ausfuehren

    operationSucceeded = true;
    shouldQuitCreatedApp = false;
}
finally
{
    if (!operationSucceeded && openedHere && !shouldQuitCreatedApp)
    {
        TryCloseDocument(doc);
    }

    if (doc is not null && Marshal.IsComObject(doc))
    {
        Marshal.ReleaseComObject(doc);
    }

    if (shouldQuitCreatedApp)
    {
        TryQuitWordApplication(app);
    }

    if (app is not null && Marshal.IsComObject(app))
    {
        Marshal.ReleaseComObject(app);
    }
}
```

## Was XHub aus AkteX uebernehmen sollte
Die andere KI soll diese Bausteine sinngemaess uebernehmen oder nachbauen:

- `CreateOrAttachWordApplication()`
- `OpenOrGetDocument(...)`
- `CountUnsavedDocuments(...)`
- `IsUnsavedDocument(...)`
- `CloseTransientEmptyDocuments(...)`
- `TryCloseDocument(...)`
- `TryQuitWordApplication(...)`
- `EnsureDocumentNotLocked(...)`
- `EnsureWordUiState(...)`

Die Methodennamen muessen nicht identisch sein. Wichtig ist das Verhalten.

## Wo der Fix besonders wichtig ist
In XHub sollte diese Schutzlogik nicht nur fuer "Akte oeffnen" gelten, sondern fuer alle Word-Aktionen, insbesondere:

- Dokument oeffnen
- zu Bookmark springen
- BU / BI / BE anspringen
- Inhalte einfuegen
- spaetere Serien-/Batch-Aktionen

## Logging-Empfehlung fuer XHub
Die andere KI soll denselben Bereich gut loggen, damit spaetere Probleme nachvollziehbar bleiben.

Mindestens loggen:
- wurde an bestehende Word-Instanz angehaengt oder neue Instanz gestartet?
- wie viele Dokumente waren offen?
- wie viele ungespeicherte Dokumente gab es vor der Aktion?
- wie viele ungespeicherte Dokumente gab es nach der Aktion?
- wie viele davon wurden geschlossen?
- wurde das Zieldokument wiederverwendet oder neu geoeffnet?
- wurde im Fehlerfall das Dokument geschlossen?
- wurde im Fehlerfall die Word-Instanz beendet?

## Wichtige Fallstricke
Die andere KI soll diese Fehler **nicht** machen:

1. **Nicht** alle ungespeicherten Dokumente blind schliessen.
   - Sonst koennen legitime Dokumente eines bereits laufenden Word betroffen sein.

2. **Nicht** nur nach Dokumentnamen wie `Dokument X` filtern.
   - Das ist sprach-/versionsabhaengig und unzuverlaessig.

3. **Nicht** vergessen, das Zieldokument aus dem Blanko-Cleanup auszunehmen.

4. **Nicht** bei neu gestarteter Instanz automatisch davon ausgehen, dass das zuerst sichtbare Blankodokument ein legitimer Altzustand ist.

5. **Nicht** im Fehlerfall sowohl Dokument als auch App unkontrolliert mehrfach schliessen.
   - Die Reihenfolge im `finally` muss sauber bleiben.

## Fachliche Erwartung an XHub
Nach Umsetzung dieses Fixes soll in XHub gelten:

- Der Benutzer oeffnet eine Akte oder springt zu BU/BI/BE.
- Word darf sichtbar in den Vordergrund kommen.
- Das eigentliche Zieldokument darf offen bleiben.
- Zusaetzliche, ungespeicherte Hilfs-/Blankodokumente duerfen nach der Aktion nicht herumstehen bleiben.
- Wenn die Aktion scheitert, muss XHub moeglichst rueckstandsfrei aufraeumen.

## Referenz in AkteX
Die andere KI kann sich in AkteX vor allem an diesen Bereichen orientieren:

- `Services/WordService.cs`
- `CreateOrAttachWordApplication()`
- `OpenOrGetDocument(...)`
- `CloseTransientEmptyDocuments(...)`
- `TryCloseDocument(...)`
- `TryQuitWordApplication(...)`

Wichtig: Die KI soll das nicht blind kopieren, sondern an die Struktur von XHub anpassen. Die Schutzlogik ist der eigentliche Kern.

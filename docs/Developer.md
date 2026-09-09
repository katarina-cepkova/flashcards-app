# Flashcards App — Developer Guide

See the main [**README**](../README.md) for a quick overview, or the
[**User Guide**](./User_guide.md) for installing and everyday use — this document covers
neither and focuses on the codebase itself.
 
Flashcards is a desktop flashcard app for studying from topic-organized card sets,
built in WPF. Card content is written in Markdown, with a live formatted preview and a
formatting toolbar; review sessions track correct/incorrect answers per card and
requeue missed cards sooner than ones already answered well.
 
This is a from-scratch WPF rewrite of an earlier WinForms version of the same idea —
the goal wasn't just to port the old code, but to redesign it with a proper layered
architecture and MVVM, using what was learned in the meantime. It's my final project
for the following MFF subjects:
 
- NPRG035 (Programming in C# language)
- NPRG038 (Advanced C# Programming)
- NPRG064 (Programming user interfaces in .NET)

**Technologies:**
 
- **C# / .NET, WPF, MVVM** for the application itself
- **SQLite**, via `Microsoft.Data.Sqlite` directly (no ORM) for persistence
- **Markdig** / **Markdig.Wpf** for parsing and rendering the Markdown card content
- **xUnit**, with **Xunit.StaFact** for the WPF-dependent tests, for the test suite
This guide explains how the codebase is put together, and why several of its design
decisions look the way they do. It's written for a developer who has never seen the
project before — including a future version of myself.
 
**Building and running.** The solution targets **.NET 10** (`Flashcards.App` targets
`net10.0-windows` specifically, since it's a WPF project — Windows only). Open
`Flashcards.sln` in Visual Studio and run `Flashcards.App` (F5) — Visual Studio
doesn't always default to the right startup project on a fresh clone, so if F5 just
builds without launching a window, right-click **Flashcards.App** in Solution
Explorer and choose **Set as Startup Project** first. From the command line:
`dotnet build` from the repository root, then
`dotnet Flashcards.App/bin/Debug/net10.0-windows/Flashcards.App.dll` to run it. Run
the test suite with `dotnet test`, or through Visual Studio's Test Explorer.

---

## Table of contents

1. [Solution and folder layout](#1-solution-and-folder-layout)
2. [Architecture](#2-architecture)
3. [Flashcards.Core — the domain layer](#3-flashcardscore--the-domain-layer)
4. [Flashcards.Data — the persistence layer](#4-flashcardsdata--the-persistence-layer)
5. [Flashcards.App — the presentation layer](#5-flashcardsapp--the-presentation-layer)
6. [Flashcards.Tests — the test suite](#6-flashcardstests--the-test-suite)
7. [Extending the application](#7-extending-the-application)
8. [Known limitations and future work](#8-known-limitations-and-future-work)
9. [Learning path: how this project evolved](#9-learning-path-how-this-project-evolved)
10. [WPF and XAML: what I learned along the way](#10-wpf-and-xaml-what-i-learned-along-the-way)

---

## 1. Solution and folder layout

The solution has four projects. Each one has a single job:

```
Flashcards.sln
├── Flashcards.Core/      Domain entities, repository interfaces, learning algorithm.
│                         No dependency on WPF or on any concrete database technology.
├── Flashcards.Data/      SQLite implementation of Core's repository interfaces.
│                         Depends on Core only.
├── Flashcards.App/       WPF/MVVM user interface. Depends on Core (for types) and
│                         Data (to construct concrete repositories at startup).
└── Flashcards.Tests/     xUnit test suite covering Core, Data, and parts of App.
                          Depends on all three of the above.
```

Inside each project:

```
Flashcards.Core/
├── Entities/          Topic, Flashcard, TopicListItem — plain domain objects
├── Repositories/      ITopicRepository, IFlashcardRepository, DomainExceptions
└── Learning/          LearningQueue, IRequeuePolicy, RequeuePolicy, LearningSessionCard

Flashcards.Data/
├── Database/          DatabaseInitializer, DatabaseLocation, ADO.NET extension helpers
└── Repositories/      SqliteTopicRepository, SqliteFlashcardRepository, *Ordinals, helpers

Flashcards.App/
├── Models/            AppState (the UI state machine enum)
├── ViewModels/        MainViewModel, split into partial classes by concern
├── Views/             MainWindow.xaml(.cs) — the single window of the application
├── Services/          FlashcardManager, Markdown pipeline/editing, localization
├── Commands/          RelayCommand, AsyncRelayCommand
├── Converters/        IValueConverter/IMultiValueConverter classes for XAML bindings
├── Common/            ObservableObject (INotifyPropertyChanged base class)
├── Dialogs/           ColorPickerDialog
└── Resources/         Colors/Spacing/Typography/Icons dictionaries, per-culture strings,
                       per-control style files

Flashcards.Tests/
├── Learning/            LearningQueue, RequeuePolicy unit tests
├── Repositories/        Integration tests against a real (in-memory) SQLite database
├── Database/            DatabaseInitializer tests
└── App/Markdown/        Tests for the markdown toolbar's editing operations
```

---

## 2. Architecture

The app is a layered architecture on the outside, with MVVM inside the WPF layer.
Dependencies only ever point one way:

```
Flashcards.App  ──depends on──▶  Flashcards.Data  ──depends on──▶  Flashcards.Core
      │                                                                  ▲
      └────────────────────── depends on ────────────────────────────────┘
```

`Flashcards.Core` doesn't reference WPF or SQLite at all. Instead of talking to a
database directly, it defines two interfaces — `ITopicRepository` and
`IFlashcardRepository` — that describe what persistence needs to do. `Flashcards.Data`
is one implementation of those interfaces, built on `Microsoft.Data.Sqlite`.

This buys two things:

- **`Core` can be unit-tested with no database and no UI.** The learning algorithm
  (`LearningQueue`, `RequeuePolicy`) is tested with plain in-memory objects. Nothing in
  `Flashcards.Tests/Learning` starts SQLite or WPF, because `Core` doesn't depend on
  either one.
- **A bug in one layer only ever points to that layer.** If a `SqliteDataReader` reads
  back the wrong column, that has nothing to do with why a button won't enable, and
  vice versa. The two hardest parts of this project — SQL/ADO.NET and WPF/XAML — never
  touch each other's code.

`MainWindow`'s constructor is the only place in the whole solution allowed to construct
a concrete `SqliteTopicRepository`/`SqliteFlashcardRepository` (see [§5.1](#51-composition-root-and-mainwindow)).
Everywhere else — most importantly `MainViewModel` — only ever calls the
`ITopicRepository`/`IFlashcardRepository` interfaces. This is what a full
dependency-injection container would normally give you, done by hand, which is fine at
the scale of one window and two entities.

**Who handles what.** User input goes through `MainWindow.xaml.cs` (the only `Window`
in the app) and through `Commands/RelayCommand`/`AsyncRelayCommand`, which every button
and key binding is wired to instead of a code-behind event handler. The actual command
logic lives in `MainViewModel`. Database access goes through exactly two classes,
`SqliteTopicRepository` and `SqliteFlashcardRepository` — no ViewModel, converter, or
other service ever opens a `SqliteConnection` directly — plus `DatabaseInitializer`/
`DatabaseLocation` for schema creation and file-path resolution at startup.

**Why not EF Core or a DI container?** This project uses raw ADO.NET
(`Microsoft.Data.Sqlite` directly, no ORM) and manual constructor wiring instead. Both
were scope decisions: pulling in Entity Framework or
`Microsoft.Extensions.DependencyInjection` would add a layer of framework "magic" to
learn and explain, for very little payoff at the size of this app. The trade-off is
that the repositories do more manual, visible work than an EF Core `DbContext` would
(see [§4.3](#43-ordinals-transactions-and-batched-saves)) — but every step between
"user clicks Save" and "row exists in flashcards.db" is traceable by reading code, not
by trusting a framework.

---

## 3. Flashcards.Core — the domain layer

### 3.1 Entities

**`Topic`** has `Id` (nullable `long`, `null` until saved), `Name`, and `CreatedAt`.

**`Flashcard`** belongs to a `Topic` via `TopicId`. Its `FrontText`/`BackText` hold
Markdown source, not rendered content (see [§5.6](#56-markdown-editing-and-preview)).
It also carries `CorrectAnswersCount`/`IncorrectAnswersCount` for the learning
algorithm, a `ColorArgb` background color, and an `IsDeleted` flag used for soft
deletion during editing (see [§5.4](#54-flashcardmanager)). `LastReviewedAt` and
`NextReviewAt` are there for a spaced-repetition scheduling feature that was scoped
out — `NextReviewAt` isn't read by any code yet. `Flashcard.CreateDefault(topicId,
colorArgb)` is a static factory the UI calls whenever it needs a blank, not-yet-saved
card.

**`TopicListItem`** is just `{ Topic, CardCount }`, returned by a `JOIN`/`GROUP BY`
query (see [§4.2](#42-getallwithcardcountsasync-one-round-trip-instead-of-n1)). It's a
plain class rather than a `record` or a `ValueTuple`, and that choice matters more than
it looks: named tuple members are erased to `Item1`/`Item2` at compile time, and WPF's
binding engine resolves a path like `"Topic.Name"` through runtime reflection — it
can't see `Topic` or `CardCount` on a `ValueTuple` at all, so a binding to it fails
silently with no compile error. A plain class's properties are real runtime members,
so this doesn't happen. The rule this generalizes to: use named classes, not tuples,
for anything a WPF binding needs to see.

**Equality on `Topic` and `Flashcard`** is based on `Id` alone, and two entities with
`Id == null` are never treated as equal to each other — not even to themselves under
value comparison. This matters because a brand-new, not-yet-saved `Flashcard` has
`Id == null`. If equality compared content instead (`FrontText`, `BackText`, ...), two
blank new cards created back-to-back would compare equal, corrupting anything that
relies on default equality — `List.Contains`, dictionary keys, and so on. Tying
equality to `Id` means an unsaved entity simply opts out of value equality until the
database assigns it one.

This is also why `FlashcardManager.SelectCard` uses `ReferenceEquals` instead of
`Equals`/`==` to find a card: an unsaved card's domain equality (`Id == null`) makes it
"never equal to anything," including itself, so an `Equals`-based search would never
find it. Reference identity is the only way to say "this is the same in-memory object"
for a card that hasn't been persisted yet.

### 3.2 Repository interfaces

`ITopicRepository` and `IFlashcardRepository` list every persistence operation the app
needs — `AddAsync`, `RenameAsync`, `GetByTopicIdAsync`, `SaveChangesAsync`, and so on —
without exposing anything about SQL or connection strings. `DomainExceptions` defines
`EntityNotFoundException` and `DuplicateEntityException`, thrown when a row that
should exist doesn't, or a unique constraint is violated.

Two methods encode actual business rules rather than plain plumbing:

- **`IFlashcardRepository.SaveChangesAsync`** is the single entry point the UI uses to
  persist a whole editing session at once: new cards are inserted, cards flagged
  `IsDeleted` are removed for real, everything else is updated. The mechanics of this
  are covered in [§4.3](#43-ordinals-transactions-and-batched-saves).
- **`ITopicRepository.MergeAsync(sourceTopicId, targetTopicId)`** moves every flashcard
  from one topic to another and deletes the now-empty source, as one transaction. It's
  implemented and tested at the repository level, but `Flashcards.App` never calls it.
  Renaming a topic to a name that already exists is currently just rejected:
  `RenameAsync` doesn't check for collisions itself, it executes the `UPDATE` and lets
  SQLite's `UNIQUE` constraint on `Topics.Name` surface as a `DuplicateEntityException`.
  The UI's live validation (`IsTopicNameValid`) is meant to stop the user from
  submitting a colliding name in the first place, so that exception is a safety net
  rather than something the UI reacts to today. `MergeAsync` is left in place as a
  ready-to-use building block for a future "rename onto an existing topic → offer a
  merge" flow.

### 3.3 The learning algorithm

A learning session is driven by two collaborating types in `Flashcards.Core.Learning`.
 
**`LearningQueue`** owns the order and membership of cards still to review. It's backed
by a `LinkedList<LearningSessionCard>` rather than an array-backed list, because cards
need to be removed from and reinserted at an arbitrary position in the middle of the
sequence — a linked list does that in O(1) once you have the target node, where an
array-backed list would have to shift elements.
 
**`IRequeuePolicy`** (implemented by `RequeuePolicy`) answers two questions the queue
itself doesn't try to answer: has this card been learned well enough to leave the
queue (`ShouldRemove`), and if not, how many cards ahead should it come back
(`StepsAhead`)? Keeping the policy separate from the queue means the queue's own
bookkeeping — linked-list operations, wraparound, tracking the current card — never
has to change if only the scheduling heuristic changes. Tuning how aggressively a
missed card returns, or swapping in a different policy entirely, only touches
`RequeuePolicy` or a new `IRequeuePolicy` implementation.
 
`RequeuePolicy` is configured with eight numbers, passed in when a learning session
starts (`MainViewModel.LearningSession.StartNewLearningQueue`):
 
```csharp
new RequeuePolicy(
    minCorrectAnswers: 2, maxCorrectAnswers: 5, minCorrectRatio: 0.7,
    baseSteps: 5, minSteps: 3, maxSteps: 10,
    correctWeight: 1.5, incorrectWeight: 0.3);
```
 
**`ShouldRemove`** computes `correctnessRatio = SessionCorrectCount / totalAnswers` for
the card, then removes it if:
 
```
(correctnessRatio >= minCorrectRatio AND SessionCorrectCount >= minCorrectAnswers)
OR SessionCorrectCount >= maxCorrectAnswers
```
 
With the numbers above: a card is removed once it has at least 2 correct answers with
a ≥70% correctness ratio (e.g. 2 correct out of 2 or 3), *or* once it hits 5 correct
answers no matter how many times it was also missed. That second condition matters in
practice: take a card sitting at 4 correct / 6 incorrect (a 40% ratio). Without the
`maxCorrectAnswers` escape hatch, it would need to be answered correctly **10 more
times in a row** (with no further misses) before its ratio alone crosses 70% —
`correctCount / (correctCount + 6) >= 0.7` only once `correctCount` reaches 14. With
`maxCorrectAnswers: 5`, the same card is removed after just **one** more correct
answer, at a 50% ratio. The absolute cap exists so a card that started out rough
doesn't have to fully recover its ratio before the session lets it go.
 
**`StepsAhead`** computes a raw distance and then clamps it, following the same three
branches as the actual implementation:
 
```
recentAdjustment = wasLastAnswerCorrect ? baseSteps * correctWeight
                                         : -(baseSteps * incorrectWeight)
rawSteps         = (baseSteps * correctnessRatio) + recentAdjustment
 
if rawSteps <= minSteps:  StepsAhead = minSteps
if rawSteps >= maxSteps:  StepsAhead = maxSteps
otherwise:                StepsAhead = round(rawSteps)
```
 
With `baseSteps: 5, correctWeight: 1.5, incorrectWeight: 0.3`, the two branches are
deliberately lopsided: answering correctly adds `5 * 1.5 = 7.5` steps, while answering
incorrectly only subtracts `5 * 0.3 = 1.5`. Two worked examples:
 
- A card with a low ratio so far (1 correct out of 3, ratio ≈ 0.33) that was *just*
  answered correctly: `rawSteps = 5 × 0.33 + 7.5 ≈ 9.15`, rounded to **9** — close to
  `maxSteps`, so a card that's still mostly wrong overall gets pushed far back the
  moment it's answered right once.
- A well-performing card (9 correct, 1 incorrect, ratio 0.9) that was *just* answered
  incorrectly: `rawSteps = 5 × 0.9 − 1.5 = 3.0`, which already sits at `minSteps` — a
  single miss is enough to pull even a strong card back to the front of the rotation.
In other words, with this particular tuning, the *most recent* answer dominates the
result far more than the running ratio does — `minSteps`/`maxSteps` aren't just a
theoretical safety net, they're doing real work, since a correct answer alone is
usually enough to saturate `rawSteps` past `maxSteps` regardless of ratio.
 
`LearningQueue.RequeueAhead` computes the reinsertion point by walking `stepsAhead - 1` further nodes
past wherever the next card already is (moving past the next card already accounts for
one step), wrapping around to `_cards.First` if it walks off the end of the list. This
kind of off-by-one-prone pointer surgery is exactly why
`Tests/Learning/LearningQueueTests.cs` and `RequeuePolicyTests.cs` exist — it's easy to
get subtly wrong and hard to spot by inspection alone.
 
---

## 4. Flashcards.Data — the persistence layer

`Flashcards.Data` is the only project allowed to `using Microsoft.Data.Sqlite;`. It
implements `ITopicRepository`/`IFlashcardRepository` against a single SQLite file.

### 4.1 Schema and initialization

`DatabaseInitializer.EnsureInitialized()` runs `CREATE TABLE IF NOT EXISTS` for
`Topics` and `Flashcards` on every startup. `Flashcards.TopicId` has
`FOREIGN KEY ... ON DELETE CASCADE`, so deleting a `Topic` row cascades to its
flashcards at the database level — `MainViewModel.DeleteOrLeaveSetAsync` only has to
remove the topic from `AvailableTopics` in memory afterwards to keep the UI in sync.

`DatabaseLocation` resolves the actual file path and connection string. It's kept
separate from `DatabaseInitializer` so tests can point at an in-memory database
instead (see [§6.2](#62-integration-tests-against-a-real-in-memory-database)).

### 4.2 `GetAllWithCardCountsAsync`: one round trip instead of N+1

The topic-selection screen needs every topic together with its flashcard count. Doing
that with one query for topics and then a `COUNT(*)` per topic would be both slow
(N+1 round trips) and racy (the database could change between queries).
`SqliteTopicRepository.GetAllWithCardCountsAsync` issues a single
`LEFT JOIN ... GROUP BY` instead and maps each row into a `TopicListItem`. It has to be
a `LEFT JOIN`, not an `INNER JOIN` — a topic with zero flashcards still needs to show up
with `CardCount = 0`, not disappear from the list.

### 4.3 Ordinals, transactions, and batched saves

This project reads every `SELECT` back manually via `SqliteDataReader.GetX(ordinal)`,
since there's no ORM. Hardcoding those ordinals as magic numbers inside a
`while (reader.Read())` loop would be fragile — reordering columns in the `SELECT`
would silently break every number. Instead, `FlashcardOrdinals`/`TopicOrdinals` are
small structs that resolve every column's ordinal once, via a static `FromReader`
factory called before the read loop starts (not per row — a reader's column layout
can't change between rows of the same query). Every row read after that indexes
through the resolved struct, so a column reorder breaks a name lookup immediately
instead of silently reading the wrong value into the wrong field.

`RepositoryHelpers.EnsureRowsAffected(rowsAffected, id, entityName)` is a small shared
guard used after every `UPDATE`/`DELETE` by id: if no row was affected, it throws
`EntityNotFoundException($"{entityName} {id} does not exist.")`. This turns "silently
did nothing because the id didn't exist" into an immediate, loud failure instead of a
bug that only shows up later as a UI inconsistency.

While a set is open for editing, add/edit/delete only touch the in-memory
`ObservableCollection<Flashcard>` inside `FlashcardManager` — nothing hits the database
until the user saves. `IFlashcardRepository.SaveChangesAsync` then reconciles the whole
working set in one atomic transaction: cards with `Id == null` are inserted, cards
flagged `IsDeleted` are deleted for real, everything else is updated. It returns the
refreshed list (new cards now carry real ids) so the UI rebuilds its view from a single
source of truth instead of patching ids in place one at a time.

Batching the whole session into one save, instead of hitting the database on every
keystroke, keeps the UI responsive and gives the user an explicit, revertible "did I
save this?" moment — which is exactly what `IsDirty` and
`TrySaveOpenSetIfNeededAsync` exist to surface. Because several statements run against
the same `SqliteConnection`/`SqliteTransaction` in sequence, they have to run one at a
time rather than in parallel: a single `SqliteConnection` isn't thread-safe for
concurrent commands, so each insert/update/delete is `await`ed individually rather than
fired off with something like `Task.WhenAll`.

---

## 5. Flashcards.App — the presentation layer

### 5.1 Composition root and MainWindow

`MainWindow`'s constructor is the application's composition root. It resolves the
connection string, runs `DatabaseInitializer`, constructs the two concrete SQLite
repositories, and passes them into `MainViewModel` — after `InitializeComponent()`, so
`DataContext` is assigned only once every control already exists to bind against. No
other class constructs a `SqliteTopicRepository`/`SqliteFlashcardRepository`; everywhere
else depends only on the `Core` interfaces.

`MainWindow.xaml.cs` also handles input that has to live in the view: finding the
`PreviewViewer`'s internal `ScrollViewer` after the visual tree is built, syncing the
Markdown editor with its preview pane while scrolling, focus management, and routing
the shared scrollbar and mouse wheel to the right `ICommand`. A ViewModel shouldn't need
to know a `ScrollViewer` exists, so this stays in the view.

### 5.2 The `AppState` state machine

```csharp
internal enum AppState
{
    ClosedSet, CreatingSet, SelectingSet,
    OpenedSetView, OpenedSetEdit, LearningSession
}
```

`MainViewModel.CurrentState` is the single source of truth for "what is the user
currently doing." Almost every other piece of UI behavior is a function of this one
value: which panel is visible (`StateToVisibilityConverterBase` and its subclasses),
whether `EditTextBox` is read-only (`StateToReadOnlyConverter`), whether a given
command can run.

That leads to one rule worth stating plainly: **state-based conditions belong in
XAML, as a converter bound to `CurrentState`; data/object-based conditions belong in a
command's `canExecute` delegate.** `NextCommand`'s `canExecute` checks both
`_flashcardManager.CanMoveToNext()` (a fact about the data, from `Cards`) and
`_currentState != AppState.LearningSession` (a fact about the state) — each half lives
where a future reader would expect to find it.

Transitions between states go through a handful of shared private methods —
`SetOpenTopic`, `CloseTopic`, `TrySaveOpenSetIfNeededAsync` — so the "is there unsaved
work? ask before discarding it" logic isn't duplicated across `EnterCreatingSetAsync`,
`EnterSelectingSetAsync`, and `DeleteOrLeaveSetAsync`.

### 5.3 `MainViewModel` split into partial classes

`MainViewModel` is `partial`, split across five files by concern:

| File | Responsibility |
|---|---|
| `MainViewModel.cs` | Fields, constructor, command wiring |
| `MainViewModel.App.cs` | Application-wide concerns (e.g. localization) |
| `MainViewModel.Topic.cs` | Topic name validation/editing |
| `MainViewModel.SetNavigation.cs` | `AppState` transitions: create/select/open/delete a set, save |
| `MainViewModel.SetEditing.cs` | Editing an open set's flashcards |
| `MainViewModel.LearningSession.cs` | Running a learning session |

There's exactly one `MainViewModel` instance and one `DataContext` at runtime — the
split is purely organizational, so each file stays a size a reader can hold in their
head. `#region` blocks group related members further inside each file (e.g.
`#region Save`, `#region Create set` inside `MainViewModel.SetNavigation.cs`).

### 5.4 `FlashcardManager`

`FlashcardManager` lives in `Services/` — the name sounds like a repository, but it
manages in-memory navigation state, not persistence. It wraps the
`ObservableCollection<Flashcard>` of the currently open set and answers "which card is
selected," "can I move next/previous," "how many active cards are there."

It tracks two related but different notions of "which card":

- **`PhysicalIndex`** — the raw index into `Cards`, including cards flagged
  `IsDeleted`.
- **`LogicalIndex`** — the position of the current card counting only non-deleted
  cards, i.e. what the user actually perceives as "card 3 of 10."

The reason for keeping both: deleting a flashcard while editing is a soft delete in
memory. The card is flagged `IsDeleted = true` and stays in `Cards` as a tombstone
until the set is saved — `SaveChangesAsync` purges tombstones for real, and only then
does `ReplaceFlashcardManager` rebuild the manager without them. That lets navigation
correctly skip a just-deleted card before a save, with no separate "pending deletions"
list to reconcile later. But `CardNavigationScrollBar` should only ever show and step
through cards the user still considers "in the set," so its range and position are
driven by `LogicalIndex`/`ActiveFlashcardCount`, not `PhysicalIndex`/`Cards.Count`.
`FindNextActiveIndex` skips tombstones going forward or backward, and
`FindPhysicalIndexOfActiveCard` translates a logical position back to a physical one —
together they're the bridge between the two index spaces.

### 5.5 Commands and converters

`RelayCommand` and `AsyncRelayCommand` (in `Commands/`) implement `ICommand` by
wrapping delegates, so individual commands don't need their own hand-written `ICommand`
class. `RelayCommand` wraps a synchronous `Action`; `AsyncRelayCommand` wraps an async
`Func<Task>` and also guards against re-entrancy with an internal `_isExecuting` flag —
a rapid double-click on `SaveButton` reports `CanExecute() == false` while the first
save is still in flight, instead of firing `SaveFlashcardsAsync` twice concurrently
against the same connection.

`Converters/` holds around twenty small, single-purpose `IValueConverter`/
`IMultiValueConverter` classes, most of them driven by `AppState`
(`StateToVisibilityConverterBase` and its subclasses: `StateToHiddenConverter`,
`StateToCollapsedConverter`, `StateToReadOnlyConverter`, ...). Each converter handles
one true/false decision rather than branching internally on a `switch`. That keeps
each XAML binding declarative and each converter trivially testable in isolation, at
the cost of having many small files — a trade-off made deliberately for this codebase.

### 5.6 Markdown editing and preview

Flashcard content is stored and edited as plain Markdown source text
(`Flashcard.FrontText`/`BackText`), and only rendered into a `FlowDocument` for the live
preview. The two stay separate representations of the same content: Markdig
(`Services/MarkdownPipelineProvider.cs`) parses the source, and
`Converters/MarkdownToFlowDocumentConverter.cs` together with
`Services/CodeBlockHighlightRenderer.cs` render it into the `FlowDocument` the preview
pane displays.

`Services/MarkdownEditingService.cs` implements the toolbar operations (`InsertQuote`,
`InsertLink`, `InsertCodeBlock`, inline-code toggle, heading level cycling
`H1↔H6↔plain`, `ToggleStrikethrough`) by manipulating the plain-text source directly at
the cursor, rather than touching the rendered `FlowDocument` — the source text is the
only thing that actually gets persisted.

**Known limitation:** clicking a rendered Markdown link in the preview
(`Hyperlink.RequestNavigate`) doesn't currently fire, even though the event handler is
wired correctly (`MainWindow.Hyperlink_OnRequestNavigate` exists and is subscribed).
This is left as an open issue rather than worked around with something fragile.

### 5.7 Localization

`Resources/Strings/Strings.{en-GB,sk-SK,cs-CZ,de-DE}.xaml` hold per-culture string
resources. The rest of the XAML references them via `DynamicResource`, not
`StaticResource`, specifically so the language can switch at runtime without a
restart: `Services/LocalizationService.cs` swaps which strings dictionary is merged
into `Application.Current.Resources.MergedDictionaries`, and every `DynamicResource`
binding re-resolves against the new one automatically.

---

## 6. Flashcards.Tests — the test suite

### 6.1 Framework choices

The suite uses `xunit.v3` plus `Xunit.StaFact`. A handful of tests (in
`App/Markdown/`) construct and drive an actual `RichTextBox` to test its behavior, and
`[StaFact]`/`[StaTheory]` are what let those tests run at all.

### 6.2 Integration tests against a real, in-memory database

Rather than mocking `IFlashcardRepository`/`ITopicRepository`, the repository tests in
`Repositories/` run against a real SQLite database — just an in-memory one:

```csharp
$"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared;Foreign Keys=True"
```

Three details here are easy to get wrong:

- **`cache=shared`** lets multiple `SqliteConnection`s (one per repository call, since
  repositories don't hold a connection open) see the same in-memory database instead of
  each getting its own empty one. But a shared-cache in-memory database only stays
  alive while at least one connection to it is open — so each test class opens and
  holds a `_keeperConnection` for the test's lifetime
  (`IAsyncLifetime.InitializeAsync`/`DisposeAsync`). Without it, the database would be
  destroyed the moment the first repository call's connection closes, and every
  later call in the same test would silently start against a fresh, empty database.
- **`Guid.NewGuid()`** gives each test class its own uniquely named in-memory database,
  so tests running in parallel — or just run more than once — never see each other's
  data.
- **`Foreign Keys=True`** has to be set on every connection string, test or production,
  since SQLite doesn't enforce foreign keys by default and enforcement is per
  connection, not per database. Forgetting it on a test connection string would let a
  test pass despite a `TopicId` referencing a nonexistent topic, hiding a bug that would
  only surface with a differently-configured connection elsewhere.

### 6.3 `InternalsVisibleTo`

`FlashcardManager` and a few repository/database helper types are `internal`, since
nothing outside the codebase's own `ViewModels`/`Services` folders has any business
constructing them directly. To let `Flashcards.Tests` still test them, `Core.csproj`,
`Data.csproj`, and `App.csproj` each declare `InternalsVisibleTo` pointing at
`Flashcards.Tests`.

---

## 7. Extending the application

A few common scenarios, and where to start:

- **Add a new persisted field to `Flashcard`/`Topic`.** Add the property to the entity
  in `Core/Entities`, add the column to `DatabaseInitializer`'s `CREATE TABLE`
  statement, add it to the relevant `*Ordinals` struct and to
  `LoadFlashcardDataIntoCommand`/the `SELECT` column list in the matching
  `Sqlite*Repository`, then to any UI binding/converter that should expose it. Existing
  database files created before the change won't gain the new column automatically —
  there's no migration mechanism (see [§8](#8-known-limitations-and-future-work)).

- **Add a new `AppState`.** Add the enum value in `Models/AppState.cs`, then check
  every `StateTo*Converter` and every command's `canExecute` that should react to it. A
  new state usually means touching several small converters rather than one central
  switch, since `CurrentState` is what most of the UI's visibility/enabled logic hangs
  off of (see [§5.2](#52-the-appstate-state-machine)).

- **Add a new Markdown toolbar operation.** Follow the pattern already in
  `MarkdownEditingService` — manipulate the plain-text source at the current
  selection/cursor, return the new text and caret position — and add a test in
  `Tests/App/Markdown/`, following `InsertXTests.cs`.

- **Add a new repository method.** Add it to the interface in `Core/Repositories`
  first, so the concrete implementation and any test double are forced to satisfy the
  same contract, then implement it in the matching `Sqlite*Repository` following the
  ordinals/transaction patterns in [§4.3](#43-ordinals-transactions-and-batched-saves).

- **Add a new UI language.** Add `Resources/Strings/Strings.{culture}.xaml` following
  the existing four, and register it in `LocalizationService` so it becomes selectable
  at runtime.

---

## 8. Known limitations and future work

- **`Hyperlink.RequestNavigate` does not fire** on links rendered inside the Markdown
  preview, despite correct event wiring in `MainWindow`. Known, deferred issue (see
  [§5.6](#56-markdown-editing-and-preview)).
- `NextReviewAt` on `Flashcard` exists in the schema and entity but isn't used by any
  scheduling logic yet — reserved for a spaced-repetition feature that was scoped out.
- `FrontSoundPath`/`BackSoundPath` are likewise present as a "time-permitting
  extension" that was never implemented.
- There's no schema migration mechanism. Adding a column to an existing table means
  either a fresh database file or a manual, one-off migration.
- **`Microsoft.Data.Sqlite` 10.0.10 transitively pulls a vulnerable native SQLite
  build.** Restoring the project reports `NU1903` for `SQLitePCLRaw.lib.e_sqlite3`
  2.1.11 (which bundles SQLite 3.49.1), flagged as
  [CVE-2025-6965](https://github.com/advisories/GHSA-2m69-gcr7-jv3q): a memory
  corruption issue in SQLite versions before 3.50.2, triggered by a query with more
  aggregate terms than available columns. There's no patched 2.1.x release as of this
  writing — the fix needs SQLitePCLRaw 3.x, which `Microsoft.Data.Sqlite` doesn't yet
  reference; a workaround (adding a `SourceGear.sqlite3` package reference to override
  the native binary SQLitePCLRaw loads at runtime) exists if this needs closing before
  an upstream fix ships. Every SQL statement in this project is fixed, parameterized
  text — no user input ever shapes the query itself (see
  [§4.3](#43-ordinals-transactions-and-batched-saves)) — so the specific attack vector
  (a maliciously crafted query) isn't reachable here, but the warning is real and worth
  tracking until `Microsoft.Data.Sqlite` ships a fix.

---

## 9. Learning path: how this project evolved
 
This WPF/MVVM app is a from-scratch rewrite of an earlier WinForms version of the same
idea. Several of the decisions above were arrived at partway through, not planned from
day one.
 
**Layering was a deliberate choice, not something inherited.** The WinForms version
mixed UI and data access more freely. Splitting into `Core`/`Data`/`App` here was a
conscious decision to practice dependency inversion and make the domain and
learning-algorithm code testable in complete isolation from WPF and SQLite. The
`Flashcards.Tests/Learning` suite is the clearest evidence it paid off — the queue and
policy tests never touch a database or a window.
 
**MVVM, `RelayCommand`, and the `AppState` machine** replace WinForms' more direct
"event handler pokes a control" style with declarative bindings driven by one state
value. The split between `canExecute` and an `IsEnabled` converter ([§5.2](#52-the-appstate-state-machine))
is the kind of distinction that only becomes clear once you've put the wrong kind of
condition in the wrong place and had to untangle it.
 
**Why everything is `async`/`Task`-based end to end** wasn't obvious at first either —
WPF, like any desktop UI framework, runs rendering and input handling on one thread
(the `Dispatcher`). A blocking call on that thread (e.g. `ExecuteReader()` instead of
`ExecuteReaderAsync()`) freezes the whole window for as long as the call takes — no
clicks, no repaints, "Not Responding" in the title bar. `async`/`await` releases the
thread back to the `Dispatcher` while waiting on I/O instead of blocking it, which is
also why `.Result`/`.Wait()` anywhere in this call chain would be a real risk: they
block the very thread that the awaited work might need in order to resume, which is
one of the more common ways to deadlock a WPF app.
 
**The tombstone approach to editing and the batched `SaveChangesAsync`**
([§5.4](#54-flashcardmanager), [§4.3](#43-ordinals-transactions-and-batched-saves)) came
from a specific realization midway through development: editing a set needed to feel
instant and freely reversible in memory, while still being backed by a single, explicit
save — not by hitting the database on every keystroke, and not by losing track of what
had changed since the last save.
 
**The requeue-based learning algorithm** ([§3.3](#33-the-learning-algorithm)) went
through more than one shape before settling on a `LinkedList` plus a pluggable
`IRequeuePolicy`. Earlier attempts at "just move missed cards back a fixed number of
positions" didn't account for a card's overall session performance, which is why the
final policy scores a correctness ratio instead of only reacting to the single most
recent answer.
 
**Comments were written as design decisions were made, not added at the end.** The
trickier pieces of code — `RequeueAhead`'s pointer walk, the physical/logical index
split, the `+1` cursor-offset quirk in `InsertCodeBlock` — carry inline comments
explaining why a particular non-obvious choice was made, so a later reader doesn't have
to reverse-engineer the reasoning from the code alone.
 
**Making a type `internal` but still testable — "friend assemblies."** Marking
`LearningQueue.GetQueueOrder()` `internal` and adding `InternalsVisibleTo` pointing at
`Flashcards.Tests` was the first time this pattern came up, well before it got reused
for a broader pass narrowing `Flashcards.App`'s public surface down to only what
actually needs to cross an assembly boundary.
 
The overall trajectory isn't "WinForms code translated line-by-line into WPF." It's a
deliberate re-design that used the rewrite as a chance to apply layering, MVVM, and
test-first thinking to a codebase already understood at the feature level — which is
also why the remaining gaps in [§8](#8-known-limitations-and-future-work) are
specifically the parts that were consciously scoped out, not parts that were simply
forgotten.
 
---
 
## 10. WPF and XAML: what I learned along the way
 
Beyond the architecture decisions in [§9](#9-learning-path-how-this-project-evolved),
WPF and XAML themselves were new territory. WPF is the UI framework the whole
`Flashcards.App` project is built on, replacing the old WinForms UI: instead of code
that reaches into a control and sets `label.Text = ...` after some event fires, the UI
is described declaratively in XAML, and each control's properties are *bound* to
properties on `MainViewModel` — directly, or through a converter first. When a bound
property raises `PropertyChanged`, WPF updates the control on its own; nothing in the
ViewModel ever touches a control directly. Layout comes from panels (`Grid`,
`StackPanel`, ...) that position children relative to each other instead of fixed
pixel coordinates, and visual styling comes from shared `ResourceDictionary` files
instead of being set control by control. What follows is grouped by what part of that
model each lesson belongs to.
 
### 10.1 Layout, without a designer
 
- **Hand-writing XAML at all was a big jump from WinForms.** WinForms is built around
  dragging controls onto a designer surface; WPF expects the layout to be written as
  markup, by hand, with layout panels doing the positioning instead of pixel
  coordinates.
- **Layout is nested `Grid`s, not fixed positions.** `MainWindow.xaml` alone nests 17
  `Grid`s to get a layout that resizes and reflows instead of one built from absolute
  coordinates.
- **The window keeps the default OS chrome on purpose.** There's no custom
  `WindowStyle`/`WindowChrome` here — building a fully custom title bar and window
  border is its own separate project, plenty of shipped WPF apps ship with the
  default chrome too, and it wasn't worth the time against the rest of the scope.
### 10.2 Data binding
 
- **A whole collection can be bound straight to a list control.** The topic-selection
  screen is a `ListView` (with a `GridView` for its columns) whose `ItemsSource` is
  bound directly to `TopicsForSelection` — populating the list is just keeping that
  `ObservableCollection` up to date, not writing code that adds/removes list items by
  hand.
- **The `TopicListItem`-instead-of-tuple rule (see [§3.1](#31-entities)) was found as a
  live bug, not read about beforehand.** The topic-selection list showed the right
  *number* of rows but every row was blank. Narrowing it down step by step — data
  present in memory? `ItemsSource` actually wired to it? — pointed at the binding
  itself, and swapping `DisplayMemberBinding` for a trivial `{Binding}` (dumping the
  whole object via `ToString()`) confirmed the data was reaching the row, just not the
  named tuple members inside it. That's what led to replacing the `(Topic, int)` tuple
  with a real `TopicListItem` class.
- **Combining several bound values with `MultiBinding`.** Some visibility rules depend
  on more than one property at once — `FocusAndNotReadOnlyToVisibleConverter` and
  `MessageAndStateToVisibleConverter` are `IMultiValueConverter`s bound via
  `MultiBinding`, so one converter can look at, say, focus state *and* `CurrentState`
  together instead of needing a separate property that combines them first.
- **`TwoWay` binding doesn't work against a computed/read-only property.** Trying to
  two-way bind a language toggle against a property with no real setter needed
  `Binding.DoNothing` inside a converter's `ConvertBack`, in
  `LanguageToIsCheckedConverter`, to tell WPF "don't try to write this value back."
- **`INotifyPropertyChanged`/`OnPropertyChanged`** is the mechanism that makes any of
  the above actually update on screen when a property changes — learned by building
  `ObservableObject` as a small reusable base class, rather than wiring the event by
  hand in every ViewModel.
### 10.3 Resources and styling
 
- **Resources are wired together through merged dictionaries.** `App.xaml` merges
  `Colors.xaml`, `Typography.xaml`, `Icons.xaml`, `Spacing.xaml`, `Converters.xaml`, a
  per-culture `Strings.*.xaml`, and one file per control style, all into
  `Application.Current.Resources`. `LocalizationService` swaps out just the strings
  dictionary at runtime to change language — the same mechanism that lets any of these
  dictionaries be split up and recombined at all.
- **Converters, and passing data into them via `ConverterParameter`,** were both new —
  writing a small reusable `IValueConverter` instead of putting the logic in the
  ViewModel, and using the parameter to make one converter class handle several related
  cases (e.g. comparing `CurrentState` against a parameter string instead of writing a
  separate converter per state).
- **`MultiTrigger` can combine more than one condition in a `Style`.**
  `FlagRadioButtonStyle` uses one to keep the background from changing on focus while
  the control is also read-only — a single `Trigger` can only react to one property at
  a time, so combining `IsFocused` and `IsReadOnly` needed a `MultiTrigger` instead.
- **Styles can build on each other with `BasedOn`, but a control's default template
  isn't always overridable from the outside.** `FormattingButtonStyle` and
  `FormattingToggleButtonStyle` both extend one shared `FormattingButtonBaseStyle` this
  way. But `FlashcardTextBoxStyle` needed a full custom `ControlTemplate`, not just
  `Setter`s, because WPF's built-in Fluent dark theme (`ThemeMode="Dark"` on the
  `Window`) forces its own background onto a focused `TextBox` regardless of what its
  `Background` property is set to — the only way around that turned out to be a
  template built entirely from `TemplateBinding`s, so the flashcard's own color always
  shows through even while the `TextBox` is focused. That template also has to name its
  inner `ScrollViewer` exactly `PART_ContentHost`, because `TextBox` looks for that
  specific name internally to host the actual editable text — a reminder that a custom
  template still has to honor the contract the base control expects, not just look
  right.
### 10.4 Controls, focus, and commands
 
- **A click can steal focus from a control in a way that's easy to miss.** A global
  `PreviewMouseDown` handler meant to leave text-editing controls alone checked
  `is not TextBox`, but `RichTextBox` isn't a `TextBox` — it's a `TextBoxBase`, same as
  `TextBox` — so every click into the card editor was stealing its own focus right back
  out from under it. The fix was checking against `TextBoxBase`, the actual shared base
  class, instead of the more familiar-sounding but wrong one.
- **Bridging a `RichTextBox`'s `FlowDocument` to a plain `string` needed an attached
  property**, since `Document` isn't a bindable dependency property on its own.
  `RichTextBoxHelper` handles both directions — Model→UI and UI→Model — each behind its
  own guard flag, so writing the model's value into the box doesn't immediately trigger
  the box's own change handler and write straight back, feeding into an infinite loop.
  Getting that guard right took more than one pass: an early version set the guard flag
  without a `finally`, so an exception partway through left it stuck `true` forever and
  silently blocked all further updates in that direction.
- **Commands and keyboard shortcuts connect through `Window.InputBindings`.**
  `MainWindow.xaml`'s `KeyBinding`s bind straight to `ICommand` properties on the
  ViewModel (`FlipCommand`, `SaveCommand`, `NextCommand`, ...) — and, for undo/redo,
  reuse WPF's own built-in `ApplicationCommands.Undo`/`Redo` instead of writing custom
  ones.
### 10.5 Rendering the Markdown preview
 
- **Markdig and recursively walking a `FlowDocument`.** Turning parsed Markdown into a
  styled preview means walking the resulting `FlowDocument` after Markdig builds it —
  `ForEachParagraph`/`ForEachParagraphRecursive` in
  `MarkdownToFlowDocumentConverter.cs` recurse into `Section`s and `ListItem`s because
  a heading, a quote, or an inline code span can be nested arbitrarily deep inside a
  list or a blockquote, so a single flat loop over the document's top-level blocks
  isn't enough to find them all.
- **Keeping the Markdown editor and its live preview scrolled together**
  (see [§5.1](#51-composition-root-and-mainwindow)) meant learning how WPF exposes (or
  doesn't expose) a control's internal `ScrollViewer`, and guarding against the two
  panes fighting each other's scroll events in a feedback loop.
- **Pulling shared string-manipulation logic out into one place.** The Markdown
  toolbar's insert operations all do cursor/selection math on plain text
  (see [§5.6](#56-markdown-editing-and-preview)); writing that once as reusable helpers
  instead of repeating slightly different versions per button was itself a lesson in
  where to draw the line between "shared enough to extract" and "specific enough to
  leave inline."

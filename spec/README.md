# Specification of the final project for relevant C# courses
(When modifying this document, please maintain the layout and structure and follow the inline instructions.)

## C# Courses selection
(Change `[ ]` to `[x]` for the courses you plan to use this final project for.)

- [x] NPRG035 (Programming in C# language | Programování v jazyce C#)
- [x] NPRG038 (Advanced C# Programming | Pokročilé programování v jazyce C#)
- [ ] NPRG057 (Advanced .NET Programming II | Pokročilé programování pro .NET II)
- [x] NPRG064 (Programming user interfaces in .NET | Programování uživatelských rozhraní v .NET)

## Specification

### Flashcards — WPF Rewrite

#### Motivation

Last year I built a flashcard application in WinForms as a course project. It works, but the codebase reflects where I was at the time: UI layout, rendering, and data access are all done by hand instead of using the tools the framework provides for them. Since then I've acquired more skills and knowledge, and I want to rebuild the application properly — not just port the old code, but redesign it so the architecture actually reflects what I've learnt over this year. Beyond the credit for these courses, the result is meant to be something I can show as a portfolio piece.

#### Use Case Scenarios

The primary user is a student (myself, but designed generally) studying from flashcards — for example, vocabulary, definitions, or formulas. A typical session: open a subject ("topic"), browse through its cards, flip each one to check the answer, and mark whether the answer was known. Between sessions, the user also edits content: adding new cards, formatting text, fixing mistakes, or reorganizing a topic.

#### Main Features

- **Topic management**: create, rename, delete a topic (a named collection of flashcards); list existing topics with a card count.
- **Flashcard management**: add, edit, delete cards within a topic.
- **Card content**: each card has a front and back side; each side can hold plain text or basic rich formatting (bold, italic, underline). *Time-permitting extensions:* LaTeX rendering for math content, and an attached voice note per side.
- **Review mode**: flip a card between front/back, step forward/backward through a topic, jump to a specific card, and mark each review as correct/incorrect (counters are stored per card).
- **Appearance**: set a background color per card.
- **Localization**: switch the UI language at runtime (English, German, Czech, Slovak).
- *Time-permitting extension:* **spaced-repetition scheduling** — use the correct/incorrect history to compute when a card is next due for review, and show which cards are currently due.

#### UI/UX

The application is a WPF desktop GUI. The main window shows the current topic's cards one at a time (browse/review view), with controls to flip, navigate, and mark a review outcome. A separate editing view lets the user add/modify card content and formatting. Topics are managed from a list/overview screen. All interaction is mouse/keyboard-driven, standard for a Windows desktop app — no command-line interface.

#### Persistence

Data is stored in a local SQLite database: topics, flashcards (front/back content, formatting, color, review counters), and, if the time-permitting extensions are implemented, paths to voice-note audio files. The database is accessed only through a repository abstraction, so the underlying storage engine is not hard-wired into the rest of the application and could be swapped later without changing application logic.

#### Libraries / Technologies

- .NET, WPF, MVVM
- SQLite for persistence
- A unit testing framework (e.g. xUnit)
- *Time-permitting:* a LaTeX rendering library for WPF, and an audio recording/playback library

#### Testing

The data-access layer (repository) is covered by a small set of unit tests, since it is the part most likely to break silently (e.g. incorrect mapping between the domain model and stored data). Beyond that, the application is verified manually by exercising the main use case end-to-end: create a topic, add and format cards, close and reopen the app, and confirm the data was correctly persisted and displayed.

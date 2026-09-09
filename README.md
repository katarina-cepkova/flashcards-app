# Flashcards App

A desktop flashcard app for studying from topic-organized card sets.

## The idea

A **flashcard** has two sides: a question or prompt on the front, the answer on the back.
You look at the front, try to recall the answer yourself, then flip the card to check.
How well you know it decides what happens next — get it wrong, and it comes back
around again sooner in the review queue; get it right consistently, and it eventually
drops out of rotation once it's earned that. A single correct answer isn't enough to
retire a card on the spot — it has to hold up over more than one attempt first, so a
lucky guess doesn't get mistaken for actually knowing it.

The app is built around that loop: put together a set of cards for whatever you're
studying, review them, and let the ordering do the work of surfacing what you're still
shaky on instead of grinding through every card the same number of times.

## Who it's for

Built with IT/CS students in mind, studying technical material — which is why card
content is written in **Markdown**, handy for code snippets and structured notes. You
don't need to actually know Markdown syntax to use it, though: the editor has a
formatting toolbar that applies bold, headings, code blocks, and the rest for you,
without needing to type the symbols by hand.

## What it does

- **Topics** organize your cards into named sets — a subject, a chapter, whatever
  you're studying together as one unit.
- **Cards** have a front and a back, each written in Markdown with a live-rendered
  preview, and their own background color.
- **Review sessions** shuffle a topic's cards and track your right/wrong answers as
  you go, driving the requeue behavior described above.
- **The interface** switches language at runtime — English, Slovak, Czech, German —
  and everything is stored locally in a SQLite database, no account or server involved.

## Documentation

- **[User guide](./docs/User_guide.md)** — installing and using the app day to day.
- **[Developer guide](./docs/Developer.md)** — architecture, design decisions, and how
  the codebase is laid out.

## Attributions

All toolbar icons are from [Flaticon](https://www.flaticon.com). Free icons on
Flaticon can carry different license terms per icon; these five require attribution:

| Icon | Author | Source |
|---|---|---|
| Refresh | deemakdaksina | [flaticon.com/free-icons/refresh](https://www.flaticon.com/free-icons/refresh) |
| Flashcards | Unknown Depths | [flaticon.com/free-icons/cards](https://www.flaticon.com/free-icons/cards) |
| Logout | Anggara | [flaticon.com/free-icons/leave](https://www.flaticon.com/free-icons/leave) |
| Code | Rendra Tri | [flaticon.com/free-icons/code](https://www.flaticon.com/free-icons/code) |
| Confirm | Roundicons | [flaticon.com/free-icons/confirm](https://www.flaticon.com/free-icons/confirm) |
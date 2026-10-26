# Twilight Struggle Assistant (TSA)

An iPad-friendly Blazor WebAssembly app used **alongside the physical board game** Twilight Struggle (GMT Deluxe Edition). Players enter what happens at the table (cards played, how they are used, influence placed, dice rolled). The app enforces the rules, keeps the board/VP/DEFCON/Military Ops/Space Race state, and blocks illegal plays. Priority: a simple, fast UI that helps the flow of the game rather than slowing it down.

Reference material the user supplied: the Deluxe rulebook (`TS_Rules_Deluxe.pdf`), "The Complete Card List v1.4" and the A2 board map. Rule numbers in code comments (e.g. "rule 6.1.2") refer to that rulebook.

## Layout

```
TSA.slnx
TSA/            Blazor WASM app (PWA, installable to iPad home screen)
  Pages/Home.razor          start screen + main game screen
  Components/               StatusBar, DecisionPanel, CardPicker, Board, SidePanels, CorrectionsPanel, Stepper
  Components/BoardInteraction.cs   taps/selections for the current decision, shared by DecisionPanel and Board
  Components/MapView.razor  "Map" button: the real board image with every counter drawn where it belongs
  Components/BoardLayout.cs pixel positions on board.jpg (country boxes, tracks), counter splitting
  wwwroot/images/board.jpg  board art (2448x1584) used by MapView
  wwwroot/images/tokens/    counters cut from the scanned counter sheet (us/ussr-control-N = front, -influence-N = back, 1-8; track markers; event-<cardId> reminders)
  Services/GameSession.cs   runs the engine, undo snapshots, localStorage save/resume
  Services/UiDecisionProvider.cs   engine <-> UI bridge (TaskCompletionSource per decision)
  wwwroot/css/app.css       all styling (no Bootstrap)
TSA.Core/       Rules engine, no UI dependencies
  Data/         countries.json, cards.json (embedded resources), GameData loader, CardIds constants
  Models/       GameState (all mutable state, JSON-serializable), Country, PlayerState, ActiveEffect, LogEntry
  Rules/        ControlCalculator, OpsCalculator, InfluenceRules, CoupRules, RealignmentRules, SpaceRace, Scoring, Victory/Defcon
  Engine/       GameRunner (turn flow), Decisions (request types), EventContext
  Events/       CardEvents registry + EarlyWarEvents / MidWarEvents / LateWarEvents
TSA.Tests/      xUnit tests; ScriptedDecisions answers engine questions from a queue
_archive/       Superseded early code and template files, kept for reference. Not built. Safe to delete.
```

## Commands

```
dotnet build TSA.slnx
dotnet test TSA.Tests/TSA.Tests.csproj
dotnet run --project TSA/TSA.csproj --launch-profile http     # http://localhost:5054
dotnet publish TSA/TSA.csproj -c Release                       # static site in bin/Release/net10.0/publish/wwwroot
```

The dev server does not hot-reload engine changes reliably; restart it after rebuilding.
To use on an iPad: host the published `wwwroot` on any static host (or the dev server over the LAN with `--urls http://0.0.0.0:5054`), open in Safari, Share → Add to Home Screen.

## Architecture (read this before changing the engine)

**Decision-driven engine.** `GameRunner.RunAsync()` is ordinary async code that walks the turn sequence. Whenever it needs input it awaits `Decisions` (`Option`, `Card`, `Country`, `Countries`, `Die`, `Influence`). Each is a `DecisionRequest` subtype answered by an `IDecisionProvider`:
- UI: `UiDecisionProvider` sets `Pending` and waits for `GameSession.Answer(...)`.
- Tests: `ScriptedDecisions` answers from a queue and throws `ScriptExhaustedException` when it runs out, so tests "run until the engine needs more input" and then assert on the state.

Answer types: Option → `int` index; Card → `int?`; Country → `string?`; Countries → `IReadOnlyList<string>`; Die → `int` 1-6; Influence → `Dictionary<string,int>`.

**Checkpoints, undo, persistence.** All progress lives in `GameState` (phase, turn, action rounds taken, setup step...). The runner calls the checkpoint callback at the start of each setup step, headline and action round. `GameSession` snapshots the serialized state there and saves it to `localStorage` (`tsa.current`, `tsa.history`). Undo = cancel the runner, restore a snapshot, start a new runner. It restarts the current step if anything was entered in it, otherwise goes back one step. **Never keep game progress in runner fields** (only per-round caches like `_roundOrigins`), or a restart from a checkpoint will lose it.

**Hidden information.** Hands are not tracked. A card is `Available` (in the deck or someone's hand), `Discard`, `Removed`, `InPlay` (Shuttle Diplomacy) or `NotInGame` (era not shuffled in yet, or an optional card that isn't enabled). When a player plays or discards, they pick from the `Available` cards. Physical reshuffles are recorded via Corrections → Cards → "Discard pile reshuffled".

**Events.** Each card's event is a static method registered by id in `EarlyWarEvents`/`MidWarEvents`/`LateWarEvents` (`Register`). Playability rules ("NATO needs Marshall Plan/Warsaw Pact", etc.) live in `CardEvents.WhyNot`. Ongoing effects are `ActiveEffect`s keyed by card id (`state.AddEffect(cardId, side, thisTurnOnly, data)`). The meaning of `Side` depends on the card:
- Red Scare/Purge: the side whose cards get -1.
- Cuban Missile Crisis: the side that loses if it coups.
- Latin American Death Squads: the beneficiary.
- Quagmire: US (trapped). Bear Trap: USSR (trapped).
- Missile Envy: the side forced to play it for Ops.
- Chernobyl: `Data` = the Region name.
- North Sea Oil: `Data` = the turn of the extra action round.
- Everything else: the player of the event.

**Map view.** `MapView` overlays counter images on `board.jpg` using `BoardLayout` coordinates, converted to percentages so it scales with zoom. Influence uses the control face when that side controls the country, and is split into counters of at most 8. If a counter sits off its box, fix the number in `BoardLayout` (country boxes are top-left corners of a 95x63 box). The token PNGs were cut from the scanned counter sheet (792x1224 JPG) by `tools/cut_tokens.py`: `python tools/cut_tokens.py <sheet.jpg> TSA/wwwroot/images/tokens contact.png` (needs Pillow; `contact.png` is a preview sheet for checking the cuts).

**Corrections.** `CorrectionsPanel` edits a copy of the state from the current checkpoint and pushes it as a new snapshot (`GameSession.ApplyCorrectionAsync`). It's the escape hatch when the table and the app disagree, or the engine has a gap.

## Conventions

- Match the existing style: file-scoped namespaces, `sealed` classes, XML doc comments on public members that aren't obvious, expression-bodied members where short.
- Country ids are snake_case (`west_germany`); card ids use `CardIds` constants, never magic numbers.
- Rules code returns a reason string for "why not" (`CoupRules.WhyNot`, `SpaceRace.WhyNot`, `CardEvents.WhyNot`). The UI shows these on disabled options and tiles, so keep them short and readable.
- Log every state change players should see via `state.AddLog` / `ctx.Log`.
- Add a test for every rule change. Prefer the rulebook's worked examples (see `OperationsRulesTests`, `ScoringAndSpaceTests`).
- Keep the UI touch-first: 44px+ targets, one decision at a time, two-step confirm for anything picked from a long list (cards, countries).

## Rules interpretations made (revisit if the group plays differently)

- Influence placement must spend all Ops unless no further marker is affordable.
- Events that grant Ops "as if a N Ops card" use exactly N (no Containment/Red Scare modifiers).
- Event "free" coups (Junta, Ortega, Tear Down This Wall) give no Military Ops and ignore DEFCON region limits. Che's coups are not "free": they count for Military Ops.
- Brush War and the other war cards ignore DEFCON region limits. An enemy superpower space adjacent to the target counts as an enemy-controlled adjacent country (rule 2.1.5).
- Realignments are only offered against countries with opponent influence (a roll elsewhere can only hurt you).
- Star Wars: the chosen card's event is carried out by its own side (US for both-player cards).
- Missile Envy: the swapped card's event fires for the player if it is theirs or both-player; otherwise the player immediately conducts Ops with it.
- Headline Defectors cancels the USSR headline (card to discard). UN Intervention has no effect as a headline.
- Final scoring: every region plus +1 VP for The China Card holder; Europe control still wins outright.

## Not implemented / ideas

- Chinese Civil War variant (rule 12) and promotional cards (Kremlin Flu, Non-Aligned Movement, etc.).
- Hand-size tracking and the tournament "reveal held card" check (scoring cards held at end of turn).
- Eagle/Bear Has Landed "discard held card" and Space Station "8 action rounds" are physical/optional; the app offers the extra AR but the player may pass.
- The Map view is read-only. Influence is entered on the region-tab tiles; tapping boxes on the real map to place influence would be a natural next step (`BoardLayout.Boxes` already has the hit areas).
- Adjacency to double-check against the physical board (these differed from the user's first draft of the data): Lebanon–Jordan, Israel–Syria, Burma–Thailand (not linked), Australia–Malaysia (Australia's only link), Philippines–Japan, Norway–Denmark (not linked), Benelux–France (not linked), Bulgaria (Greece and Turkey only).

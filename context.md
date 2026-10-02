# King's Court — context for continuing development

This file is a handoff document for whoever (human or Claude) picks up this project next. Keep it updated
as the project changes — when you finish a piece of work, add to the relevant section instead of leaving it
for someone to reconstruct from git history. Don't delete history from this file; append dated notes instead
if something changes significantly.

## What this project is

A digital, phone-multiplayer version of **King's Court**, a physical social-deduction card game designed by
a team of four students (Chris, Stylianos "Styli" Regas, Chase, Emma) for their CST-410 class. The physical
game is described in a Game Design Document (GDD v3.0) that the team wrote; the digital version is a Unity
port with online multiplayer added on top. The user (Chris, christopherluciano25@gmail.com) is driving
development via Claude Code, doing essentially none of the implementation by hand.

**Repo:** `C:\Users\17143\Desktop\CST-410KingsCourt`, GitHub `StylianosRegas/CST-410KingsCourt`.
**Current branch:** `Chris` (pushed once so far — see "Git state" below for what's uncommitted).
**Unity version:** 6000.6.1f1 — this is the version the team's project is pinned to
(`ProjectSettings/ProjectVersion.txt`). Installed locally at
`C:\Users\17143\UnityEditors\6000.6.1f1\Editor\Unity.exe` (see "Environment quirks" for why it's not in
the default location).

## The game rules (as implemented)

Source: the GDD plus a series of rulings the team made when asked (see git history / prior conversation for
the exact Q&A — the rulings themselves are what matters and are captured here and in
`Assets/Docs/RULES_ASSUMPTIONS.md`).

- **Players:** 5–10 (raised from 5–8 on 2026-09-30). One is the King (always dealt), one is the Prince, one
  is the Nobleman (Prince and Nobleman are the two Traitors, always dealt). Remaining seats are drawn from
  Loyalist roles (Queen, Knight, Elder, Court Wizard, Martyr), Chaos roles (Jester, Beggar), and two more
  singleton pools (Prophet, and as of 2026-09-30 Reaper and Lovers) using a team-specified odds table:
  - 5 players: 2 random draws, 70% Loyalist / 30% Chaos each.
  - 6 players: +1 guaranteed Chaos, then 2 random draws at 50/50.
  - 7 players: +1 guaranteed Chaos, +1 guaranteed Loyalist, then 2 random draws at 20% Loyalist / 80% Chaos.
  - 8+ players: same guarantees as 7, then as many random draws at 20/80 as needed to fill the table.
  - Draws are without replacement; a role pool that runs out drops to 0% and the other pool's odds fill the gap.
  - Prophet: flat 15% per draw, no guaranteed slot. Reaper: flat 8% per draw, only once a Queen has already
    been dealt this game. Lovers: flat 10% per draw, only at 7+ players with ≥2 undealt seats left — one
    draw assigns it to **two** seats at once. See `Game.DealRoles` and ruling #18 below.
  - **Deck doubles at 8+ players** (every `ActionCatalog` entry's count ×2) — see `ActionCatalog.BuildDeck(playerCount)`.
- **Deck:** 43 action cards total (confirmed by the team; the GDD's "50" was wrong/aspirational). Full list
  with counts in `Assets/Scripts/Core/ActionCards.cs` (`ActionCatalog.Entries`). When the draw pile empties,
  the discard pile is reshuffled into it (confirmed).
- **Round:** (1) Thronebearer names a Trusted Advisor. (2) Every other advisor — not the thronebearer, not
  the Trusted Advisor, who plays **no card** (confirmed) — plays one action card face down, all
  simultaneously (Elder plays two). Played cards are shuffled before the Trusted Advisor sees them, so seat
  order doesn't leak who played what. (3) Trusted Advisor picks 2 of those cards to pass to the King one at a
  time; the rest are discarded (unless a card effect routes everything straight to the thronebearer). (4) The
  thronebearer picks one (or two, if a card effect says so) to enact. (5) Hands refill to 3.
- **Roles and abilities** (see `Assets/Scripts/Core/Roles.cs` for text, `Game.cs AbilityPhase` for logic):
  King (always revealed, starts as thronebearer — **as of 2026-09-30 he carries no execution immunity,
  just another Loyalist**), Queen (while thronebearer: reveal an advisor; refreshes each time she newly
  takes the throne), Knight (once per game: execute an unrevealed player; if they were a loyalist, the
  Knight is executed too), Elder (play 2 cards/round once revealed, 1 while unrevealed), Court Wizard (once
  per game: force an advisor to play face-up), Jester (every round: force a random discard from a chosen
  player), Prince (once per game: swap seats with the thronebearer), Nobleman (once per game: take 1 random
  card each from 2 advisors), Prophet (once per game: become next round's trusted advisor), Beggar (no
  active ability — passive win-condition role, see below). **New 2026-09-30:** Reaper (once per game: Royal
  Execution — execute a revealed King, Prince or Queen specifically, their choice of which; only offered
  once one of the three is revealed), Lovers (no active ability
  — passive win-condition role, see below). **New 2026-10-01, replacing Martyr:** Medium (once per game, even
  while still unrevealed and without forcing a reveal: bring back one currently-dead player at that round's
  end — they live for 2 full rounds, then die again automatically, permanently; the Medium themselves never
  dies or gives anything up). All abilities require the player
  to already be revealed except the Elder (always active) and the Martyr (can act hidden).
- **Win conditions:**
  - Loyalists win when every currently Traitor-aligned player has been executed — Prince, Nobleman, the
    Prophet (if dealt), and a Beggar who has been **revealed** (changed 2026-09-29: previously a revealed
    Beggar did not count). A still-hidden Beggar is Loyalist-aligned and isn't required.
  - Traitors win when the Prince ends a round as thronebearer for **3 consecutive rounds** (changed from 2 on
    2026-09-27, per playtesting feedback). A Beggar who has **already been revealed** by that point shares
    the Traitors' win (confirmed).
  - **Jester wins only by being executed** (confirmed — NOT by the deck running out; there is no voting
    phase, "voted out" from the physical game's design doc is not implemented).
  - **New 2026-09-30, Reaper wins** when the King, Prince and Queen have all been executed (only possible in
    a game where a Reaper was dealt, which itself requires a Queen to have been dealt).
  - **New 2026-09-30, Lovers win instantly** the moment alive-count drops to `Game.LoversWinAliveThreshold`
    (5) with both Lovers still alive. If one Lover is executed, the other is executed too (a real execution
    — reveals role, still feeds the Jester/Traitor-wipe/Reaper checks).
  - Beggar: hidden = counts as Loyalist (wins/loses with them); revealed = counts as Traitor (wins/loses with
    them, and **does** count toward the Loyalists' "every traitor executed" requirement once revealed).
  - `Game.DidWin(player)` in `Assets/Scripts/Core/Game.cs` is the single source of truth for all of this —
    don't reimplement win-crediting logic elsewhere.
- **Illegal moves the engine must refuse** (confirmed requirement, tested): playing a card when it isn't your
  turn, playing a second card in the same round (Elder's second card is a separate legitimate question, not
  a "second" in the illegal sense), answering a stale/already-closed decision, submitting an out-of-range
  option index. See "Networking" below for exactly how this is enforced end-to-end.
- **Disconnects:** if a player's device drops or leaves, **a bot takes over their seat regardless of role**
  (no death, no kick, no special-casing the King or a Traitor). Confirmed requirement.
- **Bots:** optional, toggle in Settings/Lobby (`GameSettings.FillWithBots`, default on). Used to fill empty
  seats so a host can test/play alone, and to take over a disconnected human's seat.
- **Dice:** virtual (rolled by the engine, not physical). Cards that say "roll the die" roll one face per
  seat (`Game.RollSeat()`); landing on an empty seat does nothing.
- **Chat:** explicitly deferred. There's a `GameSettings.ChatEnabled` toggle in Settings that does nothing
  yet except remember the preference — no chat transport exists. Do not build chat unless asked; it wasn't
  scoped and the team's answer ("both same-room and remote") was ambiguous and never resolved.

### Rule gaps that were guesses, not confirmed by the team

These are documented in `Assets/Docs/RULES_ASSUMPTIONS.md` — read that file for the full list and reasoning.
Notable ones not superseded by later confirmations:
- Exactly what triggers each ability's availability window (assumed: must be revealed; Elder/King exempt).
- Whether the Knight/Wizard/etc. can target *any* player or only advisors — implemented as "any player" in
  most cases, advisors-only where the card text implies it. Check `Game.cs` switch statement per-card if this
  matters.
- The exact wording/behavior of a few of the rarer cards where the GDD's index and the physical card text
  didn't perfectly agree — `ActionCatalog.Entries` in `ActionCards.cs` is what actually ships; treat it as
  authoritative over any older doc.

## Architecture

### Core rules engine — `Assets/Scripts/Core/` (asmdef `KingsCourt.Core`, `noEngineReferences: true`)

Pure C#, zero UnityEngine dependency, so it compiles and runs outside Unity (used by `Tools/RulesSim`, a
standalone bot simulator) and is unit-testable without the Unity test runner.

- **`Game.cs`** — the entire rules engine. Host-authoritative: one `Game` instance is the only place rules
  execute. Async (`Task`-based) throughout so it can `await` a real human's answer instead of blocking a
  thread — this is what makes it usable both for instant bot-vs-bot simulation (bots resolve their Task
  synchronously) and for real networked humans (a `Task` that only completes when a phone's tap arrives).
  Key methods: `RunAsync()` / `PlayRoundAsync()` runs the whole game loop; `DidWin(Player)` is the win-credit
  oracle (see rules section); `Beat` is a `Func<int, Task>` pacing hook the UI sets to insert pauses between
  steps (bot sims leave it as a no-op).
- **`Player.cs`** — one seat's game state (hand, role, seat, alive/revealed flags). `EffectiveFaction`
  handles the Beggar's hidden/revealed split.
- **`Roles.cs`** — `Role` enum, `Faction` enum, `RoleInfo.AbilityText()` / `FactionOf()`.
- **`ActionCards.cs`** — `ActionEffect` enum (one per distinct card effect) and `ActionCatalog` (the 43-card
  deck with counts). This is the card list; don't hand-roll a deck elsewhere.
- **`CardInfo.cs`** — cosmetic-only mapping from `ActionEffect` to a short title and a `CardCategory` (for
  card coloring in the UI). Not gameplay logic.
- **`Agents.cs`** — the decision abstraction that makes bots and humans interchangeable to `Game`:
  - `IPlayerAgent` — `PickPlayer` / `PickCard` / `Confirm`, all `Task`-returning.
  - `RandomAgent` — a bot; resolves every question immediately with a random legal choice.
  - `SeatAgent` — **the important one for real players.** Wraps either a bot or a human seat. Exposes
    `Pending` (the current open `Decision`, or null), `Submit(decisionId, index)` → `SubmitResult`, and
    `BecomeBot()` (hands the seat to a bot mid-game, used for disconnects). `Submit` is the **single
    enforcement point** for "can't act out of turn / twice / on a stale question" — it checks the decision id
    matches the currently-open one and the index is in range, and returns a rejection reason string
    otherwise. Every legality guarantee in the game flows through this one method; do not add a second path
    for players to affect game state.
  - `Decision` — a serializable-ish description of one open question (kind, prompt, options as player ids or
    cards, per-option notes). This is what gets shown to a human (locally or sent to a phone).
- **`Snapshot.cs`** — `GameSnapshot` / `PlayerInfo` — a **per-viewer, hidden-info-safe** view of the game.
  `GameSnapshot.For(game, viewerId, seats, revealAll, logLines)` builds a snapshot where every other player's
  `Role` is `null` unless that player is revealed (or `revealAll` is set, i.e. spectator/game-over). **This is
  the only sanctioned way to read game state for display or network transmission** — never hand a `Game` or
  `Player` object to UI or network code directly, or you will leak hidden roles. Verified by
  `NetworkTests.SnapshotSurvivesJson` and `LegalityTests.WireRoundTrip` (see "Testing" below) that this
  invariant holds.
- **`Wire.cs`** — plain-field DTOs (`SnapshotDto`, `PlayerDto`, `CardDto`, `DecisionDto`) plus
  `Wire.ToDto(GameSnapshot)` / `Wire.FromDto(SnapshotDto)`. Exists because `JsonUtility` (used for
  networking, see below) can't serialize the richer `GameSnapshot`/`Decision` types directly (nullable
  `Role?`, interfaces, etc.). If you add a field to `GameSnapshot`/`Decision`, you must also add it here and
  to the DTO, or it silently won't reach the network — there is no compiler check for this, only the
  round-trip test.

### Unity layer — `Assets/Scripts/Unity/` (asmdef `KingsCourt.Unity`, references Core + Unity.InputSystem + UnityEngine.UI)

Everything here is built **in code**, not in the Unity scene editor — there are no hand-placed UI prefabs
except `Assets/Prefabs/CardView.prefab` (generated by an editor tool, see below). This was a deliberate
choice so the whole UI is diffable, greppable, and buildable headlessly; don't switch to scene-authored UI
for new screens without a strong reason, or you'll fragment the codebase into two authoring styles.

- **`UiKit.cs`** — the only place that touches raw `RectTransform`/`Image`/`Text`/`Button`/etc. construction.
  `Box`, `Label`, `MakeButton`, `Input`, `MakeToggle`, `VStack`/`HStack`, `Place`/`Stretch`/`Size` helpers.
  Colors (`UiKit.Gold`, `.Paper`, `.Button`, `.ButtonAlt`, `.Panel`, `.Night`, `.Ink`) are the house palette —
  reuse them rather than inventing new ones. `UiKit.Kill(obj)` is `Destroy`/`DestroyImmediate` depending on
  play-mode, needed because editor tools (screenshots) build UI outside Play mode.
- **`SpriteFactory.cs`** — every sprite in the game (role icons, pawn, card back, table felt, etc.) is drawn
  procedurally at runtime with hand-written pixel shapes (`Disc`, `Poly`, `Star`, etc.) rasterized into a
  `Texture2D`. **There is no imported art in this project.** If real art ever gets added, it would likely
  replace `SpriteFactory.Icon(Role)` etc. with actual sprite lookups — check call sites before assuming a
  quick swap works, some code (e.g. `TableView`) recolors/tints these sprites at runtime assuming they're
  flat-shape placeholders.
- **`CardView.cs`** — one card's visual (title, colored category header, effect text, face-down back,
  hover/selected lift-and-glow animation). Has a static `CardView.Prefab` — if set (by
  `Assets/Prefabs/CardView.prefab`, wired into `GameController.cardPrefab` in the scene), `CardView.Create()`
  instantiates the prefab instead of building one from scratch in code. Both paths must stay behaviorally
  identical; the prefab exists only so cards can be tweaked as a Unity asset later without needing to.
- **`TableView.cs`** — the top-down table: chairs, throne, draw/discard piles, one piece per player (grey
  pawn until revealed, then a role icon), thronebearer glow, trusted-advisor tag, executed-player graveyard
  column, candidate-highlight ring for "choose a player" decisions. Reads only from a `GameSnapshot` — never
  from `Game` directly.
- **`GameSession.cs`** — the **local/offline** game session: wraps a `Game` + its `SeatAgent`s, implements
  `IPlayerSession` (see below). Also owns `GameSettings` (a static class over `PlayerPrefs`, wrapped in
  try/catch since PlayerPrefs can throw in some WebGL contexts) — `FillWithBots`, `ChatEnabled`, `Speed`
  (pacing multiplier, 0=Relaxed/1=Normal/2=Fast, exposed as "Game speed" in Settings), `PlayerName`,
  `ServerUrl` (blank = auto-detect). And `Pacer` — turns `Beat(ms)` calls from `Game` into real (or
  zero-length, if `GameSettings.PaceMultiplier` is 0 — tests set this) delays without blocking a thread.
- **`GameController.cs`** — the single MonoBehaviour that bootstraps everything (camera, canvas, all
  screens) and is the hub all screens call back into for navigation (`ShowStart`, `HostGame`, `JoinGame`,
  `BeginGame`, `ChangeLobbySeats`, etc.) and for **what the lobby should currently show**
  (`GetLobbyInfo() → LobbyInfo`, which branches on host/offline/joined-client state). Put this on one empty
  GameObject in a scene; it builds the rest of the UI itself in `Init()`. Also does the responsive
  scaling trick (`CanvasScaler.matchWidthOrHeight` flips between 0 and 1 based on aspect ratio, so both
  desktop-wide and phone-in-landscape-with-browser-chrome layouts fit without clipping — this was a real bug
  found via emulator testing, see "Known issues fixed" below).
- **`GameScreen.cs`** — the in-game HUD: role panel, event log, Uno-style hand at the bottom, the modal
  decision panel (shows whichever of Play Card / Trusted Advisor pick / King enact / choose player / yes-no
  ability prompt is currently open), toast for refused moves, game-over panel. Rebuilds itself every frame
  from `session.Snapshot()` (cheap enough; don't over-optimize this without profiling). Tracks
  `answeredDecisionId` so a tapped choice doesn't get re-sent every frame while waiting for the host's next
  snapshot to arrive (this was a real bug — see "Known issues fixed").
- **`MenuScreens.cs`** — `StartScreen` (title, Host/Join/Help/Settings buttons, hosts the `CodePad`),
  `CodePad` (tap-in 4-letter join code UI — **typing into an `InputField` does not work in the WebGL
  build**, this is why the pad exists, see "Known issues fixed"), `LobbyInfo`/`LobbyScreen` (generic lobby
  view driven by `GameController.GetLobbyInfo()`, works identically for host/offline/joined-client),
  `SettingsPanel` (name, bots toggle, chat toggle (inert), speed, server address override).
- **`HelpPanel.cs`** — in-game rules reference (Round / Roles / Cards tabs), content generated directly from
  `RoleInfo`/`ActionCatalog` so it can't drift from the actual implemented rules.

### Networking — `Assets/Scripts/Unity/Net/`

**Design: host-authoritative over a thin message relay, no game logic on the server.** The host device runs
the real `Game`; the relay server only ferries opaque JSON strings between the host and each phone by a
4-letter room code. This means the server has no rules to keep in sync with the client and can't be tricked
into accepting an illegal move — legality is enforced once, in `SeatAgent.Submit`, on the host.

- **`Transport.cs` — `ITransport`.** Abstracts "a text-message duplex connection" so the same host/client
  code works in three contexts:
  - `ClientWebSocket`-based `WebSocketTransport` — used in the Editor and in a Standalone build.
  - A `UNITY_WEBGL && !UNITY_EDITOR` variant of the *same class name* (`WebSocketTransport`, picked by
    `#if`) that calls into `Assets/Plugins/WebGL/KcWebSocket.jslib` via `[DllImport("__Internal")]`, because
    browsers don't allow raw sockets and Unity's WebGL builds can't use `System.Net.WebSockets` — you have to
    go through the browser's own `WebSocket` object via a `.jslib` bridge. The bridge is **polled**
    (`KcWs_Next` returns queued event strings prefixed `O`/`M`/`E`/`C`) rather than callback-based, because
    Unity's WebGL IL2CPP build can't easily call back into C# from arbitrary JS timing — polling from
    `Tick()` (called every `Update()`) sidesteps that entirely.
  - `LoopbackTransport` (test-only, in `Assets/Tests/PlayMode/LoopbackHub.cs`) — an in-memory stand-in for
    the whole client-server round trip, used so most multiplayer tests run in milliseconds with no real
    sockets or process spawning.
  - Also exposes `WebSocketTransport.DefaultUrl()` (derives `ws(s)://<page host>/ws` from `location.host` in
    WebGL, or `ws://localhost:8000/ws` in editor/standalone), `QueryParam(name)` (reads `?join=CODE` from the
    page URL) and `PageOrigin()` (for building a shareable `http://host:port/?join=CODE` link) — all via the
    same jslib bridge, with editor/standalone stubs returning `""`.
- **`NetMessages.cs`** — the wire protocol. `RelayMsg` is the outer envelope the *server* understands
  (`t`: "host"/"join"/"hosted"/"joined"/"error"/"peer_joined"/"peer_left"/"msg"/"to"/"all"/"closed" — mirror
  of `Server/server.js`'s switch statement, **keep these two in sync manually**, there's no shared schema).
  Inside a `"msg"`/`"to"`/`"all"` envelope's `data` field is a second layer of JSON, the **game-level**
  messages the host and phones exchange: `HelloMsg` (phone → host, sets display name), `SubmitMsg` (phone →
  host, a decision answer), `LobbyMsg` (host → phones, full member list + settings), `StartMsg` (host → one
  phone, "you are seat N, game is starting"), `SnapMsg` (host → one phone, wraps a `SnapshotDto`),
  `RefusedMsg`/`DeniedMsg` (host → phone, rejection reasons). `Json.Kind(data)` peeks the `"k"` field to
  figure out which type to `JsonUtility.FromJson` into next — this two-layer envelope exists because
  `JsonUtility` needs a concrete type up front and can't do polymorphic/tagged-union deserialization itself.
  `IPlayerSession` is the interface that makes a local `GameSession` and a networked `RemoteSession` look
  identical to `GameScreen`/`TableView` — **all UI code should depend on `IPlayerSession`, never on
  `GameSession` or `RemoteSession` directly**, or you'll break the "same UI code path for local and online
  play" property that made testing tractable.
- **`NetHost.cs`** — runs on the device that pressed "Host Game". Opens a room (`t:"host"` → waits for
  `t:"hosted"` with a code), tracks `Member`s (relay id, display name, assigned seat/`PlayerId`, online
  flag), broadcasts `LobbyMsg` on any lobby change, and on Start (`StartOnline`) creates the real
  `GameSession`, assigns each joined phone to a seat (`GameSession.AssignHuman`), and from then on:
  every ~80ms (`SendSnapshots`, throttled by `dirty`/`lastSend`) pushes each online human player their own
  filtered `GameSnapshot` (never a shared one — each phone gets a separately-built snapshot so hidden info
  never crosses players); and on any incoming `SubmitMsg` from a phone, calls
  `Seats[member.PlayerId].Submit(...)` (the same legality-enforcing method local play uses) and relays a
  `RefusedMsg` back if it was rejected. On `peer_left`, if the game has started, calls `Seats[...].BecomeBot()`
  (this is where "a bot takes over a disconnected player's seat" is implemented) rather than ending the game.
  `UniqueName(wanted, exceptMember)` de-duplicates display names against the host's own name and all other
  members (added during the audit pass — two phones with the same default name used to collide silently).
- **`NetClient.cs`** — runs on a joining phone. `NetClient` handles pre-game state (connecting → joining →
  in-lobby → denied/failed/closed), then on `t:"start"` constructs a `RemoteSession` (which implements
  `IPlayerSession`) and starts feeding it `SnapMsg`s. `RemoteSession.Submit` just forwards the tap to the
  host and trusts the host's later `RefusedMsg`/next snapshot to reflect whether it worked — **the client
  never enforces legality itself**, by design; it also throttles so tapping doesn't resend the same
  `decisionId` twice before the host replies (`lastSentDecision`, added during the audit — this was
  previously a bug where rapid double-taps or a stalled `dirty` flag on the host could cause a submit to fire
  more than once for one decision). `MyId` is the relay-assigned id, used so a joined phone can find its own
  row in the `LobbyMsg` member list without relying on name-matching (also an audit fix — name-matching broke
  as soon as two players shared a name).
- **`Assets/Plugins/WebGL/KcWebSocket.jslib`** — the actual browser `WebSocket` calls (`KcWs_Open/Send/Next/
  Free/Close/DefaultUrl/Query/Origin`). Edit this only if you understand Emscripten's `mergeInto`/`_malloc`/
  `UTF8ToString`/`stringToUFT8` conventions — it's easy to leak the malloc'd strings `KcWs_Next` returns if
  you don't call `KcWs_Free` on every non-zero pointer it hands back (the C# side already does this
  correctly in `Transport.cs`, follow that pattern for any new calls you add).

### Relay server — `Server/` (plain Node.js, not part of the Unity project)

- **`server.js`** — `http.createServer` serves static files from `../Builds/WebGL` (or a path given as
  `argv[2]`) with correct MIME types, plus a `ws` `WebSocketServer` on `/ws` implementing the `RelayMsg`
  protocol described above (`rooms: Map<code, {host, peers: Map<id, ws>, nextId}>`). No game logic at all —
  it doesn't parse the inner game messages, just forwards `data` verbatim. Includes: room-code generation
  (`AEIOU`-free-ish `LETTERS` set to avoid ambiguous codes... actually check the exact excluded letters in
  the file, it excludes visually-confusable ones), a max-peers cap (`MAX_PEERS = 11`), a per-connection
  **flood guard** (`ws.win` sliding 1-second window, drops messages past 60/sec — added during the audit
  pass after realizing nothing stopped a buggy/malicious client from hammering the host), dead-connection
  reaping via ping/pong every 20s, and `/health` (`{ok, rooms}`) for a quick liveness check. Prints every
  non-internal IPv4 address it's listening on at startup so you can tell people which URL to open.
- **`package.json`** — one dependency, `ws`. `npm start` = `node server.js`.
- **`test.js`** — protocol-level tests using real `ws` clients against a real (ephemeral-port) instance of
  the server: room codes, unknown-code rejection, distinct peer ids, lobby notifications, message relay both
  directions, peers can't impersonate the host, **flood throttling and recovery after the window resets**,
  leave/close notifications, room cleanup. `node test.js` — currently **all pass** (13 checks). Run this after
  *any* change to `server.js`.
- **`README.md`** — run/test instructions for a human (or a future Claude) who hasn't seen this file:
  `npm install` once, then `node server.js`; firewall note (Windows will prompt for Node.js on first run —
  must allow **Private networks** or phones on the same Wi-Fi can't reach it).
- Not committed: `node_modules/` (gitignored — see `.gitignore` additions under "Relay server").

## Editor tooling — `Assets/Editor/`

- **`ScreenshotTool.cs`** — renders every screen (start, lobby, settings, help x3, in-game for each decision
  kind, game-over, role-piece legend) to PNGs **without entering Play mode**, by directly constructing a
  `GameController` in a fresh empty scene, calling its navigation methods, and rendering its camera to a
  `RenderTexture`. Menu: `King's Court → Capture Screenshots`. Also runnable headlessly:
  `Unity.exe -batchmode -quit -projectPath . -executeMethod KingsCourt.EditorTools.ScreenshotTool.Capture -shots <dir>`.
  Has a `ValidateScene` method too (`-executeMethod KingsCourt.EditorTools.ScreenshotTool.ValidateScene`) that
  just opens `KingsCourtTable.unity`, checks a `GameController` exists and its card prefab is wired, and exits
  with a pass/fail code — a fast sanity check that doesn't require running tests.
- **`CreatePrefabs.cs`** — `King's Court → Create Card Prefab`, (re)generates
  `Assets/Prefabs/CardView.prefab` from the in-code `CardView.Create()` construction. Run this if you change
  `CardView.Build()` and want the prefab to match (nothing currently auto-syncs them — they can drift).
- **`WebBuild.cs`** — `King's Court → Build WebGL`, or headlessly:
  `Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod KingsCourt.EditorTools.WebBuild.Build -buildOutput <dir>`.
  Forces `WebGLCompressionFormat.Disabled` (so the output is plain files any static server can host — no
  need for the server to handle Brotli/gzip content-encoding) and sets product name / default resolution.
  Logs `WEBBUILD result=... size=... errors=... time=...` and exits with a matching process exit code when run
  in batch mode, so a calling script can tell success from failure without parsing full Unity logs.

## Testing

Two independent layers — **run both after any change that touches rules, networking, or the UI's data
flow.** Neither is a substitute for the other: RulesSim is fast (~seconds, thousands of games) and catches
rules/logic regressions; the Unity PlayMode tests are slower but exercise the actual UI, the actual
networking stack (including a real Node process and real sockets for one test), and the actual scene.

### `Tools/RulesSim/` — standalone C# bot simulator (no Unity, no NUnit)

Compiles the `Assets/Scripts/Core` files directly with Roslyn (no `dotnet` SDK is installed on this machine
— see "Environment quirks" — so it's compiled manually against Unity's bundled `csc.dll` and the local
`Microsoft.NETCore.App` shared runtime; there's a helper script for this, see below). Modes (all via
`Program.cs`'s `args[0]` dispatch):
- **`RulesSim <n>`** (default 2000) — runs `n` bot-only games at each of 5/6/7/8 players, verifies no card
  leaks (`Game.CountCards() == ActionCatalog.TotalCards` at all times) and the King never dies, prints
  per-player-count win rates and average round count.
- **`RulesSim legality <n>`** (default 300) — the illegal-move enforcement test: scripted "human" `SeatAgent`s
  that repeatedly try to act out of turn, resubmit a stale decision, submit out-of-range indices, and
  occasionally `BecomeBot()` mid-game (simulating a disconnect); asserts every illegal attempt is rejected
  with a sane reason and the game still completes normally. Also runs `WireRoundTrip()` (in
  `LegalityTests.cs`) — plays real games and checks that `Wire.ToDto`→`Wire.FromDto` round-trips exactly
  and that no snapshot for a given viewer ever contains another player's hidden role.
- **`RulesSim report <n>`** (default 1000, see `Report.cs`) — the "how often does each role win, how long
  does a real game take" analysis: role win-rate breakdown, round-count distribution (mean/median/90th
  percentile/max), and an estimated real-world play time at Fast/Normal/Slow table paces (guessed
  seconds-per-phase constants, documented in the file) plus a fixed 3-minute setup allowance.

**How to build/run it** (there's no `.csproj` checked in — `RulesSim.csproj` was created ad hoc during
development and is gitignored; recreate it if it's missing, or use the manual `csc` invocation):
```
# from repo root, roughly (adjust Unity version path if it changed):
U="C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Data"     # any installed Unity's bundled Roslyn works
R="C:/Program Files/dotnet/shared/Microsoft.NETCore.App/8.0.14"   # match whatever version is on this machine
# build a response file listing all Core + RulesSim .cs files and the needed System.*.dll references,
# then: dotnet "$U/DotNetSdkRoslyn/csc.dll" @that_response_file
# then: dotnet <output.dll> [args as above]
```
This is fiddly on purpose-avoidance — if the `dotnet` SDK ever gets installed on this machine, just write a
normal `RulesSim.csproj` targeting `net8.0` and use `dotnet run` instead; there is no reason to keep doing it
the hard way once a real SDK exists.

### `Assets/Tests/PlayMode/` — Unity NUnit PlayMode tests (asmdef `KingsCourt.PlayModeTests`)

Run via `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <path>.xml -logFile <path>.log`
(**must be run with the user's own Unity editor closed** — see "Environment quirks", there's a project-lock
and a port-clash issue otherwise). All 10 currently pass.

- **`GameFlowTests.cs`** — drives the *actual UI* (`ExecuteEvents.Execute(..., pointerClickHandler)` on real
  `Button`/`CardView` GameObjects) through a complete game twice: once against a `GameController` built fresh
  in code, once against the actual `Assets/Scenes/KingsCourtTable.unity` scene (checking the scene's wired-up
  `CardView.Prefab` is used). Also verifies that clicking a card out of turn produces a visible toast/refusal.
  Sets `GameSettings.PaceMultiplier = 0` so the whole game finishes in ~1 second instead of real-time.
- **`LoopbackHub.cs`** — the in-memory relay stand-in (`LoopbackHub` + `LoopbackTransport`) described under
  "Networking" above. Not a test file itself, just test infrastructure.
- **`NetworkTests.cs`** — the multiplayer test suite:
  - `ThreePhonesPlayAWholeGameTogether` — host + 2 loopback "phones" play a complete game; checks each
    phone's snapshot has its own 3-card hand, its own role, exactly `seats` players, and **no other player's
    hidden role**; checks names propagate correctly into the actual `Game.Players`.
  - `HostRefusesIllegalMovesFromPhones` — a phone submitting out-of-turn or with a stale decision id gets a
    `Refused` event with a sane message.
  - `ADisconnectedPhoneIsReplacedByABotAndTheGameCarriesOn` — `client.Leave()` mid-game → that seat becomes a
    bot (`SeatAgent.IsBot == true`), the game finishes, and the *other* phone's snapshot shows the departed
    seat as a bot.
  - `PhonesAreToldWhenTheHostLeaves` — `host.Close()` → phones get an `Ended` event and the room is removed
    from the hub.
  - `JoiningWithABadCodeOrAFullGameIsRefused` — unknown code → `Failed` with the right error text; a phone
    joining after the seat count is already full → `Denied`.
  - `PhonesWithTheSameNameGetUniqueNamesAndFindTheirOwnRow` — the audit-fix regression test for the
    duplicate-name and `MyId`-based row-lookup bugs.
  - `RealRelayServer_HostAndTwoPhonesPlayAWholeGame` — a `[UnityTest]` coroutine that actually **spawns
    `node Server/server.js` as a child process** on a random high port, waits for it to accept TCP
    connections, then runs a real host + 2 real `WebSocketTransport` clients (not loopback) through a
    complete game over real sockets, checks both phones see the correct final result, then tests a phone
    leaving (host is told) and the host closing (the other phone is told). Self-skips
    (`Assert.Ignore(...)`) if `Server/node_modules` doesn't exist (i.e. `npm install` hasn't been run) or
    `node` isn't on PATH, so it degrades gracefully rather than failing on a machine without Node set up.
    Sets `Application.targetFrameRate = 1000` and disables vsync for the duration so the test doesn't
    artificially wait on frame pacing while polling the sockets. Allows up to 150 real seconds — a full game
    at max unthrottled speed over real (loopback) sockets took ~7 seconds / 23 rounds when last measured;
    the generous ceiling is there because CI-style machines under load can be much slower, not because the
    game is normally slow.
  - `SnapshotSurvivesJson` — the narrower, non-networked version of the hidden-info/round-trip check (games
    a `Game` directly, no host/client wrapper).

**When adding a new networked feature**, add a test to `NetworkTests.cs` in the loopback style first (fast,
deterministic), and only add real-socket coverage in `RealRelayServer_...` if the loopback stand-in can't
actually exercise what you're changing (e.g. anything about the JSON wire format itself, or server-side
behavior like the flood guard, needs the real server).

## Known issues found and fixed during development (don't reintroduce these)

1. **Typing into `InputField` didn't work for the join code in the WebGL build** (worked fine in editor).
   Root-caused to browser/Unity WebGL text-input focus quirks that weren't worth chasing further. **Fixed**
   by replacing the text field with `CodePad`, a tap-the-letter-tiles UI, plus a `/?join=CODE` shareable-link
   auto-join path as a second way in that needs no typing at all. If a future feature needs free-text input
   from a phone (e.g. a chat box), budget time to specifically verify it works in an actual WebGL build in an
   actual browser — don't assume `InputField` "just works" there.
2. **Screen scaling clipped buttons on phone-aspect-ratio screens** (e.g. Settings button cut off, lobby rows
   overlapping) when tested on an Android emulator. **Fixed** in `GameController.Tick()`:
   `CanvasScaler.matchWidthOrHeight` is now flipped live between 0 (match width) and 1 (match height) based
   on whether the current aspect ratio is wider or narrower than 16:9, instead of being a fixed 0.5.
3. **A tapped decision could be re-sent multiple times** before the host's next snapshot arrived (visible as
   double-plays or "why did it pick that" mismatches under lag). **Fixed** in two places:
   `GameScreen.answeredDecisionId` (client-side: don't let the player interact with a decision they already
   answered until a *new* decision id shows up in the next snapshot) and
   `RemoteSession.lastSentDecision` (network-layer: never send the same `decisionId` to the host twice, even
   if the UI layer above it somehow tries to).
4. **Two players with the same default name** ("Player 1") collided in the lobby member list, and a joining
   phone couldn't find its own row (was doing `startsWith(GameSettings.PlayerName)` string matching, which
   breaks the moment names aren't unique or a phone's own name differs slightly from what the host sees).
   **Fixed:** `NetHost.UniqueName()` de-duplicates against the host's name and all current members (appends
   " (2)", " (3)", ...); `NetClient.MyId` (the relay-assigned connection id) is now used for self-lookup in
   the lobby list instead of name matching.
5. **Nothing stopped a buggy or malicious phone from flooding the host** with messages. **Fixed** with a
   sliding-window rate limiter in `server.js` (max 60 messages/second per connection, silently dropped past
   that — the phone isn't kicked, just throttled, and traffic resumes normally once the window rolls over).
6. **One WebGL build crashed at the final `wasm-emscripten-finalize` linking step** with a Windows access
   violation. It has **not recurred** across several subsequent builds with no code changes to the build
   pipeline, and no root cause was found (Unity's own crash-report folder was empty both times). Treat it as
   a possible flaky/environmental issue with this specific Emscripten toolchain install rather than a bug in
   this project's code — if it recurs, worth checking whether another process was contending for the same
   temp files/ports at the time (a previous Unity batch process was still shutting down in at least one
   occurrence).

## Known issues — open, NOT yet fixed

1. **"Back to lobby, same room code" did not work in the user's own phone+laptop playtest on 2026-09-27**,
   despite passing the automated network tests (`NetworkTests.cs`, including a real-socket relay-server
   test) before this feedback came in. The user reported it plainly failed without further detail (no
   error message, screenshot, or repro steps captured yet). Automated coverage passing does not mean this
   is fixed in practice — the gap is real, just not yet diagnosed. **Next step: get exact repro details
   from the user** (host or phone side? did the phone get stuck on the game-over screen, snap back to the
   main menu instead, silently do nothing, or error? single phone or multiple? offline bots or a real
   relay server?) before attempting a fix blind. Relevant code: `GameController.ReturnToLobby()`,
   `NetHost.ReturnToLobby()`, `NetClient`'s `"backlobby"` handling in `NetHost.cs`/`NetClient.cs`.
2. **Unity WebGL `InputField` text entry does not actually work in a real browser** — confirmed
   2026-09-29 for both the pre-existing Settings "Your name" field and a newly-added lobby "Your name"
   field (added to let a QR-joined phone rename without leaving/rejoining, see rulings log). Clicking the
   field focuses and selects its placeholder text correctly, but typed keystrokes never reach it — this is
   the exact same underlying issue `Assets/Docs/RULES_ASSUMPTIONS.md` and this file's "Known issues fixed"
   section already documented for the join-code field (worked around there with `CodePad`, a tap-the-letter
   UI, instead of fixing typing itself). It was never actually fixed for regular text fields, just avoided.
   **Not fixed this session** — the real fix is a DOM `<input>` element overlaid on the canvas and bridged
   to C# via a `.jslib` plugin (the same pattern `KcWebSocket.jslib` already uses for other browser APIs),
   which is a real sub-project (position/size sync as the canvas resizes, focus/blur, mobile keyboard
   behavior) rather than a quick fix — needs the user's go-ahead before starting it.

## What has been verified to actually work (as of 2026-09-26/27)

- Full game rules, bot simulation at all player counts (thousands of games, no crashes/leaks) — via
  RulesSim. Originally 9 roles/43 card effects as of 2026-09-26/27 (this section's date); now 10
  roles/29 distinct card effects across 45 cards after the Prophet role and Mead card were added
  2026-09-29 — see the rulings log for what changed and when.
- Illegal-move rejection (out of turn, double-play, stale decision, out-of-range index) — via RulesSim
  legality mode and `NetworkTests.HostRefusesIllegalMovesFromPhones`.
- A disconnecting player's seat becomes a bot and the game completes normally — via RulesSim legality mode's
  random `BecomeBot()` calls and `NetworkTests.ADisconnectedPhoneIsReplacedByABotAndTheGameCarriesOn`.
- Hidden-information safety: no snapshot sent to any viewer (local or networked) ever contains another
  player's unrevealed role — via `WireRoundTrip()` and `NetworkTests.ThreePhonesPlayAWholeGameTogether`/
  `SnapshotSurvivesJson`.
- The actual UI (buttons, hand, decision panel) drives a complete game via simulated clicks, both against
  code-built UI and against the checked-in scene — via `GameFlowTests`.
- A real host + 2 real phones (as separate `WebSocketTransport` clients) playing a complete 23-round game
  over **real sockets through the real Node relay server** — via
  `NetworkTests.RealRelayServer_HostAndTwoPhonesPlayAWholeGame`.
- **Manually, in an actual browser** (this app's built-in Browser pane, two tabs): hosting created a real
  room code from the running server; a second tab joined via the tap-in `CodePad`; the host's lobby showed
  the new member with a de-duplicated name; starting the game gave each tab its own distinct role/hand and
  decisions; a card played on one tab correctly updated the other tab's view of the game; a
  `/?join=CODE` link auto-joined with no typing; a third tab attempting to join after the game started was
  correctly denied with "That game has already started."
- The WebGL build succeeds reliably (several consecutive successful builds after the one crash mentioned
  above), runs correctly when served by `Server/server.js`, and adapts its layout across aspect ratios.

## What has NOT been verified / is not built

- **Physical prototype note (low priority, not a code task):** the user flagged 2026-09-29 that the
  physical chair design (`Chair_Bottom.stl`/`Chair_Top.stl`, `Boardgame Design/Lords_Chair*.stl`) could use
  remodeling. Explicitly not a priority — logged here so it isn't lost, not scheduled.
- **A real physical phone joining over real Wi-Fi has never been tested.** Everything above used browser
  tabs on the same machine as the server. The server is confirmed running and reachable at
  `http://172.24.123.250:8000` (this PC's Wi-Fi IP as of last check — **will change** if the PC reconnects
  or DHCP reassigns; re-check with `ipconfig`/the server's own startup log, which prints every non-internal
  IPv4 it's bound to) but nobody has opened that URL from an actual phone. The main open risk here is the
  **Windows Firewall prompt for `node.exe`** — it must be allowed on "Private networks" or phones on the
  same Wi-Fi won't be able to reach the server at all. This should be the very next thing tested with real
  devices.
- **5+ simultaneous human players has never been tested**, real or in a browser — only up to 2 phones + 1
  host (3 total) has been exercised end-to-end. The `RealRelayServer_...` test only spins up 2 clients; the
  loopback tests go up to 2 as well. Nothing is known to break with more, but nothing has proven it either —
  worth adding a 4-6-phone loopback test before assuming it scales without incident.
- **An Android APK / native mobile build has not been attempted for this networking feature.** Earlier in
  the project (before online multiplayer existed) there was exploration of Android Build Support as an
  alternative to WebGL — that module may or may not still be installed (check
  `C:\Users\17143\UnityEditors\6000.6.1f1\Editor\Data\PlaybackEngines\` for an `AndroidPlayer` folder). If
  the team wants a real installable app instead of "open this URL in your phone's browser," that's a
  meaningfully different effort (Android SDK/NDK, signing, an actual app to distribute) that hasn't been
  scoped.
- **Chat is not implemented** (see rules section — deliberately deferred, toggle exists but does nothing).
- **Voice, spectator mode, reconnection with game-state resume after a full page reload, and any persistence
  beyond one running process's lifetime are all unbuilt.** If a phone's tab is closed and reopened mid-game,
  the current behavior is: it's treated as a fresh page load, which will NOT rejoin the in-progress game —
  it'll go to the Start screen. `SeatAgent.BecomeBot()` only fires on the relay's `peer_left` event (a clean
  or timed-out disconnect from the *server's* point of view, not from re-navigating the page); a page
  refresh's exact behavior (whether the browser's WebSocket close fires before the new page loads, whether
  it looks like a clean disconnect to the relay) has not been specifically tested and may or may not trigger
  the bot takeover promptly.
- **No load testing, no test of what happens if the relay server itself crashes or restarts mid-game**
  (every in-progress room would presumably be lost — there's no persistence of room/game state outside the
  host's own process memory, and the host's own `GameSession` lives only in that browser tab's memory too,
  so **closing the host's tab or losing the host's connection ends the game for everyone**, by design per
  "phones are told when the host leaves" — but there's no "host migration" or "someone else becomes host" 
  fallback, if that's ever wanted it's a real feature, not a bug fix).

## Environment quirks specific to this machine (read before running anything)

- **Unity is installed at `C:\Users\17143\UnityEditors\6000.6.1f1\Editor\Unity.exe`, NOT under
  `C:\Program Files`.** Installing into Program Files via Unity Hub's headless/CLI install failed
  repeatedly with a UAC/error-1223 problem specific to non-interactive installs; moving Hub's default
  install location fixed it. If a fresh Unity version ever needs installing, expect the same issue and the
  same fix (change Hub's install path in its own preferences, or install via the interactive Hub GUI where
  the UAC prompt can actually be answered).
- **A second, older Unity (6000.3.15f1) is also installed**, in the normal `C:\Program Files\Unity\Hub\...`
  location. This was used early on as a disposable scratch-project Unity for rendering screenshots/running
  quick checks without disturbing the user's real open editor — **if you use it, be aware it's a different
  (older) Unity version than the project's pinned 6000.6.1f1**, so anything that depends on 6000.6-specific
  APIs won't work there. Prefer running everything against 6000.6.1f1 now that the project is far enough
  along that version drift matters more than editor-contention convenience.
- **Unity batch-mode commands (tests, builds) must be run with the user's own interactive Unity editor
  CLOSED.** Two problems if it's open: (1) a hard project lock — a second Unity process can't open the same
  project; (2) even semi-independent operations (like `BuildReportRestService`, an internal Unity HTTP
  service) can crash with a port conflict if another Unity process touching the same project is still
  around. The established pattern: close the user's editor (gracefully — `Get-Process Unity | ...
  CloseMainWindow()`, wait, confirm it exited, don't force-kill unless graceful close fails), run the
  batch operation, reopen the editor for the user afterward (`-openfile` to the scene they were likely
  looking at) so they aren't left staring at nothing. **Do not repeatedly launch/close Unity or Unity Hub in
  rapid succession** — this visibly annoyed the user earlier in the project ("you keep closing and reopening
  unity, stop") when multiple background attempts each independently tried to relaunch it. Batch one
  editor-close, do everything that needs the closed editor, then one reopen.
- **No `dotnet` SDK is installed**, only the `dotnet` *runtime* bundled inside Unity's own installation
  (`Editor\Data\DotNetSdk\dotnet.exe`, and separately a `Microsoft.NETCore.App` shared runtime under
  `C:\Program Files\dotnet\shared\`). This is why `Tools/RulesSim` is compiled by hand with Unity's bundled
  Roslyn (`csc.dll`) against explicit `System.*.dll` references rather than via a normal `dotnet build` —
  see the RulesSim section above for the exact invocation shape. If a real SDK ever gets installed, simplify
  this immediately; the manual compile approach is a workaround, not a preference.
- **This machine's shell is Git Bash (MINGW), invoked via a `Bash` tool that runs through PowerShell/cmd
  underneath in some contexts.** A few observed footguns: (1) some heredoc constructs containing certain
  quote characters silently fail to write anything (no error surfaces) — when a `cat <<EOF` write doesn't
  seem to have taken effect, verify with `ls`/`cat` rather than trusting exit code 0, and fall back to the
  dedicated file-Write tool for anything nontrivial; (2) Windows paths with spaces (`C:\Program Files\...`)
  need care in response-file/argument construction for tools like `csc` — quote consistently and prefer
  building an `@response.rsp` file over long inline argument lists; (3) `adb reverse tcp:8000 tcp:8000` is
  what makes an Android emulator able to reach a server running on `localhost:8000` on the host PC (the
  emulator's own "host" alias, `10.0.2.2`, works too but was less reliable for WebSocket upgrade requests in
  testing — `adb reverse` was the fix that actually got the emulator's Chrome loading the game reliably).
- **Server's Wi-Fi-visible address will change if this PC's network changes** (`172.24.123.250` as of last
  check, printed fresh every time `node server.js` starts — always re-read it from that output rather than
  hardcoding it anywhere in code or docs).

## Team rulings log (chronological, for provenance — see also `Assets/Docs/RULES_ASSUMPTIONS.md`)

Kept here so the reasoning/order of decisions isn't lost even though the *current* rules are summarized
above. If a future ruling contradicts one of these, this log plus git blame on
`Assets/Docs/RULES_ASSUMPTIONS.md` is how to reconstruct "wait, why did we do it that way before":

1. Deck confirmed at 43 cards (not the GDD's 50); Trusted Advisor plays no card; Jester wins only by
   execution (never by deck depletion; no voting phase exists in this digital version).
2. Reshuffle-on-empty-deck confirmed.
3. Beggar: hidden = Loyalist (wins with Loyalists if they win while Beggar is still hidden); revealed =
   Traitor (wins with Traitors if they win after Beggar is revealed).
4. Role-count-by-player-count odds table confirmed exactly as implemented (see rules section); 8 players
   draws 3 random roles (not 2) after the guaranteed King/Prince/Nobleman/+1Chaos/+1Loyalist.
5. Disconnect handling confirmed: bot takes over regardless of role, always, no exceptions, no kick option.
6. UI/UX requirements given as a batch: start screen, real multiplayer joining from phones, a card UI
   prefab, a help panel, illegal-move refusal with a reason shown to the player, Uno-style hand at the
   bottom of the screen, a visible "this card is selected" indicator, and the screen flow
   Start → Waiting Lobby → your own individual table view. All of the above are now built.
7. Follow-up specifics: everyone gets the same top-down table view (no special host-only "big screen" view);
   networking should use a real online layer (Unity Relay was the original suggestion — implemented instead
   as a custom Node relay server, functionally equivalent for this game's needs and avoiding a Unity-account
   dependency, see "Networking" section); a disconnected/executed distinction doesn't apply — a
   Jester who disconnects does not count as "the Jester won by dying," only an actual in-game execution
   counts; no kick feature; dice are simulated in-engine; bots are optional via a toggle; illegal-move
   cases explicitly named were "played a card when it wasn't their turn" and "tried to play a second card" —
   both are refused with an explanation.
8. Phone-app-vs-browser was never explicitly answered by the team; **WebGL browser build was chosen** as the
   pragmatic default (works on any phone with no install step, no app store, no per-platform build
   maintenance) — if the team later says they want a real installable app, that's the open question to
   revisit, not a settled "no."
9. **(2026-09-27, from live playtesting with a phone + laptop bots)** Two rules changes requested by Chris:
   - Prince and Nobleman (the two Traitors) now always see each other's role, from the start of the game,
     regardless of reveal state — implemented as a targeted exception in `GameSnapshot.For`
     (`Snapshot.cs`, `IsTraitorTeam`), not a general hidden-info leak. The Beggar's hidden/revealed
     traitor ambiguity is untouched.
   - At 4 players alive (thronebearer + trusted advisor + only 2 other advisors), only 2 cards get played
     each round, leaving the trusted advisor no real choice when filtering down to 2. The engine now draws
     the top card of the deck as a 3rd option whenever exactly 2 cards are in play (and the round isn't a
     roundAllToThronebearer effect) — see `Game.PlayRoundAsync`. Both changes are also logged in
     `Assets/Docs/RULES_ASSUMPTIONS.md`.
10. **(2026-09-27, playtest round 2)** Three more fixes from real phone+laptop testing:
    - **Bug fix:** the Prince/Nobleman visibility change in #9 only fixed `GameSnapshot` — `TableView.cs`
      still gated the role icon/name on `p.Revealed || p.IsYou`, so a Nobleman still saw a blank grey pawn
      for the Prince. Fixed: the private "badge" + `(RoleName)` label now show for any `p.Role.HasValue`
      case, without changing the public `full`-reveal flag (so nobody else at the table sees anything new).
    - **New rule (requested):** the thronebearer can't name the same trusted advisor two rounds running,
      except when the next trusted advisor was forced by a "die decides" card effect (that case still
      allows a repeat, by design). Falls back to allowing a repeat only if literally nobody else is
      eligible. See `Game.PlayRoundAsync` (`lastTrustedId`).
    - **Networking fix:** joining while a game was already running used to be denied outright ("already
      started"). Now the room accepts joins any time there's a free seat; joining mid-game waits in the
      lobby (`LobbyMsg.inProgress` tells the phone why) and is auto-seated in the next game once the host
      returns everyone to the lobby and starts again. A disconnected player's room slot is now freed
      immediately instead of being kept forever as a dead entry, so they (or anyone) can get back in. See
      `NetHost.cs`, `NetworkTests.APhoneCanRejoinTheLobbyMidGameAndIsSeatedInTheNextOne`.
11. **(2026-09-27, playtest round 3)** Prince win condition changed from 2 consecutive rounds as
    thronebearer to **3 consecutive rounds** (requested after playtesting; makes the Traitors' win harder).
    Updated `Game.cs` (`princeStreak >= 3`), `HelpPanel.cs`'s in-game rules text, and
    `Assets/Docs/RULES_ASSUMPTIONS.md`. Verified via RulesSim (no crashes, card-count invariant holds,
    Traitor win rate dropped as expected with the harder condition). **Also reported the same session:**
    "back to lobby" (item #10's networking fix) did not work in the user's own phone+laptop test, despite
    passing automated coverage beforehand — see "Known issues — open, NOT yet fixed" above. Not
    investigated yet; the user asked to move on to other gameplay items first.
12. **(2026-09-27, playtest round 4)** Card size and the "blurry on PC, sharp on phone" text bug:
    - Card size bumped from 170x244 to 196x280 (`CardView.Width`/`Height`) — "a little bigger," per the
      user. Regenerated `Assets/Prefabs/CardView.prefab` to match (the two card-building paths drift if
      you change `CardView.Build()` without regenerating it — see the Editor tooling section above).
    - The "blurry on PC" bug's real cause: Unity's stock WebGL template renders desktop browsers into a
      small fixed-size box (was 1280x720) instead of filling the browser window — only phones got a
      fill-the-viewport treatment. That fixed box meant every pixel of UI, including card text, was
      rendered at a much lower effective density on a PC than on a phone filling its real screen. Fixed
      with a custom WebGL template, `Assets/WebGLTemplates/KingsCourt/` (copied from Unity's built-in
      "Default" template, with the per-device branching removed so every device always fills the
      viewport), wired in via `PlayerSettings.WebGL.template = "PROJECT:KingsCourt"` in `WebBuild.cs`.
      Confirmed the canvas backing resolution now tracks window size (measured directly via JS in a real
      browser, before/after resizing) instead of staying capped, and confirmed visually in a live
      browser game. **User confirmed fixed on both PC and phone after this build.**
13. **(2026-09-27, same session)** The "How to Play" text-clipping bug (first "fixed" earlier this
    session — that fix was wrong; corrected here). The actual, confirmed-by-direct-measurement cause:
    `HelpPanel.cs`'s scrollable `content` RectTransform was left at Unity's default `sizeDelta = (100,
    100)` — since it's a stretched anchor, that made it exactly 100px wider than its own viewport mask,
    overflowing ~50px past both the left and right edges **on every line**, not just long paragraphs
    (headers were clipped too). The earlier attempted fix (a `LayoutElement` with `preferredWidth` on the
    paragraph body text) did nothing, because that layout group has `childForceExpandWidth = true`, which
    stretches every child to the (already oversized) content width regardless of any per-child
    min/preferred/priority — confirmed via a temporary diagnostic script that dumped live RectTransform
    values in batch mode, not by guessing. Real fix: `content.sizeDelta = Vector2.zero;` right after it's
    created. Verified via `ScreenshotTool` renders of all three help tabs before rebuilding WebGL, then
    **user confirmed fixed on both PC and phone.**
    **Lesson for next time:** don't declare a UI layout bug fixed off a plausible-sounding theory alone —
    verify with an actual render (ScreenshotTool batch renders are fast, seconds, and don't need a full
    WebGL rebuild) or, when the numbers still don't add up, dump the live RectTransform values directly
    rather than iterating on more theories.
14. **(2026-09-27, playtest round 5)** Copy-paste join link and a QR code, both host-only in the lobby:
    - **Copy join link:** `LobbyInfo.JoinLink` (set only when hosting online, from
      `WebSocketTransport.PageOrigin()` + the room code), a "COPY JOIN LINK" button in `LobbyScreen`
      (`MenuScreens.cs`) using `GUIUtility.systemCopyBuffer` (Unity's standard WebGL clipboard API),
      flipping to "COPIED!" for 1.5s via `LobbyScreen.Tick()` (now called from `GameController.Tick()`).
      Confirmed live in a browser: click executes cleanly, no console errors, label reverts correctly.
      Could not verify the actual clipboard contents from this automated session (clipboard *read*
      permission was denied in that context — a different, stricter permission than the *write* path
      Unity uses, so this isn't evidence of a problem) — worth a real Ctrl+V check.
    - **QR code:** no existing QR library in the project (or available to add via a package manager here),
      so this is a **from-scratch QR encoder**, `Assets/Scripts/Core/QrCode.cs` (`QrEncoder.Encode`, pure
      C#, no Unity dependency, matching the rest of Core) — byte mode, ECC level L, versions 1-6 only (a
      join link is always short; versions 7+ need an extra "version information" block this doesn't
      implement). Rasterized to a `Sprite` via `SpriteFactory.QrCode(text)` (point-filtered, spec's
      4-module quiet zone — both matter for real scanning) and shown in `LobbyScreen` next to the copy
      button, host-only.
      **This went through 3 real bugs before it worked, all found by actually decoding the output with an
      independent JS QR library (jsQR) rather than trusting the algorithm by inspection** — visually
      correct-looking finder patterns are not proof a QR code decodes:
      1. The timing-pattern loop ran across the *entire* row/column instead of just the gap between the
         finder patterns, corrupting the finder patterns themselves.
      2. `WriteFormatInfo`'s bit-to-position mapping didn't match the spec (traced and fixed by reading
         the reference library `qrcode-generator`'s actual source for the ground-truth placement), and
         separately overwrote the fixed "dark module" with a format bit.
      3. `GeneratorPoly` built the Reed-Solomon generator polynomial's coefficients in the wrong order
         (LSB-first instead of the MSB-first order the synthetic-division step needs) — caught by an
         independent Reed-Solomon syndrome check (a valid codeword must evaluate to zero at every
         `alpha^i` root; this failed for i=0..14 before the fix, passed clean after `Array.Reverse`).
      After all three fixes: verified with 3 different URLs (different lengths → versions 2 and 3,
      different chosen masks) all round-tripping byte-for-byte through jsQR. The live Unity render was
      confirmed visually (sharp, correctly proportioned, in the right spot) via a real OS-level screenshot,
      but this session could **not** decode the exact live WebGL canvas pixels (the browser clears the
      WebGL backbuffer between frames since Unity doesn't set `preserveDrawingBuffer`, so a JS canvas read
      comes back blank) — **a real phone-camera scan is the one remaining check the user needs to do.**
15. **(2026-09-27, same session)** Two follow-up fixes to the copy-link/QR feature, both caught by the
    user actually looking at it:
    - **Real bug:** the join link/QR used `WebSocketTransport.PageOrigin()` — literally whatever URL the
      host typed into their own browser, e.g. `http://localhost:8000` if that's how they opened the game.
      Useless to anyone else, since "localhost" only ever means "this machine." Fixed properly: the relay
      server (`Server/server.js`) already detects its own LAN-reachable address for its startup console
      log (used to print BOTH a real LAN IP and a useless `169.254.x.x` link-local one — that filtering
      logic is now a shared `lanAddresses()` helper) — it now also sends that address in the `hosted`
      relay message (`RelayMsg.lan`, new field), and `NetHost.LanAddress` / `GameController.GetLobbyInfo`
      prefer it over the page's own URL. Verified for real: connected directly to the relay's WebSocket
      as a plain script and read the raw `hosted` JSON back, confirmed `lan` holds the real LAN IP, then
      confirmed the lobby UI updates in a live browser. **Caught a stale-server gotcha in the process:**
      editing `server.js` doesn't affect an already-running `node server.js` process — it must be
      restarted for the fix to take effect, easy to forget mid-session.
    - **Layout bug:** the "Fill empty seats with bots" toggle (bottom-anchored, from before the QR/copy
      button existed) visually overlapped the QR code once that got added above it. Moved into the same
      top-anchored column as the rest of the join-info stack, positioned between the copy button and the
      QR code — confirmed live in the browser, no more overlap.
16. **(2026-09-29, in-class playtest batch)** A large batch of feedback. Rules/engine changes, all verified
    via RulesSim (default + legality + report modes, all pass; deck now 45 cards):
    - **Elder** now plays 2 cards only once revealed (1 while unrevealed) — was "always 2," per feedback.
    - **Queen's** ability now refreshes each time she newly becomes thronebearer (checked at each round's
      start, not mid-round if the throne changes hands via an ability), instead of once-per-game-ever.
    - **Hand refill changed**: everyone draws back exactly as many cards as they played that round (Elder:
      1 or 2 depending on reveal; thronebearer/trusted advisor: 0, since they play no card face-down that
      round) — no longer a flat "top up to 3." Hand sizes can now drift from card effects instead of
      always snapping back; this measurably lengthened games in simulation (mean round count roughly
      doubled) — expected, not a bug, but worth knowing if it feels too slow at the table.
    - **Double Decree** (`EnactTwoNextRound`) changed: the trusted advisor now keeps 3 cards (not 2) that
      round, so the king's "enact 2" is an actual choice instead of "both cards you already had."
    - **Fatal Roll and Judgement Roll** now reroll on an empty seat instead of doing nothing
      (`RollSeatRerollEmpty`, capped at `SeatCount*4` attempts as a safety net).
    - **Loyalists' win condition widened**: now requires executing *every* currently Traitor-aligned player
      — Prince, Nobleman, the Prophet (if dealt), and a Beggar who has been revealed (previously a revealed
      Beggar did NOT count; a hidden Beggar still doesn't, per the user's own wording: "the only time the
      beggar doesn't count is when he's not revealed"). Implemented as one change (`Execute()` now checks
      `EffectiveFaction` instead of the static role-based faction), since `EffectiveFaction` already
      encoded exactly this hidden/revealed split for the Beggar.
    - **New role: Prophet** (Traitor faction, ability "Diviner's Revelation" — become the next trusted
      advisor; crystal-ball icon, user picked it over a bishop's hat or an eye from 3 mockups). Drawn from
      a **new 3rd role pool** (Loyalist/Chaos/Traitor-extra) at a flat 15% chance per random draw across
      all player counts, with the Loyalist/Chaos split scaled down proportionally to make room — these
      odds are Claude's own proposal (the user asked for "something reasonable, scaled off the current
      numbers"), not confirmed by the team; revisit if Prophet feels too rare/common. No guaranteed slot
      at any player count, unlike Chaos (6+) and Loyalist (7+). Prophet recognizes Prince/Nobleman and vice
      versa, same as the existing traitor-teammate visibility exception.
    - **New card: Mead** (x2 copies, deck→45): next round, nobody can see any card's face — not their own
      hand, not the trusted advisor's pick, not the king's enact pick. This is enforced as genuinely
      hidden-info-safe over the network (not just a client-side "hide the text" trick): `Decision.Blind`
      and `GameSnapshot.RoundBlind` flags travel to the wire layer, where `Wire.cs` blanks the card's
      `effect`/`text` before serializing (`BlindCard`), matching the same standard already used for hidden
      roles. Discards (ShowHandDiscard1 etc.) are unaffected — only PlayCard/KeepCard/EnactCard are blind.
    - **Enacted cards are now shown to everyone** as an actual card graphic (bottom-right popup, ~4.5s),
      not just log text — `Game.LastEnacted`/`EnactedSeq` (public once enacted, not hidden info) drive a
      new panel in `GameScreen.cs`.
    Bug fixes:
    - **Copy-link clipboard bug (real, reported by the user):** `GUIUtility.systemCopyBuffer` needs a
      browser "secure context" (https, or exactly `localhost`) — silently does nothing on a plain-http LAN
      page, which is how every phone reaches this game. Fixed with a custom clipboard bridge
      (`KcCopyText` in `KcWebSocket.jslib`) that always falls back to the older `execCommand('copy')` path,
      which works in any context. New `Clipboard.Copy()` static helper (`Transport.cs`) used in place of
      the direct `GUIUtility` call.
    - **Physical keyboard didn't work on the join-code `CodePad`.** Added `CodePad.Tick()` (polls
      `Keyboard.current` for A-Z/Backspace/Enter), wired from `GameController.Tick()` via a new
      `StartScreen.Tick()`. Tap-to-join still works unchanged.
    - **iOS fullscreen button doesn't work (Android does).** This is a genuine, long-standing WebKit/Safari
      limitation — iOS Safari has never supported the Fullscreen API for a plain page element, only
      `<video>`. Not fixable from the app side. Detected iOS in the WebGL template and swapped the button's
      behavior there to a one-time tip ("Add to Home Screen" is iOS's actual equivalent) instead of leaving
      a dead button; every other platform keeps the real fullscreen call. Since the game already fills the
      whole viewport by default (see ruling #12), fullscreen on iOS was only ever going to hide Safari's
      own address bar, not make the game bigger — so this is a lower-severity gap than it first sounds.
    - **Not fixed — needs a decision:** verifying the QR-join "pick your name" feature live in the browser
      surfaced that Unity's WebGL `InputField` doesn't actually accept typed keystrokes at all, and this
      turned out to be a PRE-EXISTING bug also affecting the Settings screen's "Your name" field, not
      something new. See "Known issues — open, NOT yet fixed" above for the real fix (a DOM-overlay input
      bridged via jslib) and why it wasn't attempted in this same pass.
    "Traitor visibility (other than Beggar)" was also re-reported this batch; the user confirmed afterward
    it was already working (see ruling #17) — was just re-listed while compiling the whole playtest batch,
    not an actual regression.
17. **(2026-09-29, same session)** User asked for the known-traitor-teammate case to be easier to spot at
    a glance: a known role's NAME text is now tinted by team color (red for traitors, so your fellow
    traitor's name reads red, not just a small badge icon next to a white name) — covers both a public
    reveal and the private traitor-teammate badge case from ruling #10, in `TableView.Refresh`
    (`v.Name.color`). Verified via PlayMode tests (11/11 pass) and a fresh WebGL build; the user resumed
    playtesting on this build.
18. **(2026-09-30)** A batch of three new roles plus a rule change, gathered over a design conversation
    (Q&A transcript not reproduced here; see rulings below for the settled answers):
    - **The King loses his execution immunity, in every game**, not just ones with a Reaper. He's just
      another Loyalist now — `Execute()`'s old `if (p.Role == Role.King) return;` block is gone entirely, no
      replacement loss condition attached to his death.
    - **New role: Reaper** (Chaos). Wins when the King, Prince and Queen have all been executed. Ability
      ("Royal Execution", once per game): execute a revealed member of the Royal Family specifically — King,
      Prince or Queen, Reaper's choice of which — and only offered at all once at least one of the three is
      revealed (corrected 2026-09-30, same day: originally implemented as "any revealed player," which
      didn't match the card's actual wording — also, the ability case was accidentally never wired into
      `AbilityPhaseAsync` at all in the first pass, so Reaper had no way to act until this fix). Only ever
      dealt in a game where the Queen is also dealt (King/Prince are always dealt, so this just gates on the
      Queen showing up in that
      game's Loyalist draws); 8% chance per eligible draw — Claude's own proposal, like Prophet's 15%,
      revisit if it feels off.
    - **New role: Lovers** (Chaos). Dealt exactly like any other role from the random pool — explicitly
      **not** a manual host assignment and **not** gated on any real-world relationship between the two
      players (the physical-game note about them "knowing each other" doesn't translate to anything the
      engine enforces) — one draw just assigns it to two seats at once instead of one. Win: instant, the
      moment alive-count drops to `Game.LoversWinAliveThreshold` (5, picked as a first guess — "let's try
      top 5 at first") with both Lovers still alive. If one Lover is executed, the other is executed too,
      through the exact same `Execute()` path as any other death (reveals their role, still feeds the
      Jester-win and Traitor-wipe checks if either Lover happens to hold one of those roles — confirmed
      acceptable, the user couldn't think of a card/check this would break). 10% chance per draw, only at
      7+ players, only with ≥2 undealt seats remaining that draw.
    - **New role: Martyr** (Loyalist). Once per game: sacrifice themselves to bring back any one currently
      dead player (no faction restriction — "they get to choose from the people who are currently dead"),
      who returns alive at that same round's end with their prior reveal state untouched (a hidden player
      stays hidden, a revealed one stays revealed — free, since `Execute()` never un-sets `Revealed`). The
      ability can be used even while the Martyr is still unrevealed, and using it does **not** force a
      reveal — a carve-out from the usual "abilities need you to be revealed" gate in `AbilityPhaseAsync`.
      The Martyr's own death does not count as an "execution" for Jester-win purposes (implemented by
      reusing `Execute(martyr, revealRole: false)`, which was already the no-force-reveal path). If the
      Martyr was thronebearer when they sacrifice, the die rerolls among the living players for a new one
      (`Game.RollSeatRerollEmpty`) instead of the usual deterministic "lowest surviving seat" fallback used
      elsewhere — a repeated die roll until it lands on someone alive. Revival also needed a new
      `Game.FindFreeSeat` helper: a resurrected player's old `Seat` number can collide with whoever
      `EnsureThrone()` moved into the vacated throne while they were dead, so revival prefers their old seat
      but falls back to the lowest genuinely free one.
    - **Max players raised 8 → 10.** `TableView.Slots`/`SlotsFor` gained two new physical seat positions
      (slotted into the clockwise order between the existing top row and the right-hand end, so `RotateLeft`
      still reads as a sensible visual rotation) for the 9- and 10-player layouts.
    - **Deck doubles at 8+ players**: every `ActionCatalog` entry's count ×2 (a x1 card becomes x2, a x2
      card becomes x4) via `ActionCatalog.BuildDeck(playerCount)` — the previously-static `BuildDeck()`/
      `TotalCards` are now player-count-parameterized; every call site (`Game`'s constructor,
      `Tools/RulesSim`'s three files, `HelpPanel.ShowCards()`) was updated to pass the count through.
    - `Tools/RulesSim/LegalityTests.cs`'s hard "FAIL: King died" assertion was removed (the King dying is now
      legitimate) and its player-count cycling widened from 5–8 to 5–10; `Program.cs`'s "every role is
      unique" sanity check was relaxed to allow exactly the Lovers pair as the one legitimate duplicate.
    - **Verified same day**: `RulesSim` (default/legality/report, 5–10 players) all pass; 11/11 Unity
      PlayMode tests pass; a full `ScreenshotTool` render plus a one-off 9/10-player table render confirmed
      the new layout. Two real bugs were caught and fixed by this verification pass, not just assumed fixed:
      the Reaper/Lovers odds were compounding across a game's several role draws (~37%/~22% actual instead
      of the stated 10%/8%) until switched to a one-shot-per-game check; and the new top-right table seat
      sat close enough to center to get hidden behind the decision panel, repositioned after an actual
      render caught it. `Report.cs` also had a stale hardcoded "4 player-count brackets" that silently
      skewed every percentage once the range became 6 brackets (5–10) — fixed.
19. **(2026-10-01)** A follow-up batch after more playtesting:
    - **Reaper's ability was actually never wired up** in the first pass — only the win-condition check
      existed, so Reaper had no way to act at all. Fixed, and corrected to match the card's real wording:
      Royal Execution only targets a revealed King, Prince or Queen specifically (not any revealed player),
      and the ability isn't even offered until one of the three is revealed.
    - **Martyr replaced with Medium** (Loyalist). No more self-sacrifice: once per game, even while still
      unrevealed and without forcing a reveal, bring back any one currently-dead player at that round's end;
      they get 2 full rounds alive, then die again automatically (a real `Execute()` call, so it still feeds
      the Jester/Traitor-wipe/Reaper checks, but without forcing a reveal). New `Game.borrowedLifeRoundsLeft`
      dictionary tracks the countdown, decremented at each round's end before that round's new resurrection
      (if any) is added, so a freshly-revived player isn't docked a round they never got to play.
    - **Card UI fixes**: hovering or selecting a card now calls `SetAsLastSibling()` so its text doesn't get
      hidden behind a neighboring card's art; the selected-card scale changed from 1.16x to 1.10x ("10%
      bigger"). Mead's card text shortened (was a 2-sentence paragraph, now one line) to fit the card better.
    - **Royal Gifts (`Peek3Distribute`) now names the card in the prompt itself** ("Give \"<card>\" to which
      advisor?") instead of a generic "give this card" prompt with no indication of which card — the
      thronebearer couldn't previously tell what they were giving away.
    - **New: a "recent cards" panel** above the event log shows the last 3 *enacted* cards (already public
      knowledge once enacted) with no player attribution, via `Game.EnactedHistory` → `GameSnapshot` →
      `Wire.cs` → `GameScreen`'s new `recentCardsText` label.
    - **Settled same day:** Jester should be "50%" accounting for the Chaos pool having more than just
      Jester/Beggar in it. The actual fix was realizing the per-draw Jester-vs-Beggar pick was already a
      fair coin flip, but the Chaos pool could be (and usually was, at higher player counts) drawn from
      *twice* in the same deal, so both ended up dealt together almost every game — Jester's real per-game
      appearance rate was ~83%, not 50%. Fixed by clearing the Chaos pool entirely the moment either role is
      chosen (`chaos.Clear()` right after both `TakeRandom(chaos)` call sites in `DealRoles`), making
      Jester/Beggar mutually exclusive per game with a genuine one-time 50/50 between them. Reaper and
      Lovers are untouched — separate pools, unaffected by this. Verified via `RulesSim report`: Jester and
      Beggar now both land at ~45% in-play (not 100% between them, since some games draw neither at all,
      e.g. 5-player games with no guaranteed Chaos slot).
    - Knight executing a hidden Beggar: **confirmed as intended, kept as-is** — it still kills the Knight
      too, since a hidden Beggar's `EffectiveFaction` is Loyalist same as any other hidden Loyalist target.
    - Queen's reveal ability: **confirmed as intended, kept as-is** — stays an optional confirm each time she
      newly takes the throne (the once-per-throne-take refresh itself was already live since ruling #16),
      not made mandatory.

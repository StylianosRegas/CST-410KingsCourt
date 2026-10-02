# King's Court

A digital, phone-multiplayer version of **King's Court**, a social-deduction card game for 5–10 players
built in Unity for CST-410. One player is the King's court; hidden Loyalist, Traitor, and Chaos roles try
to protect or destroy the throne over a series of rounds of secret card-passing.

This file gets you from a fresh clone to a running game. For the full rules, architecture notes, and the
history of every design decision, see [`context.md`](context.md) (project handoff doc) and
[`Assets/Docs/RULES_ASSUMPTIONS.md`](Assets/Docs/RULES_ASSUMPTIONS.md) (rules engine specifics).

## What you need

- **Unity 6000.6.1f1** (the exact version this project is pinned to — see `ProjectSettings/ProjectVersion.txt`).
  Install it via Unity Hub. The WebGL Build Support module is required if you want to build/host online play.
- **Node.js** (any reasonably recent version) — only needed to run the relay server for online multiplayer.
  Not required to just open the project and play offline with bots.

## 1. Open the project

Open Unity Hub → Add → select this folder → open with 6000.6.1f1. Let it import (first import can take a
few minutes). Open `Assets/Scenes/KingsCourtTable.unity` if it isn't already open, then press Play.

## 2. Play offline (no setup needed)

From the Start screen, click **HOST GAME**. If no relay server is reachable, the lobby falls back to
**OFFLINE** automatically — turn on "Fill empty seats with bots" and press **START GAME** to play a full
game solo against bots right in the Editor.

## 3. Play online (phones on the same Wi-Fi)

The relay server only forwards messages between the host and phones — it has no game logic of its own, so
there's no account or cloud service to set up.

```bash
cd Server
npm install        # first time only
node server.js     # serves ../Builds/WebGL and relays on port 8000
```

It prints a LAN address like `http://192.168.1.20:8000` — that's what phones on the same Wi-Fi open. The
first time you run it, Windows will ask whether Node.js may accept connections: choose **Private networks**,
or phones won't be able to reach it.

The server serves whatever is currently in `Builds/WebGL`, so you need a build there first — see the next
section. If you only want to test from the Unity Editor itself as the host, the server still needs to be
running so phones have something to join (the Editor connects to `ws://localhost:8000/ws` automatically).

## 4. Build the WebGL version

From Unity's menu: **King's Court → Build WebGL**, or headlessly from a terminal:

```bash
"<path to Unity.exe>" -batchmode -quit -projectPath . -buildTarget WebGL ^
  -executeMethod KingsCourt.EditorTools.WebBuild.Build -buildOutput Builds/WebGL
```

Rebuild + restart `node server.js` any time you change game code and want online players to see it —
the server reads files straight off disk, so a restart alone picks up a new build, but it doesn't rebuild
anything for you.

**Close the Unity Editor before running any batch-mode command** (build, tests, or screenshot tool) — Unity
can't open the same project twice at once. Reopen it afterward with:

```bash
"<path to Unity.exe>" -projectPath . -openfile "Assets/Scenes/KingsCourtTable.unity"
```

## 5. Run the tests

Two independent layers:

**Unity PlayMode tests** (drives the actual UI, and one test spins up a real relay server over real
sockets) — editor must be closed first:

```bash
"<path to Unity.exe>" -batchmode -projectPath . -runTests -testPlatform PlayMode ^
  -testResults results.xml -logFile test.log
```

**`Tools/RulesSim`** — a standalone bot simulator (thousands of games/second, no Unity needed) that checks
rules invariants, illegal-move handling, and role-deal odds. Its `.csproj` isn't checked in (regenerate it
if missing — see below), since this is normally built with Unity's own bundled .NET SDK rather than a
separately installed one:

```bash
cd Tools/RulesSim
dotnet build -c Release
dotnet bin/Release/net8.0/RulesSim.dll            # full games, checks for leaks/crashes
dotnet bin/Release/net8.0/RulesSim.dll legality    # illegal-move enforcement
dotnet bin/Release/net8.0/RulesSim.dll report      # win rates + estimated play time
```

If no `dotnet` SDK is installed system-wide, Unity 6000.6.1f1 ships one at
`<Unity install>\Editor\Data\DotNetSdk\dotnet.exe` — use that path directly in place of `dotnet` above.
If `RulesSim.csproj` is missing, recreate it targeting `net8.0`, compiling `Program.cs`, `LegalityTests.cs`,
`Report.cs`, and everything under `../../Assets/Scripts/Core/`.

## Project layout

- `Assets/Scripts/Core/` — the rules engine (`Game.cs`), zero Unity dependency, shared by the game and
  `Tools/RulesSim`.
- `Assets/Scripts/Unity/` — the UI and local game session, built entirely in code (no hand-authored prefabs
  except `Assets/Prefabs/CardView.prefab`).
- `Assets/Scripts/Unity/Net/` — the networking layer (host/client/transport) on top of `Server/`.
- `Server/` — the plain Node.js relay server. No game logic, just message forwarding by room code.
- `Tools/RulesSim/` — standalone bot simulator for rules testing and balance tuning.
- `Assets/Tests/PlayMode/` — Unity NUnit tests covering the full UI flow and the networking stack.
- `Boardgame Design/` (sibling folder, not under version control here) — the physical game's design docs.

## A secret, for testing

Click the discard pile 10 times within one round (host or offline game only) to unlock an admin panel:
force any pending decision, reassign any player's role, or reveal anyone. Useful for testing specific
scenarios without playing a whole game out to reach them.

## Known open issues

See the "Known issues — open, NOT yet fixed" section of `context.md` for the current list (as of the last
update: an intermittent "back to lobby" repro that needs more detail, and a few odds/UX items still being
tuned from live playtesting). `context.md`'s rulings log also explains *why* various rules ended up the way
they did, which is worth reading before changing win conditions or role abilities.

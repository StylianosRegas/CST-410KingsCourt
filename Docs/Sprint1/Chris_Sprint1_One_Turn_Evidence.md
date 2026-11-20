# CST-410 Digital Prototype Sprint 1: Chris's evidence for "one complete turn, twice in a row"

Chris Luciano. Repo: `CST-410KingsCourt`, branch `Chris`, last commit `70aff5b` (Build 9 tag). Run on 2026-11-19.
Labels: **RUN** = I ran it today and kept the output. **READ** = from the repo's own notes or files, not re-run today.
**TEAM** = needs the team, not me.

## What I ran (RUN)
Both runs use `Tools/RulesSim/bin/Debug/net8.0/RulesSim.dll`, built 2026-11-13 11:46, after the last change to
`Game.cs` (11:45), so it matches Build 9. The unity editor was not opened.

1. `phasetrace 5 2 1` (5 players, 2 rounds, seed 1). Exit code 0, "No errors." The same `Game` object plays
   Round 1 and then Round 2 with nothing set by hand in between. Output: `RAW_phasetrace_2_rounds_2026-11-19.txt`.
   Each round goes: NameAdvisor, SubmitCards (each advisor), Filter (trusted advisor passes cards to the
   king), Enact (thronebearer picks), then ability decisions. Every move the engine accepted was one the
   turn machine had listed as legal; the run would have thrown on a rejection.
2. `legality 300` (300 games, two scripted humans plus bots). Accepted 9,973 moves and refused 38,908. Refusals
   were by reason: not a valid option 20,316; choice no longer open 10,158; already played this round 5,031;
   not your turn 3,403. Exit code 0, "LEGALITY CHECKS PASSED". Output: `RAW_legality_300_games_2026-11-19.txt`.
   The run is a headless simulator, not the Unity Game view.

Limit: the trace was run with random placeholder decisions and the second round ended at the round cap (the game
was still running), as `phasetrace` is designed to do. It shows two consecutive rounds, not a finished game.

## The five conditions in the assignment

| # | Condition | Evidence | Label |
|---|---|---|---|
| 1 | A turn starts from the state the last turn left | One `Game` object plays both rounds. Round 2 lists different cards in each hand than Round 1 (for example player 1 holds different action cards), so the hands carried and refilled. | RUN (headless) |
| 2 | Actions use the names on the player aid | The engine names are NameTrustedAdvisor, PlayActionCard, FilterToTwo, EnactCard, Reveal. Not compared with the player aid. | Dropped: not needed for this sprint |
| 3 | An illegal action is refused when attempted | Legality run above: 38,908 refusals, none accepted. A refused move is also pictured in `Docs/Screenshots/07_waiting_and_refused_move.png`. | RUN (headless); screenshot READ |
| 4 | State changes are visible | Screens 08 to 14 in `Docs/Screenshots/` show play, filter, enact, ability and game-over screens. `GameFlowTests` drive the UI with simulated clicks (READ in `context.md`). Not re-run today. | READ |
| 5 | Play passes and the next turn runs through the same code | Round 2 runs through the same `PlayRound` path in the trace. | RUN (headless) |

Naming note: the trace prints "thronebearer: Player 3" in the log and "player 2" in the round header. Both are
the same seat: the log counts from 1 and the header counts from 0.

## What I did NOT do
- I did not open Unity or play the Game view, so the Thursday cold-start observations are not covered.
- No commit and no push. The working tree has uncommitted files (screenshots, `CardView.prefab`,
  `SpriteFactory.cs`, `ScreenshotTool.cs`). The assignment wants "a commit on the team repository"; that needs
  your OK, and the "no merge to main" rule still applies.

## Draft backlog candidates I own (for the team's Padlet; estimates and MoSCoW are left for the team vote)
Source: the repo's own "known issues, open" list and the Build 9 blanks. None is invented work.
1. **Back to lobby with the same room code fails (open since 2026-09-27).** As a host, I can return my players to the lobby with the same room code, so that a second game starts without everyone rejoining. Done when: (a) exact repro steps are written down; (b) after a game ends, "Back to lobby" keeps the room code; (c) a phone on the game-over screen lands in the lobby; (d) a new `NetworkTests` case covers it. Touches `GameController.ReturnToLobby()`, `NetHost.ReturnToLobby()`, `NetClient` "backlobby". Needs repro details first.
2. **Typing a name works in the WebGL build.** As a phone player, I can type my name in the lobby, so that I do not have to use the tap pad. Done when: (a) keystrokes reach the field in a WebGL build in Chrome; (b) the same on a phone browser; (c) the name shows in the host's lobby list. Touches a new `.jslib` plugin next to `KcWebSocket.jslib`. Probably large: split before estimating.
3. **Fill the physical-box parity column in the Build 9 parity sheet.** As a team member, I can see which parts of the physical box the digital game matches, so that the team can find gaps. Done when: (a) every row of `BUILD9_PARITY_CHECK.md` has a physical-box entry; (b) each entry is checked against the box by a person. Needs the physical box (TEAM).
4. **Action names match the player aid.** As a player, I see the same action names on screen as on the printed aid, so that I do not relearn the game. Done when: (a) every on-screen action label is listed next to its player-aid name; (b) every mismatch is fixed or logged. Needs the player aid post (TEAM).

## Hours
The assignment text says to log real hours and not invent them, and the Halo rubric compares hours with a minimum
for the team size (team of 4: 160 hours in the template). Those two statements pull in different directions, so
ask Professor Artzi which one governs. I have not estimated or entered any hours.

## Update 2026-11-20: what the team Padlet says (READ, not re-run)
Board: padlet.com/sregas/sticky-board-s023e49jsiil8gzep41a (read only, nothing changed). I read the Observations,
Greybox Screens - Nov 17 and Backlog Card Sort - Nov 19 sections. The board loads lazily, so a section may have more posts than I saw.

**This changes condition 4 (and the whole of Part 2): the one-turn claim is NOT proven in Unity.**
- Backlog post **T-01 MUST, "Round-1 decision panel freeze"**: in a WebGL build the first KeepCard/EnactCard decision panel
  showed a bare card with no text and no button, and the game stopped before Round 1 finished. Reproduced as Jester/Host Game and
  Knight/"The Quiet Court". A NullReferenceException is logged in `GameScreen.cs` (`BuildPanel`/`UpdatePanel`, OptionType.Card branch);
  the exact line is unknown because WebGL strips the stack trace. Owner on the post: "TBD (needs real team)".
- Backlog post **T-02 MUST (process), "WebGL build on disk is stale"**: `Builds/WebGL/Build/WebGL.data` dates from 2026-10-23, older than the
  newest script change. So T-01 may or may not still happen on a fresh build. T-02's last criterion is to re-run the Round-1 cold start on a
  fresh build to find out. Owner: "TBD".
- My headless runs (above) show the rules engine plays two rounds. They do not show the Unity screens do. Until T-02 and T-01 are
  settled, do not tell the instructor "one complete turn is playable in the build".

Other things the Padlet shows:
- **T-03** (Court Wizard label) is marked "Owner: Chris (already fixed)", branch `s1/T-03-courtwizard-label`, commit `eb6823e`, pushed.
  That commit is not on my local branch `Chris` (my last commit is `70aff5b`), so I have not checked it.
- Player aid icon letters on the greybox screens: X Execute, R Reveal, S Swap, C Cards. These are action icons, not the engine's
  move names (NameTrustedAdvisor, PlayActionCard, FilterToTwo, EnactCard, Reveal). Condition 2 needs the team's named player aid
  to be compared line by line; I have not seen that post.
- The **Observations** section holds the Oct 29 and Nov 5 physical-play notes (25 and 4 numbered observations), not the Nov 17
  board-screens "Hesitations". The assignment wants at least four backlog posts linked to a Nov 17 hesitation. I found no
  Hesitations section on the board; see the TBD below.

## Update 2026-11-20 (later): fresh WebGL build, played by hand (RUN)
- **Build:** Unity 6000.6.1f1 batch build from branch `Chris` at `70aff5b` plus the uncommitted working tree, written to a scratch
  folder (not into the repo). `WEBBUILD result=Succeeded size=49884529 errors=0 time=00:00:19`. Log: `build_run_2026-11-20/unity_webgl_build.log`.
  The log also prints "Build Finished, Result: Success."; the other stack-trace-looking lines in it are Unity logging, not build failures.
- **Played:** served by `Server/server.js` on localhost, opened in a browser, **Host Game**, 6 seats (me as host and King, 5 bots),
  Start Game. Screenshots 1 to 6 in `build_run_2026-11-20/`.
  - Round 1: I was thronebearer, chose Bot 5 as trusted advisor. The bots played cards face down and Bot 5 passed two to me. The
    card-decision panel (the one T-01 describes) showed the prompt "Choose the card to enact", subtext, two cards and an
    **ENACT THIS CARD** button. I enacted "Change Seats" and answered "Swap seats with which advisor?" (Bot 6).
  - Round 2 started with no editor action: seats had swapped (Bot 6 thronebearer), Bot 4 named trusted advisor. I played the Mead card as an
    advisor. The card left my hand and a new one (Lucky Loss) arrived.
  - Round 3 started: Bot 6 had enacted Change Seats and Bot 6 and Bot 5 swapped, so play passed to Bot 5. The "Enacted" panel and the
    recent-cards list showed it.
  - Browser console: no errors.
- **What this shows:** two consecutive rounds played end to end in the real UI of a fresh build, with no editor, which covers the
  assignment's conditions 1, 4 and 5 for this path. **T-01 did not reproduce on this path** (Host Game, King, bots). T-01 was reported
  for the roles Jester and Knight, which I did not play, so T-01 is not proven fixed.
- **What it does not show:** refusal of an illegal action by hand in the UI (condition 3 is still from the legality run and the repo
  screenshot), the Jester and Knight paths, a phone joining, or the player-aid name comparison (condition 2).
- Because the build was written to a scratch folder, `Builds/WebGL` in the repo is still the old Oct 23 build, so T-02 is not closed.
  Replacing it would change tracked files, so I did not do that without your OK.

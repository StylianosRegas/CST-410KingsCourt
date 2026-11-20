# CST-410 Sprint 1: playtest data

Scope note: the assignment asks for a cold-start card sort and Nov 17 hesitation posts. Our game was already complete and had been playtested
heavily, so those did not apply. We changed the backlog to be built from the evidence we already had.
These are real results. "RUN" = I ran it (date shown) and kept the output. "COPIED" = copied from the team Padlet.

## 1. Simulator playtests on Build 9 (RUN, 2026-11-19 and 2026-11-20)
Tool: `Tools/RulesSim/bin/Debug/net8.0/RulesSim.dll`, built 2026-11-13 after the last `Game.cs` change. Bots choose at random, so
these show whether the rules engine behaves, not whether the game is balanced or fun.

| Run | Result | File |
|---|---|---|
| `phasetrace 5 2 1` | 2 rounds played through the real decision path, exit 0, no rejected move (2026-11-19) | RAW_phasetrace_2_rounds_2026-11-19.txt |
| `legality 300` | 300 games: 9,973 moves accepted, 38,908 refused (not a valid option 20,316; no longer open 10,158; already played 5,031; not your turn 3,403); passed (2026-11-19) | RAW_legality_300_games_2026-11-19.txt |
| `rules` | All rule-ID and rulesheet checks passed. 120 full games, 9,368 play decisions, 2,254 filter decisions, 2,391 enact decisions checked; 91 games reshuffled the discard with no card lost (2026-11-20) | RAW_rules_checks_2026-11-20.txt |
| `scenarios 500` | **1 failure.** The Poisoned Crown's end condition fired 0 of 500 games (453 ended in a Draw). The Falling Hourglass end condition fired 175 of 500. Exit 1 (2026-11-20) | RAW_scenarios_500_each_2026-11-20.txt |
| `report 1000` | 6,000 games (1,000 each at 5 to 10 players): Traitors won 39.8%, Jester 21.1%, Draw 19.1%, Loyalists 18.7%, Lovers 0.9%, Reaper 0.5%. Mean 19.3 rounds, median 16, 90th percentile 39. Estimated play time at 190 s a round: mean 64 min, median 54, 90th percentile 127 (2026-11-20) | RAW_report_6000_games_2026-11-20.txt |

The play-time figures use the report's own assumptions (3 min setup plus a per-round time), not timed human play.

## 2. A fresh WebGL build, played by hand (RUN, 2026-11-20)
Build succeeded (49,884,529 bytes, 0 errors). Host Game, 6 seats, King role. Round 1, Round 2 and the start of Round 3 played
through the real screens with no editor and no console error. Screenshots and log: `build_run_2026-11-20/`. Jester and Knight paths not played.

## 3. Earlier physical playtests (COPIED from the Padlet 'Observations' section)
- Session 1, Oct 29: 25 numbered observations. Session 2, Nov 5: 4 observations plus a closed-box setup time of about 1 minute (one rotation, so one setup time).
- Gabe's survey is cited there (Q4, Q11). I have not seen the survey itself.

## 4. Classmate reviews of QuizIt (CST-461, not this class)
Not part of this assignment; kept in the CST-461 folder.

## What is still missing
Exact teammate hours (they each reported 4 to 6); Jester and Knight hand playtests.

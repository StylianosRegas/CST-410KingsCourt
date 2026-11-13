# Build 9: the setup procedure, step by step

The numbered steps a player follows from a closed box to the first action, and the method in `Assets/Scripts/Core/SetupProcedure.cs` that does the same step. `Game`'s constructor calls them in this order and nothing else, and every step records itself in `Game.SetupSteps`, so what ran is visible.

> **Provisional.** These steps are written from the finished rules engine, whose setup is the one the team playtested. They have not yet been compared line by line with the setup procedure on the table (the parity check, `BUILD9_PARITY_CHECK.md`).

| Step | At the table | In the code | Leaves behind |
|---|---|---|---|
| 1 | Choose the scenario | `Step1_ChooseTheScenario` | Player count, hand size and refill rule settled; the balance numbers loaded from the asset |
| 2 | Lay out the table: the throne and one chair for every other player | `Step2_LayOutTheTable` | Seat 0 (the throne) and chairs 1 to players minus 1 |
| 3 | Deal one loyalty card face down to every seat | `Step3_DealTheLoyaltyCards` | One role per player, drawn by the odds table |
| 4 | Seat the players: the King takes the throne, everyone else takes a chair | `Step4_SeatThePlayers` | Every player in a seat; the King on the throne |
| 5 | Take out the action cards | `Step5_BuildTheActionDeck` | 45 cards, or 90 from 8 players |
| 6 | Shuffle the action deck | `Step6_ShuffleTheActionDeck` | A shuffled draw pile |
| 7 | Deal the starting hands | `Step7_DealTheStartingHands` | 3 cards in every hand (4 in the Poisoned Crown) |
| 8 | Seat the King and open the first round: whoever is on the throne goes first | `Step8_SeatTheKingAndOpenTheFirstRound` | The King as first thronebearer, ready to name a trusted advisor |

## What "ready for the first action" means

`Game.FirstActionProblem()` returns nothing when all of these are true, and names the first thing that is wrong otherwise:

- all 8 steps ran, in order
- the King is the first thronebearer
- no two players share a seat
- every hand holds the starting number of cards
- no card is in two places
- the game is at round 0 with no result

`RulesSim setup` runs this for every scenario and every player count (2,200 cold starts in the last run, 0 failures).

## Why step 8 is a check and not a draw

The table decides the first player by who sits on the throne, and the King always does, so there is nothing to draw or roll. The code therefore verifies that the King is on the throne instead of picking someone. If the table ever decides the first player some other way, this is the one step to change.

## What the step split did not change

The steps are the old constructor body cut into named pieces, using the random number generator in exactly the same order. `RulesSim fingerprint` plays 1,800 seeded games (all six player counts) and hashes every move: before and after, the hash is `19B7DAA7037ACAF06274324160928FD521312B2A5015BB32D4AFC3B9085651C5`.

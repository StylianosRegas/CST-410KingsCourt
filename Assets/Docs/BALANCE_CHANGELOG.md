# Balance changelog

The digital changelog for the Unity build. Every entry names its source on the team's Sticky Board Padlet
(https://padlet.com/sregas/sticky-board-s023e49jsiil8gzep41a) so the physical and digital logs agree.
Every balance number now lives in one asset, `Assets/Resources/BalanceConfig.asset`
(see `Assets/Scripts/Core/BalanceConfig.cs`).

## What the table actually did in Week 9

The Nov 3 Balance Surgery session produced **one** operation, not two. The Padlet's Changelog post says:
"Operation 2: not run this pass, only one disease had enough repeated evidence. Held for Thursday."
Build 8 therefore carries one table change (entry 1) plus one value the build and the revised sheet
disagreed on (entry 2), and entry 2 is labelled as that, not as a surgery operation.

---

## Entry 1: 2026-11-03, Operation 1, "Rules overhead" (table change)

**Source:** Padlet section "Balance Surgery - Nov 3", posts "Diagnosis - Rules Overhead" and "Changelog - Operation 1".

**Symptom (table):** five moments in Playtest Session 2, all the same gap: the top-deck rule was not read,
confusion over whether the Trusted Advisor's own hand counts at the filter, the table believed hand refill
meant "draw a card", the thronebearer passed a card to the Trusted Advisor, and the Jester as Trusted Advisor
included their own hand.

**Diagnosis (table):** rules overhead.

**Change (table):** five spots on the printed rules sheet rewritten to say it out loud instead of implying it.
NAME bars repeat Trusted Advisors, PLAY states the thronebearer plays nothing, FILTER excludes the Trusted
Advisor's own hand, REFILL spells out draw-back, not a flat draw, and the top-deck rule covers pass and filter.
All additive, nothing removed.

**Prediction (table):** at the next playtest the top-deck rule is applied unprompted, no re-asking about the
Trusted Advisor's hand, no flat-draw confusion, and the thronebearer does not try to play a card.

**Digital counterpart (Build 8):**
- How to Play, "The Round" now has a **Things people forget** section with those five spots in plain words.
- The same page now reads its numbers from the balance asset (cards passed, cards enacted, hand size, filter
  minimum), so the help text cannot drift from the rule.
- The engine already enforced all five (R-05 no repeat advisor, R-10 thronebearer and Trusted Advisor play no card,
  R-06 filter from at least 3 cards, top-deck fill, draw-back refill), so no rule behavior changed for this entry.

**What the second playtest showed:** the Padlet's own status is "applied to the sheet. Confirmation test not run
yet, needs a live table." The Nov 5 session (post "PlaySession2 Nov 5") has one questionnaire: asked which rule
they looked up or got wrong, the player answered "Trusted advisor"; hardest component: "Rulesheet"; most boring
stretch: "Set up"; what to cut: "Hand draw mechanics". One respondent is not a verdict, so this entry stays
**NEEDS ANOTHER PASS**, and the Trusted Advisor rule is the first thing to watch on Thursday.

---

## Entry 2: 2026-11-08, Prince throne rounds 4 to 3 (value alignment, found while building the config)

**Source:** Padlet "Revised Rules", section 4, step 7 (rewritten Nov 5): "If the Prince has now ended 3 rounds in a
row as the Thronebearer ... the Traitors win." The older Loop post also says 3 consecutive turns.

**Disagreement:** the engine used **4** (`princeStreak >= 4`), and so the Quiet Court scenario text said 3 while the
game ran 4. The in-game help already said 3. Putting every number in one place is what exposed it.

**Change:** `PrinceThroneRoundsToWin` 4 -> 3 in `BalanceConfig.asset`. One value edited, no code edited.

**Prediction:** traitors win more often and games are shorter, since the Prince has one fewer round to hold.

**What the simulator showed** (300 seeded bot games per player count, same seeds before and after,
`RulesSim tune 300 --compare PrinceThroneRoundsToWin=4`, "after" column is the table's value):

| Players | Traitor wins, 4 rounds | Traitor wins, 3 rounds | Avg rounds, 4 | Avg rounds, 3 |
|---|---|---|---|---|
| 5 | 37.3% | 47.3% | 14.5 | 13.1 |
| 6 | 30.0% | 37.7% | 18.0 | 16.5 |
| 7 | 33.0% | 40.0% | 19.1 | 17.4 |
| 8 | 31.7% | 37.7% | 21.3 | 18.9 |
| 9 | 35.3% | 40.7% | 22.6 | 21.0 |
| 10 | 33.3% | 42.7% | 29.1 | 25.9 |

Bots play at random, so this shows the direction of the change, not how humans will balance it. The table decides
whether that direction is wanted; the sheet already says 3.

---

## Open disagreements to take back to the table (not changed in code)

1. **Refill wording.** Operation 1 and the Loop post say draw back as many cards as you played. Revised Rules
   section 4 step 6 says draw "until they hold 3", including the Thronebearer and Trusted Advisor. They agree
   while every hand starts a round at 3. They differ after a card effect changes a hand size. The engine does
   draw-back. Which one is the rule?
2. **Prince dead.** Revised Rules step 7 says "(or, once the Prince is dead, another Traitor has)". The engine
   only counts the Prince.
3. **Operation 2** was not run at the table and is held for Thursday.

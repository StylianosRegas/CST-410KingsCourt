# Rules engine: decisions and assumptions

Engine: `Assets/Scripts/Core` (no Unity dependency). Bot simulator: `Tools/RulesSim`.
Source docs: Game Design Document v3.0, Action Card Index.docx, team answers (Jester win, reshuffle).

## Confirmed by the team
- Deck reshuffle: when the deck runs out, unplayed/discarded cards are reshuffled into it.
- Jester wins only by being voted out and/or killed.

## Decisions where the docs were silent or contradictory (please confirm)
1. **Deck was 43 cards (confirmed by the team); now 45** after "Mead" (x2) was added 2026-09-29 from playtesting feedback. Two "Card Ideas" in the original index are unfinished and not included.
2. **Role deal (from the team's odds table):** King, Prince and Nobleman always play. Remaining seats are drawn without replacement: pick Loyalist or Chaos by the odds, then a random role of that kind. A kind with no roles left drops to 0%. Loyalist = Queen, Knight, Elder, Court Wizard. Chaos = Jester, Beggar. The King is not counted as a Loyalist pick.
   - 5 players: 2 draws at 70% Loyalist / 30% Chaos
   - 6 players: +1 guaranteed Chaos, 2 draws at 50 / 50
   - 7 players: +1 guaranteed Chaos, +1 guaranteed Loyalist, 2 draws at 20% Loyalist / 80% Chaos
   - 8 players: same guarantees, 3 draws at 20 / 80 (confirmed: as many as needed to reach 8)
   Bot check over 3,000 games each: average Chaos roles 0.60 / 1.75 / 1.94 / 1.99 at 5 / 6 / 7 / 8 players, so at 7 and 8 both Chaos roles are almost always in play.
3. **Round:** Thronebearer names the trusted advisor. Every other advisor (not the thronebearer, not the trusted advisor, who plays no card: confirmed) plays 1 card face down, all at the same time (Elder plays 2). Played cards are shuffled before the trusted advisor sees them. Trusted advisor keeps 2, discards the rest. Thronebearer enacts 1. Hands refill to 3 at end of round.
   - **Playtesting fix (2026-09-27):** at 4 players alive (thronebearer, trusted advisor, and only 2 others), only 2 cards get played, leaving the trusted advisor no real choice when filtering to 2. The engine now draws the top of the deck as a 3rd option in that case (`played.Count == 2` and not `roundAllToThronebearer`) so there's an actual decision. See `Game.PlayRoundAsync`.
   - **Playtesting addition (2026-09-27):** the thronebearer can't name the same trusted advisor two rounds in a row — except when the next trusted advisor is forced by a "die decides" card effect, which still allows a repeat. If nobody else is eligible (very few players left), a repeat is allowed rather than deadlocking. See `Game.PlayRoundAsync` (`lastTrustedId`/`eligible`).
4. **The die** has one face per seat. Landing on an empty seat does nothing.
5. **Abilities** need the player to be revealed (King is always revealed; Elder is always active). Jester's ability is every round; all others once per game. Queen's is once per game.
6. **"Voted out" for the Jester:** there is no voting mechanic in the docs, so only being executed wins for the Jester right now.
7. **Win checks:** Loyalists win when *every* currently Traitor-aligned player has been executed — Prince, Nobleman, the Prophet (if dealt), and a Beggar who has been revealed (changed 2026-09-29: a revealed Beggar now DOES count, reversing the earlier "does not count" rule below). A still-hidden Beggar is Loyalist-aligned and isn't required. Traitors win when the Prince ends 3 consecutive rounds as thronebearer (changed from 2, per playtesting feedback on 2026-09-27). A hidden Beggar loses if the Traitors win; a revealed Beggar loses if the Loyalists win. The Jester wins only by being executed (not when the deck runs out). Implemented as `Execute()` checking `EffectiveFaction` instead of the static role-based faction — see context.md ruling #16.
8. **"Next round the thronebearer chooses 2 action cards"** = the thronebearer enacts both cards passed to them.
9. **"All advisors play face up next round; the thronebearer forces an advisor to reveal"**: face-up applies next round, the forced reveal happens immediately.
10. **Executions** reveal the executed player's role, except "Do not reveal their loyalty card".
11. **(changed 2026-09-30)** The King can now be executed like anyone else, in every game. He carries no
    special immunity or extra win/loss condition — he's just another Loyalist body for the existing
    "all traitors executed" check.
12. Fewer than 3 players alive ends the game as a draw.

## Playing with real people (host-authoritative)
- Every answer goes through the host (`SeatAgent.Submit`). It refuses: answering when it is not your turn, a second card in the same round (the Elder's second card is a separate, legitimate question), a stale or closed choice, or an option that does not exist. The player is told why. Covered by `RulesSim legality`.
- A player who disconnects or leaves is replaced by a bot in their seat, whatever their role (no death, no kick). The bot answers any question that was waiting on them.
- Bots are optional (Settings and Lobby toggle). Dice are virtual. Chat is a placeholder toggle until online play exists.
- Each player only ever receives a `GameSnapshot` for themselves: other players' roles and hands are not in it.
- **Playtesting fix (2026-09-27):** a phone joining while a game was already running used to be denied outright ("That game has already started."), so someone who disconnected mid-game — or a late joiner — had no way back into the room until the whole game ended and a new code was shared. `NetHost` now accepts joins any time there's a free seat; a join while `Session != null` just waits in the lobby (`PlayerId` stays -1, so it gets no snapshots and isn't part of the running game) and is seated automatically the next time `StartOnline` runs. `LobbyMsg.inProgress` tells a waiting phone's lobby screen that a game is currently running. A player who disconnects mid-game now also has their room slot freed immediately (previously kept forever as an `Online = false` ghost entry) so they — or anyone else — can actually get back in. See `NetHost.OnRelay` ("peer_joined"), `NetHost.OnPeerLeft`, `NetworkTests.APhoneCanRejoinTheLobbyMidGameAndIsSeatedInTheNextOne`.
- **Playtesting addition (2026-09-27):** the Prince and Nobleman always recognize each other as fellow traitors, regardless of reveal state (matches the user's playtesting request — traitor faction members should know their teammate). Implemented as a targeted exception in `GameSnapshot.For` (`IsTraitorTeam`), not a general hidden-info leak: the Beggar's traitor/loyalist ambiguity is untouched.
  - **Fixed same day:** the Snapshot-level fix alone wasn't enough — `TableView.cs` only drew a role icon/name when `p.Revealed` was true (a *public* reveal) or `p.IsYou`, so a Nobleman looking at the Prince still saw a blank grey pawn even though the snapshot correctly carried the Prince's `Role`. `TableView.Refresh` now shows the small private "badge" + a `(RoleName)` label for any `p.Role.HasValue` case, not just `p.IsYou`, without touching `full` (the public-reveal flag), so nobody else at the table sees anything different.
- **(2026-09-29) Answered:** a revealed Beggar now DOES count toward the Loyalists' win requirement (see item 7 above) — but the traitor-mutual-visibility exception itself was NOT extended to the Beggar; it's still just Prince/Nobleman/Prophet recognizing each other from the start. A hidden Beggar's identity is still a surprise to everyone, traitors included.
- **(2026-09-29) New role, Prophet** (Traitor faction, ability "Diviner's Revelation": become the next trusted advisor) joins the traitor-visibility exception above. Drawn from a new 3rd role pool (Loyalist/Chaos/Traitor-extra) at a flat 15% chance per random draw, no guaranteed slot at any player count. The 15% figure is Claude's own proposal, not team-confirmed — revisit if it feels off.
- **(2026-09-29) Elder** now needs to be revealed to play 2 cards (1 while unrevealed) — was unconditional.
- **(2026-09-29) Queen's** ability now refreshes every time she newly becomes thronebearer, not once per game ever.
- **(2026-09-29) Hand refill changed**: draw back exactly as many cards as you played that round, not a flat top-up to 3. Thronebearer/trusted advisor draw 0 (they play no card face-down). Hand sizes can drift from card effects now instead of always snapping back to 3.
- **(2026-09-29) Double Decree** (`EnactTwoNextRound`): trusted advisor now keeps 3 cards that round (was 2), so the king's "enact 2" is an actual choice.
- **(2026-09-29) Fatal Roll / Judgement Roll** reroll on an empty seat instead of doing nothing.
- **(2026-09-29) New card, Mead** (x2, deck→45 cards): next round nobody can see any card's face, including their own hand — enforced wire-safe (card content blanked before sending), not just hidden client-side. See context.md ruling #16 for full detail on all of the above.
- **(2026-09-30) Max players raised 8 → 10, deck doubles at 8+ players** (every `ActionCatalog` entry's
  count ×2, so e.g. a x1 card becomes x2, a x2 card becomes x4). See `ActionCatalog.BuildDeck(playerCount)`.
- **(2026-09-30) Three new roles added** from playtesting feedback — see context.md ruling #18 for the full
  design discussion and rationale:
  - **Reaper** (Chaos): wins when King, Prince and Queen have all been executed. Once/game ability ("Royal
    Execution") executes a revealed member of the Royal Family specifically — King, Prince or Queen,
    whichever is currently revealed and alive — and is only offered at all once at least one of them is
    revealed (changed 2026-09-30: originally any revealed player, corrected to match the card's actual
    wording). Only ever dealt in a game where the Queen is also dealt (King/Prince are always dealt); 8%
    chance per eligible draw when that gate is met — Claude's own proposal like Prophet's 15%, revisit if
    it feels off.
  - **Lovers** (Chaos): dealt like any other role, except one draw assigns it to two seats at once (not a
    manual host pick — there is no real-world-relationship requirement baked into the engine). 10% chance
    per draw, only at 7+ players, only with ≥2 undealt seats remaining. Instant win the moment alive-count
    drops to `Game.LoversWinAliveThreshold` (5) with both Lovers still alive. If one Lover is executed, the
    other is executed too, as a real execution (reveals role, still feeds the Jester/Traitor-wipe checks).
  - **Medium** (Loyalist, replaced Martyr 2026-10-01): once/game, even while still unrevealed and without
    forcing a reveal, can bring back one currently-dead player of their choice (any dead player, no faction
    restriction) at that round's end. They return alive with their prior reveal state untouched, get to play
    2 full rounds, then die again automatically at the end of the second one — a real `Execute()` call (so
    it still feeds the Jester/Traitor-wipe/Reaper win checks), but without forcing a reveal, so a player who
    came back hidden stays hidden through their permanent death too. Unlike the old Martyr, the Medium
    themselves never dies or gives anything up to use this. See `Game.borrowedLifeRoundsLeft`.
- **(2026-10-01) Jester vs Beggar are now mutually exclusive per game**, with a genuine 50/50 coin flip
  deciding whichever one (if either) gets dealt at all — previously both tended to end up dealt together
  once the 2-member Chaos pool got drawn from twice in the same game (common at higher player counts),
  pushing Jester's real appearance rate to ~83% instead of the intended 50%. Confirmed via `RulesSim report`:
  now ~45%/45% Jester/Beggar in-play (the gap to 100% is games where neither got drawn at all, e.g. at 5
  players with no guaranteed Chaos slot). Reaper and Lovers are unaffected — they're separate pools with
  their own independent odds, not part of this exclusivity.
- **(2026-10-01) Confirmed, not changed:** a Knight executing a hidden Beggar still kills the Knight too
  (a hidden Beggar's `EffectiveFaction` is Loyalist, same as any other hidden Loyalist-aligned target) —
  the user confirmed this is the intended ruling, not a bug.
- **(2026-10-01) Confirmed, not changed:** the Queen's reveal ability stays an optional confirm each round
  ("use it or not"), refreshing every time she newly takes the throne (already implemented 2026-09-29, see
  item 5 above and context.md ruling #16) — not made mandatory.
- **(2026-10-01) Prince's win condition: 4 consecutive rounds** as thronebearer (was 3, changed 2026-09-27).
- **(2026-10-01) Medium revives for 3 rounds** (was 2), and the revived player is **always revealed** on the
  way back, loyalist or not, regardless of their prior hidden/revealed state — changed from the original
  "prior reveal state untouched" behavior.
- **(2026-10-01) The "give the trusted advisor a real choice" top-deck top-up generalized**: was only
  triggered at exactly 2 cards in play; now keeps drawing from the deck until there are 3 whenever fewer
  than 3 end up played (covers 0/1-card cases too), not just the one specific 2-card scenario it was
  originally written for.
- **(2026-10-01) Show Your Hand (`ShowHandDiscard1`) is now genuinely public**, not just a log line using
  raw internal role/effect names. A dedicated popup (`Game.ShownHandOwner`/`ShownHandCards`/`ShownHandSeq`,
  same pattern as `LastEnacted`) shows for every player for ~6s with whose hand and the actual card titles.
- **(2026-10-01) Admin cheats, host/offline only**: clicking the discard pile 10 times within one round
  (`GameScreen`'s invisible hit area over `TableView.DiscardPilePos`) unlocks an ADMIN panel that can
  reassign any player's role live (`Game.AdminSetRole`), force-reveal anyone (`Game.AdminReveal`), and
  force-submit an answer on behalf of any seat's current pending decision (`Game.AdminForceSubmit`, reuses
  `SeatAgent.Submit` so it's still logged and bounds-checked). A joined phone has no `Game` to cheat on, so
  the hit area and panel are gated behind `localGame = session as GameSession` being non-null. `Player.Role`
  had to become mutable (was `readonly`) to support reassignment.

## Not built yet
Online joining from phones (Unity Relay + Lobby needs a linked Unity account; app vs browser build not decided), chat, real art. The UI screens exist (Assets/Scripts/Unity) but were verified by headless renders, not by clicking through in Play mode. The Unity project targets Unity 6000.6.1f1.

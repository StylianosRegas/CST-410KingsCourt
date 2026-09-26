# Rules engine: decisions and assumptions

Engine: `Assets/Scripts/Core` (no Unity dependency). Bot simulator: `Tools/RulesSim`.
Source docs: Game Design Document v3.0, Action Card Index.docx, team answers (Jester win, reshuffle).

## Confirmed by the team
- Deck reshuffle: when the deck runs out, unplayed/discarded cards are reshuffled into it.
- Jester wins only by being voted out and/or killed.

## Decisions where the docs were silent or contradictory (please confirm)
1. **Deck is 43 cards (confirmed by the team).** Two "Card Ideas" in the index are unfinished and not included.
2. **Role deal (from the team's odds table):** King, Prince and Nobleman always play. Remaining seats are drawn without replacement: pick Loyalist or Chaos by the odds, then a random role of that kind. A kind with no roles left drops to 0%. Loyalist = Queen, Knight, Elder, Court Wizard. Chaos = Jester, Beggar. The King is not counted as a Loyalist pick.
   - 5 players: 2 draws at 70% Loyalist / 30% Chaos
   - 6 players: +1 guaranteed Chaos, 2 draws at 50 / 50
   - 7 players: +1 guaranteed Chaos, +1 guaranteed Loyalist, 2 draws at 20% Loyalist / 80% Chaos
   - 8 players: same guarantees, 3 draws at 20 / 80 (confirmed: as many as needed to reach 8)
   Bot check over 3,000 games each: average Chaos roles 0.60 / 1.75 / 1.94 / 1.99 at 5 / 6 / 7 / 8 players, so at 7 and 8 both Chaos roles are almost always in play.
3. **Round:** Thronebearer names the trusted advisor. Every other advisor (not the thronebearer, not the trusted advisor, who plays no card: confirmed) plays 1 card face down, all at the same time (Elder plays 2). Played cards are shuffled before the trusted advisor sees them. Trusted advisor keeps 2, discards the rest. Thronebearer enacts 1. Hands refill to 3 at end of round.
4. **The die** has one face per seat. Landing on an empty seat does nothing.
5. **Abilities** need the player to be revealed (King is always revealed; Elder is always active). Jester's ability is every round; all others once per game. Queen's is once per game.
6. **"Voted out" for the Jester:** there is no voting mechanic in the docs, so only being executed wins for the Jester right now.
7. **Win checks (Beggar confirmed by team):** Loyalists win when both traitors (Prince, Nobleman) are executed; they share the win with every loyalist, including a Beggar who is still hidden. Traitors win when the Prince ends 2 consecutive rounds as thronebearer; a Beggar who has been revealed wins with them. A hidden Beggar loses if the Traitors win; a revealed Beggar loses if the Loyalists win. The Jester wins only by being executed (not when the deck runs out). A revealed Beggar does not count toward the 'both traitors executed' check.
8. **"Next round the thronebearer chooses 2 action cards"** = the thronebearer enacts both cards passed to them.
9. **"All advisors play face up next round; the thronebearer forces an advisor to reveal"**: face-up applies next round, the forced reveal happens immediately.
10. **Executions** reveal the executed player's role, except "Do not reveal their loyalty card".
11. The King cannot be executed; the effect is wasted if it targets him.
12. Fewer than 3 players alive ends the game as a draw.

## Playing with real people (host-authoritative)
- Every answer goes through the host (`SeatAgent.Submit`). It refuses: answering when it is not your turn, a second card in the same round (the Elder's second card is a separate, legitimate question), a stale or closed choice, or an option that does not exist. The player is told why. Covered by `RulesSim legality`.
- A player who disconnects or leaves is replaced by a bot in their seat, whatever their role (no death, no kick). The bot answers any question that was waiting on them.
- Bots are optional (Settings and Lobby toggle). Dice are virtual. Chat is a placeholder toggle until online play exists.
- Each player only ever receives a `GameSnapshot` for themselves: other players' roles and hands are not in it.

## Not built yet
Online joining from phones (Unity Relay + Lobby needs a linked Unity account; app vs browser build not decided), chat, real art. The UI screens exist (Assets/Scripts/Unity) but were verified by headless renders, not by clicking through in Play mode. The Unity project targets Unity 6000.6.1f1.

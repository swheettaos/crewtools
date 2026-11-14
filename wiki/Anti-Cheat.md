# Anti-Cheat

Two parts, one switch (Anti-cheat tab: on/off): the **money checks** and the **cheat watch**. A catch from either is
handled the same way: refused, every switch off, then **Ban / Kick / Restrict**, either **at once** or **ask you**.

## Money checks

A crew-mate's game sends the money for a build or a delete separately from the build itself. Crew Tools prices what
each building message builds and removes (belts at the game's own price for their length, and pieces already gone from
what it remembers of the last 5 minutes) and pays building money **only when it matches**. Money that matches nothing
is never paid, then:

| Money that matches nothing | What happens |
|---|---|
| A cost (money taken) | Flagged in the cheats log, never a ban |
| A refund, with a build or delete from them within 10 s | Flagged; a ban once these pass $20,000 in 10 minutes |
| A refund with no build or delete at all | Cheating |

Also cheating: money no build could cost, trick money over the limit, impossible kilograms.

**No false bans:** for 10 s after any refused action, building money is refused quietly: not paid, not flagged. (A
refused demolish still sends its $50 refund; without this an honest player would be banned for it.)

## Cheat watch

Catches what cheat trainers (WeMod and the like) do to *your* world: the host can't see the program on someone else's
PC, only what their game sends.

| Check | Catches |
|---|---|
| Speed | Faster than 30 m/s sideways on foot, or 60 m/s in a vehicle, for 2+ seconds |
| Flying | Off the ground (not in a vehicle, not a passenger, not climbing) for 45+ seconds |
| Reach | Building or removing more than 60 m from where they stand (the game's reach is about 30 m) |
| Deposits | Selling or unloading more than 3x the dust their tool or vehicle holds, or selling dust more than 150 m from every station |
| Teleports | A jump of 150 m+ that lands more than 200 m from every station (the game's map only teleports to stations), or a jump with no teleport payment within 15 s (once one paid crew teleport has been seen in the session) |

It never counts: anything a permission or a protected base refused, money or deposits within 10 s of a refusal, the
host, or the first 20 s after a player appears. Fast Machines and Freeze Time of Day only change the cheater's own game.
The limits are generous, so only blatant cheating trips them; every check and limit is a setting (`[AntiCheat]`).

## When someone is caught

- **At once** (AutoBan on): a small notice in the top-right corner, "BANNED: name" and why, gone after 8 s.
- **Ask me:** a card at the top: **F7** bans, **F8** lets it go (the keys work during play; its buttons work with the
  pause menu open).

Every catch goes into `BepInEx/CrewPermissions.cheats.log`, and building money into `CrewPermissions.money.log`.

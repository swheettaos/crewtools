# Permissions and Bases

## Permissions

Per player: **Build**, **Demolish**, **Sell**, **Shop**, **Capsules**, each on or off (Players tab: orange = on, click to
switch). A refused sale isn't paid: refused dust goes back into the player's hopper (the game does that itself), and a refused
cargo capsule is given back, beside the player. Capsules moved by a tool, brush or vacuum are always
allowed.

**New players** start with the switches you choose on the Rules tab (default: build, sell, shop, capsules on;
demolish off). Everyone already known keeps their own. On the Players tab a switch that differs from the default is
marked `*` (e.g. "off*"); Manage > **Reset to defaults** sets a player back to them.

## The game's own crew limits (Moon Sweepers 1.0.6)

From 1.0.6 the game has its own per-player limits, **Build**, **Drive** and **Spend**, set in its crew menu (hold
**Tab**, then the **right mouse button**). Crew Tools keeps them the same as its switches:

| Crew Tools switch | Game limit |
|---|---|
| Build | Build |
| Shop | Spend |
| (none) | Drive: set it in the game's crew menu |

Either side can change them: switch Build or Shop in Crew Tools and the game's limit follows; change Build or Spend in
the game's crew menu and the Crew Tools switch follows. A player joining a public crew gets the game's own default
there (no building or spending) only for a moment: Crew Tools sets them to their switches within a second, so a new
player gets your new-player switches (Rules tab) and a returning one keeps their own. The game keeps its limits per save slot
(`crew_limits.json`). On 1.0.5 the game has none and Crew Tools works as before.

## Protected bases

Per base (North, South, East, West): on or off, and a radius (default 150 m from its station). Crew-mates inside can
**look but not touch**: no building, demolishing, selling, shopping or handling capsules.

- Restrict only: nobody is banned for it.
- The first time a crew-mate walks into a protected base after joining, they're told once in crew chat:
  "@name: this area (South base) is restricted." Refused actions after that are refused quietly.
- **Passes:** give a crew-mate a pass for chosen bases with **Passes** in their player row.
- You (the host) are never restricted, and map teleports still work.

## Vehicles

Crew-mates can always **drive, park and recall** vehicles they own: inside a protected base, over the spending cap, or
with Shop off. Only **buying or upgrading** a vehicle needs Shop (and no zone, and room under the cap).

# Credits

## Crew Permissions by tylerun

The permission switches and the money anti-cheat come from
[Crew Permissions by tylerun](https://www.nexusmods.com/moonsweepers/mods/71), merged in with the author's permission
("Anyone can take and edit my mods. Please provide credit to the original on your page"). Thank you, tylerun!

Rebuilt from its released DLL, renamed and commented for Crew Tools; what is checked and refused is tylerun's design:

| File | What |
|---|---|
| `Guard.cs` | The permission check on every crew message (build, demolish, sell, shop, capsules) and the pricing of what a crew-mate builds or removes |
| `Hooks.cs` | The hooks marked "(Crew Permissions)": which game methods are checked |
| `AntiCheat.cs` | Its money section: building money must match what was really built or removed; impossible amounts, trick money and kilograms; what a catch gets |
| `Alerts.cs` | Telling the host about a catch (redesigned for Crew Tools) |
| `Returns.cs` | A refused cargo sale gives the capsule back, beside the player |
| `Players.cs` | The permission switches (`Switch`, `Allowed`) |
| `Node.cs` | The five switches |

Changes in Crew Tools are marked "Crew Tools:" in the code: vehicle messages always go through; building money right
after a refusal is refused quietly; the check also applies protected bases and the spending cap; its settings and
player file are merged into Crew Tools'. Its roles (Visitor, Crew, Trusted) are gone: every player was loaded as a
Visitor, so they never applied.

Everything else (`Plugin.cs`, `Crew.cs`, `Zones.cs`, `Spending.cs`, `Owners.cs`, `Cleanup.cs`, `Welcome.cs`,
`ServerName.cs`, `Ui.cs`, `Widgets.cs`, `Http.cs`, `Handover.cs`, `Trace.cs`, and the cheat watch) is Crew Tools' own.

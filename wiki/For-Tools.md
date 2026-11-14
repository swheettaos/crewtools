# For Tools

Crew Tools answers HTTP requests under `/crew/` when a mod that serves HTTP, such as [LunarLink](https://swheettaos.github.io/lunarlink/), passes requests on to plugins: its plugin
class has a public `string HandleHttp(string method, string path, NameValueCollection query, string body)` and lists
its requests in `string[] HttpHelp()`. Without such a mod nothing calls them. Reads are GET; changes are POST, and
each change does exactly what the CREW TOOLS window's button does.

<!-- include: endpoints -->

It can also be reloaded in place by a mod loader that reloads plugins while the game runs, such as LunarLink ([[With LunarLink]]): `Tools` (`Plugin.cs`)
starts and stops as one piece and hands what is in flight (each crew-mate's spending in the last 30 minutes, building
money held for its building, a ban reclaim in progress, cheating popups not answered yet) to its next start.

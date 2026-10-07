# ValheimGoblinPack

A BepInEx/Jötunn mod that adds two unique, persistent NPCs to a Valheim world.

## Joe

A small Fuling (0.8x scale) who roams the whole map looking for Sean.

- **Wanders the map.** He travels between far-off points and goes to Sean's last known position about half the time (`Joe.SeanSeekChance`). He avoids the Ashlands, the Deep North and open ocean.
- **Only talks about Sean or the Baby Wars.** Every 18–40 s, if a player is within 40 m, he shouts a line, often using the nearest player's name. All his dialogue is in `BepInEx/config/GoblinPack/joe_lines.txt` (created on first run). Edit it, then run `goblinpack_reloadlines` in the console.
- **Throws up.** Every 1–2.5 minutes, if a player is within 40 m, he stops and vomits, using the same effect as eating Pukeberries (`Joe.PukeEnabled`, `Joe.PukeIntervalMin/Max`).
- **Steals.** He walks up to a nearby player, pickpockets a random non-equipped item (up to 10 from a stack), gloats and runs off. Each theft is stored on Joe, and killing him drops everything he stole at its original quality.
- **Passive until hit.** Joe doesn't target a player until that player damages him. He then stays hostile to that player for 90 s (`Joe.ProvokeSeconds`). Other players stay safe unless they hit him too.
- **"babywars".** If anyone types `babywars` or `baby wars` in chat (any case or punctuation, including `/s` shouts), Joe turns hostile to all players for 120 s. If the speaker is within 400 m, he charges at them. The phrases are configurable (`Joe.RagePhrases`).
- **Scales with "player level".** Valheim has no character level, so the mod uses one of two stand-ins:
  - **Skills:** the highest top-5 skill average among players within 80 m.
  - **Bosses:** the number of the 7 bosses defeated in the world.

  The default uses whichever is higher. Both map to 0–1, which sets his health (0.6x–10x a Fuling) and his damage (0.25x–2.5x, low at the start because Fuling weapons are Plains-tier). His hover name shows `Joe (Lv N)`.
- **Unique, and respawns.** The server enforces exactly one Joe (and one Sean): every few seconds it removes any extra copy, including ones made with `spawn`. When Joe dies he disappears, and ten minutes later he reappears 60–140 m from a random online player.

## Sean

A travelling Dvergr merchant (cloned from the ice mage) who sells weather-themed gear.

- He walks 250–700 m to a new spot, camps there for 3–6 minutes, then moves on. While any player is within 15 m he stops and stays open for business.
- Press **E** on him to open the normal trader window (vanilla `Trader` + `StoreGui`, paid in coins).
- He's a tank (3000 HP) and takes only 10% damage from ordinary monsters, so wolves don't keep killing the shop. Players can still kill him, and he respawns after 20 minutes.

**Sean's stock:**

| Item | Type | Effect | Price |
|---|---|---|---|
| Soda ×5 | food | +35 health, +150 stamina (20 min) | 25 |
| Merch | chest | black, no armor, no stats | 60 |
| Glock | crossbow | semi-automatic Arbalest: no reload, uses bolts | 750 |
| Restraining Order | belt | Joe must stay 15 m away from you: no stealing, no taunting, he backs off. Void if you hit him; doesn't cover the Baby Wars rage | 300 |
| Diddy Oil ×3 | potion | +25% movement speed, +100% parry bonus for 5 min | 120 |
| 67 | fists | end-game: 160 slash, 67 blunt, 30 lightning (+ per upgrade level) | 6767 |

The original weather gear (three charms, Bottled Sunshine/Thunderstorm and four weather weapons) still exists in the game, so nobody loses copies they already own, but Sean only sells it if `Sean.SellWeatherGear = true` (read at game start).

## The Baby Wars

Say "baby wars" in chat three times within an hour (anyone, in total; `BabyWars.TriggerCount` / `TriggerWindowMinutes`) and the war starts near whoever said it last:

- Everyone sees **THE BABY WARS HAVE BEGUN** and the sky turns to thunderstorm until it's over.
- **Joe** sets up a war camp 25–40 m away (cauldron, totem, banners) and commands from it.
- **Joe's baby army** pours out of the camp in waves (4 by default, 6 babies then +3 each wave). Next wave comes after 75 s, or sooner once most of the current one is dead:
  - **Diaper Commando:** a Fuling at 0.4x.
  - **Bottle Grenadier:** a Fuling shaman at 0.45x.
  - **Binky Berserker:** a Fuling berserker at 0.6x, from wave 2.

  They scale with progression like Joe (`BabyHealthScale` / `BabyDamageScale`) and drop only a few coins, so there's no Fuling-loot farming.
- **Sean** arrives on the far side with **Sean's Security** (3 Dvergr guards), who fight the babies.
- **Picking a side:**
  - **Baby Bonnet:** Joe drops one into the pack of everyone who said the phrase. Babies and Joe leave you alone, and Sean's Security attacks you.
  - **Merch:** Sean's shirt. Security leaves you alone, and the babies still come for you.
  - **Neither:** everyone attacks you.
- **Curse the next wave** at **Joe's War Cauldron**: look at it and press a hotbar key holding one of these items, the same way you use an offering bowl.
  - **Pukeberries:** the babies keep stopping to throw up.
  - **Thistle:** they're half speed.
  - **Troll hide:** they're slower and hit 40% softer.

  Curses stack, and the whole camp is told who cursed the wave.
- **How it ends:**
  - The last wave dies: the players win.
  - Joe dies: the babies scatter.
  - 15 minutes pass: Joe "declares victory".

  Either way, the camp, babies and guards disappear, and there's a 30-minute cooldown before the next war.

## When Joe meets Sean

When the two get within 35 m of each other, they fight for 45 s:

- **Joe** throws fireballs and calls down meteors.
- **Sean** throws ice shards, lightning strikes (Eikthyr's lightning effect) and hail volleys.

Every spell is an area-of-effect blast, and plenty of them miss and land around the arena, so anyone nearby gets caught in it. Bystanders take 50% damage by default (`Fight.BystanderDamageMultiplier`). The spells don't change the terrain.

Neither of them can drop the other below 15% health, so the fight always ends in a stalemate. Joe then storms off. After a 10-minute cooldown they can fight again. Players nearby get a centre-screen warning when a fight starts.

## Install (Vortex)

1. In Vortex, install **BepInExPack Valheim** and **Jotunn** (both on Nexus) and deploy.
2. Build the zip (below), then in Vortex go to **Mods → Install From File** and pick `dist\GoblinPack-<version>.zip`. Enable it and deploy.
3. Every player and the server need GoblinPack and Jotunn. Jotunn refuses connections where the versions don't match.

The zip contains `BepInEx/plugins/GoblinPack/GoblinPack.dll` (plus this README), so Vortex deploys it relative to the game folder. It doesn't include Jotunn: install that as its own mod.

## Building

Requirements:
- .NET SDK 6 or newer (`winget install Microsoft.DotNet.SDK.8`)
- Valheim with BepInEx already deployed into the game folder (step 1 above)

From the repo root in PowerShell:

```powershell
dotnet build src\GoblinPack\GoblinPack.csproj -c Release -p:ValheimDir="D:\Steam\steamapps\common\Valheim" -p:PackageZip=true
```

This writes `dist\GoblinPack-0.1.0.zip`; the build output prints the full path. Notes:
- On the first build, JotunnLib generates publicized copies of the game assemblies in `valheim_Data\Managed\publicized_assemblies` and compiles against them. Re-run the build after a game update.
- `-p:DeployToPlugins=true` copies the DLL straight into `<ValheimDir>\BepInEx\plugins\GoblinPack` for quick testing without Vortex. Don't leave that copy in place alongside the Vortex-installed one.
- Bump `<Version>` in `GoblinPack.csproj` for each release so the zip names (and Vortex's mod list) stay distinct.
- Instead of `-p:ValheimDir`, you can copy `Environment.props.example` to `Environment.props` and set `VALHEIM_INSTALL` there.

## Config

`BepInEx/config/cavmkii.goblinpack.cfg`. Every setting is admin-only, so the server's values sync to clients.

## Console commands

- `goblinpack_where`: Joe's and Sean's last known positions, and whether Joe is enraged.
- `goblinpack_summon joe|sean`: cheat command (devcommands; the server also checks that you're an admin). Replaces Joe or Sean with a fresh one in front of you. Handy for testing a fight: summon both.
- `goblinpack_reloadlines`: reload Joe's dialogue from `joe_lines.txt`.
- `goblinpack_babywars start|stop`: cheat (devcommands; the server also checks you're an admin). Start the Baby Wars at your position, or call them off.

## How it works (code map)

| File | Role |
|---|---|
| `GoblinPackPlugin.cs` | Entry point. Patches each Harmony class separately, so a game update that breaks one signature only disables that feature. |
| `Content.cs` | Clones `Goblin` → Joe and `DvergerMageIce` → Sean with Jötunn, builds the items, status effects and Sean's `Trader` stock. |
| `JoeBrain.cs`, `SeanBrain.cs` | Behaviour on top of vanilla `MonsterAI`. Runs only on the client that owns the NPC, and keeps shared state (provokers, stash, fight timers) on the ZDO so nothing is lost when ownership moves. |
| `Wander.cs` | Long-distance travel by sliding `BaseAI.m_spawnPoint` toward a destination, so vanilla pathfinding and animation do the walking. |
| `Fight.cs`, `SpellFx.cs` | The duel. Joe's owner decides every spell and broadcasts it. Every client draws inert copies of vanilla projectile and lightning visuals. The authority applies the area damage. |
| `WorldDirector.cs` | Server-only. One Joe and one Sean per world, death detection (ZDO gone), respawn timers, position broadcasts. State is in `BepInEx/config/GoblinPack/<worldUID>.txt`. |
| `Patches.cs` | `BaseAI.IsEnemy` (Joe passive), `Character.RPC_Damage` (provocation), `Character.Damage` (scaling and duel floor), `Character.OnDeath` (stash drop), hover text, `Trader.Update` (skipped for Sean), `Chat.SendText` (rage phrases). |

## Verification status

What's been checked:
- The project compiles warning-free against Jötunn 2.30.2, HarmonyX and Unity reference assemblies.
- It also compiles against the community `ValheimGameLibs` 0.221.4 stripped game assemblies.
- Every reflected field and Harmony target exists in that game build, with exactly one matching overload.
- The `-p:ValheimDir ... -p:PackageZip=true` build runs end to end against a stand-in game folder built from those assemblies, and produces the zip layout above. Not yet tried in Vortex itself.

It has **not been run in-game yet.** Treat the first session as a test.

Things I expect might need tuning:
- **Prefab names that can't be checked offline.** `Goblin`, `DvergerMageIce`, `BeltStrength`, `MeadTasty`, `StaffFireball`, `StaffIceShards`, `lightningAOE`, and the four weapon bases. A missing one logs a warning rather than crashing.
- **Dedicated servers.** The server spawns Joe and Sean in zones it hasn't loaded, using world-generator height, which ignores player terrain edits. If they ever appear underground, that's the place to look.
- **Item icons.** The items reuse the icons of the items they're cloned from.
- **Bottled weather is local.** Valheim's forced-environment API is per-client, so only the drinker sees the change.

## Roadmap

More NPCs to come.

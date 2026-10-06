# ValheimGoblinPack

A BepInEx/Jötunn mod that adds two unique, persistent NPCs to a Valheim world.

## Joe

A small Fuling (0.8x scale) who roams the whole map looking for Sean.

- **Wanders the map.** He travels between far-off points and goes to Sean's last known position about half the time (`Joe.SeanSeekChance`). He avoids the Ashlands, the Deep North and open ocean.
- **Insults you.** Every 18–40 s, if a player is within 40 m, he shouts an insult at the nearest player by name or yells about Sean.
- **Throws up.** Every 1–2.5 minutes, if a player is within 40 m, he stops and vomits, using the same effect as eating Pukeberries (`Joe.PukeEnabled`, `Joe.PukeIntervalMin/Max`).
- **Steals.** He walks up to a nearby player, pickpockets a random non-equipped item (up to 10 from a stack), gloats and runs off. Each theft is stored on Joe, and killing him drops everything he stole at its original quality.
- **Passive until hit.** Joe doesn't target a player until that player damages him. He then stays hostile to that player for 90 s (`Joe.ProvokeSeconds`). Other players stay safe unless they hit him too.
- **"babywars".** If anyone types `babywars` or `baby wars` in chat (any case or punctuation, including `/s` shouts), Joe turns hostile to all players for 120 s. If the speaker is within 400 m, he charges at them. The phrases are configurable (`Joe.RagePhrases`).
- **Scales with "player level".** Valheim has no character level, so the mod uses one of two stand-ins:
  - **Skills:** the highest top-5 skill average among players within 80 m.
  - **Bosses:** the number of the 7 bosses defeated in the world.

  The default uses whichever is higher. Both map to 0–1, which sets his health (0.6x–10x a Fuling) and his damage (0.25x–2.5x, low at the start because Fuling weapons are Plains-tier). His hover name shows `Joe (Lv N)`.
- **Respawns.** The server keeps exactly one Joe. Ten minutes after he dies, he reappears 60–140 m from a random online player.

## Sean

A travelling Dvergr merchant (cloned from the ice mage) who sells weather-themed gear.

- He walks 250–700 m to a new spot, camps there for 3–6 minutes, then moves on. While any player is within 15 m he stops and stays open for business.
- Press **E** on him to open the normal trader window (vanilla `Trader` + `StoreGui`, paid in coins).
- He's a tank (3000 HP) and takes only 10% damage from ordinary monsters, so wolves don't keep killing the shop. Players can still kill him, and he respawns after 20 minutes.

**Sean's stock** (I read "bobbits" as baubles/trinkets):

| Item | Type | Effect | Price |
|---|---|---|---|
| Stormcaller's Charm | utility | lightning resistant, +15% stamina regen | 220 |
| Hoarfrost Pendant | utility | frost resistant | 260 |
| Sunshard Talisman | utility | fire resistant, +15% health regen | 240 |
| Bottled Sunshine ×3 | consumable | clear skies for 5 min (only for the drinker) | 90 |
| Bottled Thunderstorm ×3 | consumable | thunderstorm for 3 min (only for the drinker) | 60 |
| Thunderclap Axe | iron axe | +25 lightning | 450 |
| Hailstone Mace | iron mace | +22 frost | 500 |
| Gale Spear | bronze spear | +15 lightning, 2x knockback | 320 |
| Squall Bow | fine bow | +12 frost | 550 |

## When Joe meets Sean

When the two get within 35 m of each other, they fight for 45 s:

- **Joe** throws fireballs and calls down meteors.
- **Sean** throws ice shards, lightning strikes (Eikthyr's lightning effect) and hail volleys.

Missed spells land around the arena and leave **craters**. These are real terrain edits that persist like pickaxe digs, capped at 14 per fight. Anyone caught in a blast takes damage (50% by default, `Fight.BystanderDamageMultiplier`).

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

`BepInEx/config/cavmkii.goblinpack.cfg`. Every setting is admin-only, so the server's values sync to clients. `Fight.CraterScale` is read at startup.

## Console commands

- `goblinpack_where`: Joe's and Sean's last known positions, and whether Joe is enraged.
- `goblinpack_summon joe|sean`: cheat command (devcommands; the server also checks that you're an admin). Replaces Joe or Sean with a fresh one in front of you. Handy for testing a fight: summon both.

## How it works (code map)

| File | Role |
|---|---|
| `GoblinPackPlugin.cs` | Entry point. Patches each Harmony class separately, so a game update that breaks one signature only disables that feature. |
| `Content.cs` | Clones `Goblin` → Joe and `DvergerMageIce` → Sean with Jötunn, builds the items, status effects and Sean's `Trader` stock, and registers the crater `TerrainOp` prefabs. |
| `JoeBrain.cs`, `SeanBrain.cs` | Behaviour on top of vanilla `MonsterAI`. Runs only on the client that owns the NPC, and keeps shared state (provokers, stash, fight timers) on the ZDO so nothing is lost when ownership moves. |
| `Wander.cs` | Long-distance travel by sliding `BaseAI.m_spawnPoint` toward a destination, so vanilla pathfinding and animation do the walking. |
| `Fight.cs`, `SpellFx.cs` | The duel. Joe's owner decides every spell and broadcasts it. Every client draws inert copies of vanilla projectile and lightning visuals. The authority applies damage and spawns craters. |
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

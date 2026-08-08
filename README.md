# Zoominoes Archipelago

BepInEx plugin that connects [Zoominoes](https://store.steampowered.com/app/3282420/)
to an [Archipelago](https://archipelago.gg) multiworld.

Almost every animal, snack and souvenir starts locked and arrives as an Archipelago
item. Gift Shop slots hold items for the multiworld, and finishing a day sends a
check. Runs are played on a dedicated **AP** profile, so an existing save is never
read or written.

## Install

1. Download **BepInEx 6 bleeding-edge, `Unity.Mono`, `win-x64`** from
   <https://builds.bepinex.dev/projects/bepinex_be> and unzip it into the game
   folder, next to `Zoominoes.exe`.
2. Run the game once so BepInEx creates its folders, then close it.
3. Unzip `ZoominoesArchipelago.zip` into the same game folder — it drops into
   `BepInEx/plugins/`.
4. Install `zoominoes.apworld` by placing it in your Archipelago install's
   `custom_worlds/` folder.

Zoominoes is a Unity 6 Mono game; BepInEx 5 will not work.

On Linux the game runs under Proton with the same files. Add
`WINEDLLOVERRIDES="winhttp=n,b" %command%` to the Steam launch options, or the
loader will not inject.

## Playing

Press **F1** in game for the connection panel. Enter the server address, port, slot
name and password, then Connect. Details are remembered for next launch.

The panel opens by itself on launch until you connect.

## What is randomised

| | |
|---|---|
| Items | Animals, snacks, souvenirs, zookeepers, progressive difficulty tiers, plus extra starting gold, extra plays and hand size |
| Locations | Gift Shop slots, each playable day, winning with each zookeeper, clearing each tier |
| Goals | Clear a target tier, clear it with several zookeepers, or collect Zoo Tickets |

Only normal runs take part. Daily challenges, seeded runs and challenge runs are
left alone entirely.

Items apply from your **next** run — the game fixes its content pool when a run
starts.

## Building

```sh
dotnet build -c Release
```

Reference assemblies come from the game and BepInEx rather than NuGet; see the
`Reference` items in the csproj and copy them into `lib/`. Set `GameDir` in
`ZoominoesArchipelago.csproj.user` and the build deploys straight into the game.

## Configuration

`BepInEx/config/com.jpn.zoominoes.archipelago.cfg`. Connection details live here,
along with a `ConnectionUIKey` binding. Seed settings such as the goal and shop slot
count come from the Archipelago server, so they need no local configuration.

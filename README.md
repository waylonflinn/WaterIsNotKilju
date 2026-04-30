# Water Is Not Kilju

A My Summer Car mod that makes plastic bottles correctly display **"Water"**
instead of "Kilju" in the hover/helper text when they're filled with water,
and plays the correct drinking sound effect.

## Prerequisites

- My Summer Car installed and running (via Proton on Linux is fine)
- [MSCLoader](https://github.com/piotrulos/MSCModLoader) installed
- [Developer Toolkit](https://www.racedepartment.com/downloads/developer-toolkit.17214/) (for investigation)
- .NET SDK for building (`dotnet build`)

## Build & Install

1. Edit `WaterIsNotKilju.csproj` — set `GameDir` to your MSC install path
2. Build: `dotnet build`
3. Copy output DLL to mods folder:
   ```
   cp bin/Debug/net46/WaterIsNotKilju.dll "$GameDir/Mods/WaterIsNotKilju/WaterIsNotKilju.dll"
   ```
4. Launch MSC and check console (`~` key) for `[WaterIsNotKilju]` messages

---

## Architecture

The mod uses two different strategies for the two fixes:

### Text Fix: Update() loop (PartInspector pattern)

Instead of hooking specific FSM states, we check `GUIinteraction` each frame.
When the game sets it to "Kilju", we raycast to identify the item, check
`KiljuAlc` on its "Use" FSM, and override to "Water" if `KiljuAlc == 0`.

This approach:
- Handles unlimited instances automatically
- Doesn't require knowing which state sets the text
- Is proven (PartInspector uses the same pattern)
- Is simple and robust against game updates

### Sound Fix: FSM state injection

The sound fix requires precise timing (swap audio clip before `AudioPlay` fires),
so we inject a custom `FsmStateAction` into the relevant FSM state. This still
requires knowing the exact state name and audio source.

---

## What We Know

| Constant | Value | Status |
|----------|-------|--------|
| Object name | Likely `kilju(itemx)` | ⚠️ Needs confirmation via BottleInspector |
| FSM with KiljuAlc | Likely `Use` | ⚠️ Needs confirmation via BottleInspector |
| KiljuAlc type | `FsmFloat` | ✅ Confirmed |
| KiljuAlc scope | Local FSM variable | ✅ Confirmed (on PlayMakerFSM section) |
| KiljuAlc values | `0` = water, `>0` = kilju | ✅ Confirmed |
| GUIinteraction | Global `FsmString` | ✅ Confirmed (PlayMakerGlobals) |
| Object tag | `ITEM` | ✅ Confirmed |

## What We Still Need

| Constant | What it is | Status |
|----------|-----------|--------|
| `BottleObjectName` | Exact game object name | ⚠️ Needs BottleInspector run |
| `BottleFsmName` | FSM name containing KiljuAlc | ⚠️ Likely "Use" — needs confirmation |
| `DrinkSoundStateName` | State that plays drinking sound | ❌ TODO (sound fix only) |
| `WaterClipSource` | Where to find water drink sound | ❌ TODO (sound fix only) |

---

## Investigation Steps

### Quick method: Use BottleInspector

1. Add `BottleInspector.cs` to the project alongside `WaterIsNotKiljuMod.cs`
2. Build and install both mods
3. Launch MSC with a plastic bottle in your save
4. Open the MSCLoader console (`~` key)
5. Search for `[BottleInspector]` lines — it dumps:
   - Exact game object names and paths
   - All FSMs, variables, states, and actions
   - Which objects have the `ITEM` tag
6. It also logs `GUIinteraction` value changes for 5 seconds
   — look at a water bottle in-game to see what text appears
7. Remove `BottleInspector.cs` once you have the info

### Manual method: Developer Toolkit

1. Launch MSC with Developer Toolkit
2. Search for `kilju` or `juiceconcentrate` in the object hierarchy
3. Note the exact game object name (e.g., `kilju(itemx)`)
4. Click the `?` next to the object → PlayMakerFSM tab
5. List all FSMs on the object — look for one named `Use`
6. In the `Use` FSM, confirm `KiljuAlc` is listed as a Float variable
7. Browse states to find ones with `AudioPlay` actions (for sound fix)

---

## Code Tour

| File | Purpose |
|------|---------|
| `WaterIsNotKiljuMod.cs` | Main mod — text fix (Update loop) + sound fix (FSM injection) |
| `BottleInspector.cs` | Temporary debug mod — dumps all FSM info |
| `WaterIsNotKilju.csproj` | Build config — references to game DLLs |
| `install.sh` | Build + copy to game Mods folder |
| `README.md` | This file |

### How the text fix works

1. Each frame in `Mod_OnUpdate()`, check if `GUIinteraction == "Kilju"`
2. If so, raycast to find what the player is looking at
3. Check if the object name contains "kilju" (the bottle pattern)
4. Get the `Use` FSM on that object and read `KiljuAlc`
5. If `KiljuAlc == 0`, override `GUIinteraction` to `"Water"`

### How the sound fix works (when configured)

1. At load time, find all bottle objects and their "Use" FSMs
2. Find the state that plays the drinking sound
3. Append a `DrinkSoundFixAction` to that state
4. When the state runs, our action checks `KiljuAlc`:
   - If `== 0` → swaps `AudioSource.clip` to the water sound
   - If `> 0` → leaves the clip unchanged

---

## Troubleshooting

| Symptom | Likely Cause |
|---------|-------------|
| Mod loads but doesn't correct text | `IsBottleObject()` not matching the game object name — check console |
| `[BottleInspector] No bottle objects found` | No plastic bottle in the current save |
| Wrong text still shows | `BottleFsmName` wrong — KiljuAlc FSM isn't "Use" on your object |
| Sound plays kilju even with water | Sound fix not configured — need `DrinkSoundStateName` |
| Mod doesn't appear in MSCLoader | DLL not in `Mods/WaterIsNotKilju/WaterIsNotKilju.dll` |

Check the MSCLoader console (`~` key) for `[WaterIsNotKilju]` log lines.
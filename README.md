# Water Is Not Kilju

A My Summer Car mod that makes plastic bottles correctly display **"Water"**
instead of "Kilju" in the hover/helper text when they're filled with water.

## Prerequisites

- My Summer Car installed and running (via Proton on Linux is fine)
- [MSCLoader](https://github.com/piotrulos/MSCModLoader) installed
- [MSCToolset](https://www.nexusmods.com/mysummercar/mods/710) installed (for investigation)
- .NET SDK for building (`dotnet build`)
- VS Code with C# extension (recommended)

## Build & Install

1. Edit `WaterIsNotKilju.csproj` — set `GameDir` to your MSC install path:
   ```
   ~/.steam/steam/steamapps/common/My Summer Car
   ```
   If running via Proton, the path may be inside the compatibility prefix. Check
   `~/.steam/steam/steamapps/compatdata/516750/pfx/drive_c/...`

2. Build:
   ```bash
   cd forge/WaterIsNotKilju
   dotnet build
   ```

3. Copy the output DLL to your mods folder:
   ```bash
   cp bin/Debug/net46/WaterIsNotKilju.dll \
     "$GameDir/Mods/WaterIsNotKilju/WaterIsNotKilju.dll"
   ```

4. Launch MSC and check the MSCLoader console (`~` key) for `[WaterIsNotKilju]` messages.

---

## ⚠️ Before the mod will work: MSCToolset Investigation

The mod code contains **TODO placeholders** that must be filled in by inspecting
the game's live FSMs. This is the critical step — you need to run MSC with
MSCToolset and discover how the plastic bottle actually works internally.

### Step 1: Find the bottle game object

1. Launch MSC with MSCLoader + MSCToolset
2. Load a save that has a plastic bottle (empty, with water, or with kilju)
3. Press **Ctrl+Z** to open the MSCToolset inspector
4. In the search bar, type `plastic` or `bottle` and look for the bottle object
5. Write down the **exact game object name/path**

   Common patterns: `BottlePlastic`, `ITEMS/BottlePlastic`, etc.

   ➡️ **Update `BottleObjectName` in `WaterIsNotKiljuMod.cs`**

### Step 2: Identify the FSM that handles hover text

1. Click the orange `?` next to the bottle object in MSCToolset
2. Switch to the **PlayMakerFSM** tab
3. You'll see one or more FSMs listed. Common names: `Data`, `Interaction`, `States`, `Use`
4. For each FSM, click **Show States** and look through the state actions for one that:
   - Sets `GUIinteraction` (a global string variable)
   - Or calls something like `SetFsmString` on `GUIinteraction`
5. Also look for states triggered by raycast/look-at — these are the "interaction" FSMs

   ➡️ **Update `BottleFsmName` with the FSM that sets hover text**

### Step 3: Find the state that writes the text

1. In the FSM from Step 2, go through each state's Actions
2. Look for an action like `SetFsmString` or `SetProperty` that sets
   `GUIinteraction` to "Kilju"
3. Note the **state name** where this happens

   ➡️ **Update `SetTextStateName`**

### Step 4: Find the content-tracking variable

This is the most important discovery. You need to figure out how the game
knows the bottle contains water vs kilju. Look at the bottle's FSM variables:

1. Click **Show Variables** on the bottle's FSM(s)
2. Look for a variable that changes based on content. It could be:
   - An `FsmString` with values like `"water"`, `"kilju"`, `"empty"`
   - An `FsmInt` (e.g., 0=empty, 1=water, 2=kilju)
   - An `FsmBool` (true=kilju, false=water, or vice versa)
   - A variable on a *different* FSM on the same object
3. To verify, pick up two bottles (one water, one kilju) and check whether
   the variable differs between them

   ➡️ **Update `ContentVarName` with the variable name**

   ➡️ **Update `WaterContentValue` with the value that means "water"**

   Also update the **variable type check** in `ContentCheckAction.OnEnter()` —
   the code has commented examples for FsmString, FsmInt, FsmBool. Uncomment
   the one that matches and remove the default.

### Step 5: Handle dynamic bottle instances (IMPORTANT)

MSC may clone bottle objects at runtime (each bottle in the world could be a
separate instance). If bottles are instantiated from a prefab:

- Our `HookBottleFsm()` only hooks **one** object found by `GameObject.Find()`
- We may need to hook **all** bottle instances, or hook the prefab so clones
  inherit the modification
- Check in MSCToolset: are there multiple bottle objects? Does `GameObject.Find()`
  find the right one?

If multiple instances exist, we'll need to iterate:
```csharp
// Potential approach for multiple instances:
foreach (var bottle in GameObject.FindObjectsOfType<PlayMakerFSM>())
{
    if (bottle.FsmName == BottleFsmName && bottle.gameObject.name.Contains("BottlePlastic"))
    {
        // Hook each one
    }
}
```

This is something to evaluate after the initial investigation. The current code
may work fine if the hover text is set via global variable (which it likely is).

---

## Code Tour

| File | Purpose |
|------|---------|
| `WaterIsNotKiljuMod.cs` | Main mod — hooks FSM, corrects hover text |
| `WaterIsNotKilju.csproj` | Build config — reference paths to game DLLs |

### Key method: `HookBottleFsm()`

This is where the magic happens:

1. Finds the bottle game object by name
2. Locates the FSM that handles interaction text
3. Finds the specific state that sets the hover label
4. Appends a `ContentCheckAction` to that state's action list

The `ContentCheckAction` runs **after** the game's original actions. If the
game just set `GUIinteraction = "Kilju"` but the content variable says water,
we overwrite it to `"Water"`.

This approach (appending to an existing state) is the least intrusive — we
don't replace or modify any original game actions, we just add a post-check.

---

## Troubleshooting

| Symptom | Likely Cause |
|---------|-------------|
| Mod loads but doesn't correct text | TODOs not filled in, or wrong variable/state names |
| `[WaterIsNotKilju] Could not find game object` | Wrong `BottleObjectName` — check MSCToolset hierarchy |
| `[WaterIsNotKilju] Could not find FSM` | Wrong `BottleFsmName` — list all FSMs on the object |
| `[WaterIsNotKilju] Could not find state` | Wrong `SetTextStateName` — list all states in the FSM |
| Error reading content variable | Variable type mismatch — update the check in `ContentCheckAction` |
| Mod doesn't appear in MSCLoader | DLL not in `Mods/WaterIsNotKilju/WaterIsNotKilju.dll` |

To debug, open the MSCLoader console in-game (`~` key) and look for
`[WaterIsNotKilju]` log lines.
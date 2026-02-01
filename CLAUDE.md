# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run

```bash
dotnet build              # Build the project
dotnet build -c Release   # Build for release
dotnet run                # Run the application
```

The project targets .NET 10.0 (Windows) and requires Windows Forms support.

## Architecture Overview

TBot is a Tibia game automation bot built with C# and Windows Forms. It uses DirectX 11 screen capture, process memory reading, and OpenCV for vision.

### Core Loop

The bot runs a ~45ms tick loop in `BotController`:
1. Capture screen frame via DirectX 11 (`CaptureService`)
2. Read game memory for entities (`MemoryReader`)
3. Analyze vision (mana)
4. Update `BotContext` state
5. Evaluate and execute tasks via `TaskOrchestrator`
6. Send input (keyboard/mouse)

### Key Components

**State Layer** (`State/`)
- `BotContext` - Central state container (player position, creatures, corpses, health, mana, cached templates)
- `BotRuntime` - Couples `BotContext` with `BotServices`
- `BotServices` - Service container (Mouse, Keyboard, Memory, Capture, PathRepo, MapRepo)
- `ProfileSettings` / `ProfileStore` - User configuration persistence (JSON in `Profiles/`)

**Task System** (`Tasks/`)
- `BotTask` - Base for root tasks with lifecycle: `OnStart()`, `Execute()`, `OnComplete()`
- `SubTask` - Simpler base for subtasks with `Failed` property
- `TaskOrchestrator` - Single root task scheduler with priority-based preemption
- `TaskPriority` - Constants: Attack (100) > Loot (60) > Heal (55) > FollowPath (50)
- Tasks with `IsCritical = true` cannot be preempted
- Call `Complete()` from `Execute()` when done - single completion mechanism

**Root Tasks** (`Tasks/RootTasks/`) - Scheduled by orchestrator
- `AttackClosestCreatureTask` - Target selection and combat
- `LootCorpseTask` - Opens corpses, loots gold, drops floor loot, handles bags
- `CastLightHealTask` - Healing spell (F1/F2 key)
- `FollowPathTask` - Waypoint navigation

**SubTasks** (`Tasks/SubTasks/`) - Used by root tasks
- `WalkToCoordinateTask`, `WalkToCreatureTask` - A* pathfinding movement
- `StepDirectionTask` - Single-tile movement for stairs/ramps
- `RightClickInTileTask`, `UseItemOnTileTask` - Tile interactions
- `OpenNextBackpackTask` - Inventory management

**Memory Reading** (`MemClass/`)
- `MemoryReader` - Reads game process memory via Win32 P/Invoke
- `MemoryAddresses` - Hardcoded offsets (game version specific)
- `RawEntity` - Marshaled memory structure for entities

**Input Control** (`Control/`)
- `KeyMover` - Arrow/numpad key input for movement
- `MouseMover` - Bezier-curved mouse movement with random jitter for human-like behavior

**Vision** (`Vision/`)
- `ManaAnalyzer` - Extracts mana percentage from screen
- `ItemFinder` - Template matching for loot items using OpenCV
- Creature detection and health bar analysis

**Navigation** (`Navigation/`)
- `AStar` - A* pathfinding on minimap data
- `PathRepository` - Waypoint save/load (JSON in `Paths/`)
- `MapRepository` - Floor minimap images (0-15)
- `Waypoint` types: Move, Step, RightClick, UseItem

### Directory Layout

```
Assets/           # Template images (copied to output)
  Loot/           # Gold stack templates
  Food/           # Food item templates
  Tools/          # Rope, shovel, backpack
  Minimaps/       # Floor maps (map_color_floor_N.png, map_cost_floor_N.png)
Profiles/         # User profile JSON files (source)
Paths/            # Saved waypoint paths
bin/Debug/net10.0-windows/Profiles/default.json  # Runtime profile (actual config used)
bin/Debug/net10.0-windows/Paths/                 # Saved waypoint paths (JSON)
```

**Note:** Legacy vision-based creature detection files exist in `Vision/CreatureDetection/`, `Vision/Minimap/`, `Vision/GameWindow/` but are unused (now using memory reading). See `.claudeignore`.

### Adding a New Task

**Root Task** (scheduled by orchestrator):
1. Create class in `Tasks/RootTasks/` extending `BotTask`
2. Implement `Priority` and `Execute()` (optionally `OnStart()`, `OnComplete()`)
3. Call `Complete()` or `Fail()` from `Execute()` when done
4. Add evaluation logic in `BotBrain.EvaluateAndSetRootTask()`

**SubTask** (used by parent tasks):
1. Create class in `Tasks/SubTasks/` extending `SubTask`
2. Implement `Execute()` (optionally `OnStart()`, `OnFinish()`)
3. Call `Complete()` or `Fail(reason)` from `Execute()` - parent checks `Failed` property

### Memory Address Updates

When game client updates break the bot, update offsets in `MemClass/MemoryAddresses.cs`. The Y-coordinate normalization offsets in `MemoryReader.NormalizeCoordinates()` are Z-level dependent.

### Dependencies

- OpenCvSharp4 - Image processing and template matching
- ScreenCapture.NET.DX11 - DirectX 11 screen capture
- WindowsInput - Keyboard/mouse simulation
- 1680 x 1050 screen resolution

# Tibia Game Mechanics

## Coordinate System
- World uses absolute grid coordinates
- Z-levels: 7 = ground, 8-12 = underground
- Memory coords need Z-based Y-offset normalization (varies by floor)
- Minimap PNGs used for walkmap: 1 pixel = 1 tile

## Movement
- 8-directional (NSEW + 4 diagonals)
- Diagonals cost 3x cardinals in pathfinding
- Stairs/ramps require directional step (not just walk-to)
- Blocked by: walls, creatures, some furniture or stacks of furniture

## Combat
- Click creature to attack (sets red square in client)
- Must be adjacent (8 neighbors including diagonals)
- Creatures can push/block player
- Attack fails if creature moves before click registers

## Items & Looting
- Corpses appear on creature death
- Must right-click corpse to open
- Ctrl+drag to loot items to backpack
- rope (climb up) items block rope spot
- shovel open hole to walk down

## Vision System
- Mana bar: Blue pixels in specific UI region
- walkmap: Yellow = unwalkable, Grayish = walkable
- Loot detection: Template matching on corpse contents

## Player
- Has health if reaches 0 you die
- Has mana for spells, executed by hotkeys or typing spells in chat 
- Can carry items in backpack inventory
- has capacity limit 
- Can use items (food, rope, shovel) from inventory
- Can move by arrow keys or numpad keys or clicking tiles 
- Can be blocked by creatures or some items
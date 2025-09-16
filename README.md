# OilLeak - The Game

A dark satire physics game about the futility of stopping corporate environmental disasters, inspired by the 2010 Deepwater Horizon oil spill.

## Quick Start

1. Open project in Unity 2022.3 or later
2. Open `Assets/Scenes/GameScene.unity`
3. Press Play

## Current Status

⚠️ **Under Active Development** - Core mechanics complete, game flow being implemented

### Working Features
- ✅ Oil particle system erupting from ocean floor
- ✅ Boat controller with left/right movement
- ✅ Physics-based item dropping system
- ✅ Collision detection for blocking oil
- ✅ Scoring system with efficiency grades
- ✅ Inventory management with weight limits

### In Progress
- 🔄 Main menu and game flow
- 🔄 Restart functionality
- 🔄 WebGL optimization
- 🔄 Progressive difficulty

## Project Structure

```
Assets/
├── Scripts/
│   ├── Player/          # Boat control and player profile
│   ├── OilLeak/         # Oil particle system and leak mechanics
│   ├── Inventory/       # Item management and physics
│   ├── Scoring/         # Score calculation and grades
│   ├── Timer/           # Round timing system
│   └── UI/              # Interface controllers
├── Prefabs/             # Reusable game objects
├── Materials/           # Shaders and materials
├── Models/              # 3D assets (boat, items, creatures)
└── Scenes/              # Game scenes
```

## Architecture Overview

The game uses ScriptableObjects for data management and an event-driven architecture:

- **GameController**: Singleton managing game state
- **OilController**: Handles particle emission and collision
- **InventoryController**: Manages available items and dropping
- **ScoringManager**: Calculates efficiency and assigns grades
- **PlayerController**: Handles boat movement and input

## Development Setup

### Requirements
- Unity 2022.3 LTS or later
- WebGL build support module
- 4GB RAM minimum

### Build Settings
1. File → Build Settings
2. Platform: WebGL
3. Compression: Brotli
4. Memory Size: 512MB

## Testing

Currently no automated tests. Manual testing checklist:

- [ ] Game starts when Play pressed
- [ ] Boat moves left/right with arrow keys
- [ ] Items drop when selected and spacebar pressed
- [ ] Oil particles blocked by dropped items
- [ ] Score increases for blocked particles
- [ ] Game ends when timer runs out
- [ ] Final grade displayed correctly

## Known Issues

- Cannot restart game after round ends
- No main menu to start game
- Some ScriptableObject references may not be connected
- WebGL build not yet optimized

## Contributing

This is a personal project with a specific vision. Not currently accepting contributions.

## License

Copyright 2024. All rights reserved.

## Background

This game was inspired by the creator's experience as an activist during the 2010 Deepwater Horizon oil spill, where they organized thousands through "Save The Gulf" and personally delivered supplies to Jefferson Parish, Louisiana.

The game's dark humor and futility are intentional - you cannot stop the leak, only delay it. Just like reality.

## Links

- Game Vision Document: [GAME_VISION.md](GAME_VISION.md)
- Technical Documentation: [TECHNICAL.md](TECHNICAL.md) (coming soon)
- Web Build: Coming soon to itch.io

---

*"How long can you hold back the inevitable?"*
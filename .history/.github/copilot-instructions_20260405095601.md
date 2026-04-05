# Project Guidelines

## Code Style
C# code follows standard Godot C# practices. Key examples:
- [bin/battlefield_.cs](bin/battlefield_.cs) for main game logic
- [bin/cardBase_.cs](bin/cardBase_.cs) for card entity model

## Architecture
Core components:
- battlefield_: Game orchestrator and turn manager
- cardBase_: Card/unit entity with traits system
- ResourceManager: Singleton for asset caching
- place_: Board tile/slot
- Bullet: Attack animations
- End: Screen effects overlay

Key decisions: Singleton pattern for global state, INI configs for runtime settings, Flags enum for card traits, deck-as-health mechanic.

## Build and Test
Build command: `dotnet build`
No automated tests present.

## Conventions
- Class names with trailing underscore (_) indicate core logic (e.g., cardBase_, battlefield_)
- INI configuration files in bin/ directory using custom IniHandler
- Trait system using [Flags] enum with bitwise operations
- Asset paths hard-coded as strings (e.g., "res://cards/...")

See [docs/UnitTraitsGuide.md](docs/UnitTraitsGuide.md) for trait implementation details.
See [rules.txt](rules.txt) for game mechanics.

Potential pitfalls: Hard-coded paths risk breakage on rename; partial classes require careful management; tween synchronization issues; trait interaction edge cases.
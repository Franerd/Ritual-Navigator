# Ritual Navigator

Ritual Navigator lets players revisit Green Hell's four Story rituals through the game's native altars instead of forcing dream scenes directly.

The mod preserves the normal ritual flow: the player teleports near the selected altar, gathers the required ingredients, lights the fire, prepares the brew, and triggers the sequence through the game itself.

## Features

- Story-mode-only ritual menu opened with `F5`.
- Navigation to all four native ritual altars.
- Progression-aware access: Ritual 3 appears after Ritual 2 is completed, and Ritual 4 appears after Ritual 3.
- Dedicated navigation to the partially burned backpack required to unlock the first Ayahuasca recipe and map.
- Optional material drop beside the player, avoiding full-backpack problems.
- Lit torch delivery with a 30-second protection window, followed by normal fuel consumption.
- Safe return to the previous position.
- Native Green Hell cursor handling and player-input blocking while the menu is open.
- English interface with a lightweight, nearly transparent visual style.

## Controls

| Key | Action |
| --- | --- |
| `F5` | Open or close the ritual menu |
| Mouse | Select and activate menu options |
| Arrow keys + `Enter` | Keyboard menu navigation |
| `Esc` | Close the menu |
| `Shift` + `F7` | Return to the saved pre-teleport position |
| `F7` | Safely stop an active dream sequence |

`F9` and `F10` are intentionally left untouched because GHMod uses them.

## Installation

1. Install GHMod for Green Hell.
2. Place `RitualNavigator_0.5.1.ghmod` in the GHMod mods directory.
3. Enable the mod in GHMod.
4. Start or load a Story-mode save.

The `F5` menu is blocked outside active Story gameplay and while the game is paused.

## Ritual 1 prerequisite

The first ritual requires the Ayahuasca document found in the partially burned backpack. Use **Ritual 1 Prerequisite - Burned Backpack**, read the document, and then return to the first altar.

## Material delivery

The material option drops the following items on the ground to the player's right:

- 1 Banisteriopsis vine
- 1 Psychotria berry
- 1 torch
- 8 sticks
- 6 small sticks

The action requires confirmation to avoid accidental item creation. The torch arrives lit and temporarily protected for 30 seconds; after that it behaves like a normal torch and consumes fuel normally.

## Scope and safety

- The mod does not mark rituals as completed.
- It does not force progression variables.
- It does not permanently unlock recipes.
- It does not provide an infinite-burning torch.
- Ritual completion remains controlled by Green Hell.

## Compatibility

Version 0.5.1 was developed and tested in Green Hell Story mode with GHMod. Multiplayer and non-Story modes are intentionally outside the supported scope.

## License

Copyright (c) 2026 FraNerd. All rights reserved. See [LICENSE](LICENSE).

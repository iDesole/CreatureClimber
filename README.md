# Creature Climber

Mobile climb game. Unity 6, URP 2D, portrait 1080×2400.

<p align="center">
  <img src="Assets/Art/Sprites/Creatures/Frog.png" height="88" alt="Frog"/>
  <img src="Assets/Art/Sprites/Creatures/Fox.png" height="88" alt="Fox"/>
  <img src="Assets/Art/Sprites/Creatures/Otter.png" height="88" alt="Otter"/>
  <img src="Assets/Art/Sprites/Creatures/Owl.png" height="88" alt="Owl"/>
  <img src="Assets/Art/Sprites/Creatures/Panda.png" height="88" alt="Panda"/>
</p>

Tap left or right to hop between platforms. Six modes, Easy/Hard difficulty, creature loadout, shop, and local save.

## Modes

| Mode | Rules |
| --- | --- |
| Classic | Mixed solid and breakable pads |
| Blackout | Classic plus brief screen blackouts |
| Race | First to a finish height against an AI climber |
| Combatant | Dodge flying hazards between pads |
| Time Trial | Score before the clock hits zero (90 / 120 / 200 s) |
| Tempo | Auto-scrolling camera; stay above the waterline |

Hard mode: every pad is breakable.

## Features

- 20 playable creatures with jump, fall, and score stats
- Platform skins and backgrounds via ScriptableObject databases
- Coin shop and unlocks
- Audio, haptics, and display settings
- Local `PlayerPrefs` save, flushed on app pause

## Controls

| Input | Action |
| --- | --- |
| Left half of screen, **A**, **←**, gamepad left | Jump left |
| Right half of screen, **D**, **→**, gamepad right | Jump right |
| **Esc** | Pause |

## Build

Unity **6000.0.38f1**.

1. Open this folder in Unity Hub.
2. Open `Assets/Scenes/SampleScene.unity`.
3. If the scene has no game objects, **Creature Climb → Build Game Scene**.
4. Play.

`Library/`, `Temp/`, `Logs/`, and generated IDE files are gitignored.

## Structure

```
Assets/Art/Sprites/       Creatures, platforms, backgrounds, combatants, UI
Assets/Data/Resources/    ScriptableObject databases
Assets/Input/             Input System actions
Assets/Scenes/            Play scene
Assets/Scripts/Runtime/   Game code (CreatureClimb.Runtime)
Assets/Scripts/Editor/    Inspectors and setup (CreatureClimb.Editor)
Assets/Settings/          URP
Packages/                 Unity packages
ProjectSettings/          Player and editor settings
Tools/                    Sprite processing
```

## License

Copyright (c) 2026 Chase Wilson. All rights reserved.

See [LICENSE](LICENSE) and [NOTICE](NOTICE).
Contact: [chasewilsonbusiness@gmail.com](mailto:chasewilsonbusiness@gmail.com)

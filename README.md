# Roguelike Pang (working title: Bubblewright)

A prototype of a roguelike game inspired by Pang (bubble-splitting arcade action), built in **Unity 2D (URP)**.

Platforms, in priority order: Mobile, then Desktop, then Console.

## Goal of this prototype

Nail the design, animations and movement before building the polished game in a separate project.

Plan:
1. Visual direction and design sheet (done: [design sheet](https://claude.ai/artifact/1gTaySmyLvPqnkRrDUxyZ5))
2. Repo and movement prototype (this)
3. Animation and game-feel pass
4. Thin roguelike loop (procedural stages, permadeath, upgrades between levels, meta-progression)

## What step 2 contains

One stage of World 1 (Rooftops) built entirely from code, using the numbers from the design sheet:

- **Wick** runs on the floor only (no jump): 9 u/s top speed, reached in 0.06 s, stops in 0.04 s. He plays with his back to the camera, faces it at stage start, turns with a 0.2 s squash, and turns back to celebrate after the last pop. His candle flame shows his 3 hearts.
- **Harpoon**: one shot at a time, straight up from the launcher tip (+0.35 u right of centre) at 20 u/s. A 1 px twisted steel cable stays where it was fired; a bubble touching the cable or the barbed tip pops.
- **Bubbles**: kinematic, with our own bounce maths (no physics engine). Constant 4 u/s sideways. Each tier always peaks at its height from the sheet (XL 13, L 11, M 8.5, S 6 u). They split into the next size (Trigloops into 3), kicked up and out. No eyes: type reads from colour plus a centre mark.
- **Screen**: renders at 480 x 270 with the URP Pixel Perfect Camera (15 px per unit, upscale render texture). The arena is always 32 x 14 u with a 4 u ground strip; wider or taller screens show extra scenery. Phones are locked to landscape.
- **Controls**: touch first, plus keyboard and gamepad (below).
- Placeholder pixel art is painted in code from the sheet's shapes and palette (`PlaceholderArt.cs`), so there are no image files yet.

Not in this step on purpose: ledges, the storm timer, other weapons, upgrades, audio, and most of the animation list (that is step 3).

## Open it on desktop

1. Install **Unity Hub**, then **Unity 6000.6.7f1**. Add the modules **Android Build Support** (with OpenJDK and SDK/NDK) and, on a Mac, **iOS Build Support**.
2. Install Git LFS once per machine: `git lfs install`. Then clone this repo.
3. In Unity Hub: **Add > Add project from disk**, pick the repo folder, open it. If Hub offers a slightly different version, accept it. The pinned editor is 6000.6.7f1.
4. On first open:
   - If Unity asks to **enable the new Input System backends and restart**, click **Yes**. (Either answer works; the code supports both.)
   - The project setup runs by itself: it creates the URP 2D renderer, the tuning asset and `Assets/Scenes/Prototype.unity`. The Console says `[Bubblewright] Project setup done`. You can re-run it any time from the menu **Bubblewright > Run Project Setup**.
5. Open `Assets/Scenes/Prototype.unity` (or **Bubblewright > Open Prototype Scene**) and press **Play**.
6. In the Game view, pick a landscape size such as **1920x1080** or **16:9**. For phone shapes, switch the Game view tab to **Simulator** and pick a phone; it simulates touch too.
7. After the first open, commit what Unity generated (`Packages/packages-lock.json`, `ProjectSettings/*`, `Assets/Bubblewright/Settings`, `Assets/Bubblewright/Resources`, `Assets/Scenes`) so the project is reproducible.

### Controls

| Action | Touch | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Move | Drag left or right anywhere in the left half (relative to where your thumb lands) | A / D or arrows | Left stick or d-pad |
| Fire (hold to keep firing) | Hold anywhere in the right half (the mint button) | Space, J, K, W or Up | A / Cross, X / Square, RT or RB |
| Play again | Tap | R, Enter or Space | Start or A |

The mouse acts as one finger, so you can try the touch controls on desktop: click and drag in the left half, click in the right half.

Debug keys on desktop: **1-4** spawn an XL Gloop, Trigloop, Lead or Lucky bubble; **F1** shows hitboxes.

### Tuning

Every number lives in `Assets/Bubblewright/Resources/Tuning.asset`. Select it and edit in the Inspector, even while playing. Bubble gravity is derived per tier from its peak height and a bounce time, so it always reaches the sheet's peak height.

## Test it on a phone

**Android** (easiest):
1. On the phone: enable Developer options and **USB debugging**, then plug it in.
2. In Unity: **File > Build Profiles**, select **Android**, click **Switch Platform**.
3. Make sure `Assets/Scenes/Prototype.unity` is in the scene list, then **Build And Run**. Pick a file name for the APK; Unity installs and launches it.

**iPhone** (needs a Mac with Xcode):
1. **File > Build Profiles**, select **iOS**, **Switch Platform**, then **Build**.
2. Open the generated Xcode project, choose your Apple ID team under Signing, plug in the phone and press Run.

Hold the phone sideways: left thumb drags to move, right thumb holds the mint button to fire.

## Project layout

```
Assets/Bubblewright/
  Scripts/Core/      Game (bootstrap and loop), Tuning (all numbers), GameInput (touch/keys/pad), Palette and sorting layers
  Scripts/Player/    Wick: movement, facing turn, hurt, procedural squash and lean
  Scripts/Bubbles/   Bubble: own bounce maths, split children
  Scripts/Weapons/   Harpoon: cable and barbed tip
  Scripts/FX/        Pop ring and droplets
  Scripts/UI/        Thumb controls drawn in the ground strip
  Scripts/Art/       PixelCanvas and PlaceholderArt: sprites painted in code at 15 px per unit
  Editor/            ProjectSetup: URP 2D renderer, tuning asset, player settings, scene
ProjectSettings/TagManager.asset   Sorting layers: Sky, Stage, Bubbles, Shots, Player, FX, UI
```

Units: 1 unit = 15 art pixels. Floor at y = 0, ceiling at y = 14, walls at x = -16 and +16. The ground strip is y = -4 to 0.

## Troubleshooting

- **Package errors on open** (a version in `Packages/manifest.json` not found): open **Window > Package Manager**, install **Universal RP** and **Input System** at the versions it offers, then run **Bubblewright > Run Project Setup**.
- **Sprites look blurry or the screen is not pixel perfect**: check that **Edit > Project Settings > Graphics** uses `Assets/Bubblewright/Settings/URP-2D.asset`, and that the Main Camera has a Pixel Perfect Camera set to 15 PPU, 480 x 270, Grid Snapping = Upscale Render Texture. Re-running the setup fixes both.
- **Nothing in the Game view before pressing Play**: expected. Everything is built when the game starts.

## Setup notes

- Unity's `.gitignore` is in place. Git LFS rules for images, audio, fonts, models, video and archives are in `.gitattributes`.
- Progress tracker: `docs/prototype-tracker.html`. Live version: https://claude.ai/artifact/1v9Jo761zKs3VKdJ1wcpE3

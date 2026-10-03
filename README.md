# Low-Poly 1v1 FPS

Unity 6 project with the included low-poly SWAT and weapon packs. The arena is a symmetric Blue vs. Red map with mirrored lanes, cover, and a central bunker.

## Online 1v1 with Unity Relay

Open `Assets/Scenes/FPSOnline.unity` and press Play. Choose **Host room** to obtain a join code; the other player opens the same scene in another build and enters that code under **Join**. The host spawns as Blue and the joining player as Red. Both players have a synchronized full-body SWAT character, weapon selection, movement, health, hit zones, firing, and respawns. This scene is first in Build Settings, so standalone builds launch into the online lobby.

Unity Services must be linked to this project and both players need internet access. A Relay room was successfully allocated from this editor on 2026-10-03. A two-machine match has not yet been tested. Rebuild this scene and its network-player prefab using **FPS > Create Relay 1v1 Scene**; this overwrites the generated scene and prefab.

## Controls

- `1`: primary weapon slot
- `2`: secondary pistol slot
- `3`: fists; left-click alternates left and right punches
- `Q` / `E`: change gun in the equipped slot (eight primary guns and two pistols)
- left-click: fire
- right-click: aim down sights (weapon-specific zoom)
- `R`: refill the active weapon's magazine
- `WASD`: move, `Shift`: sprint, `C`/`Ctrl`: crouch, `Space`: jump

Every weapon has its own movement multiplier, fire rate, range, damage, magazine, hip/aim spread, spray bloom, recoil, and aim zoom. Hit zones have head (2.4x), body (1x), and leg (0.7x) multipliers. Both sides have 100 HP and respawn after defeat. The SWAT rig has procedural idle, walk, sprint, crouch, aim, and alternating punch poses. All ten distinct firearm prefabs in the imported packs are selectable.

## Low-poly asset scene

Open `Assets/Scenes/FPSPrototype.unity` and press Play. This is a local training arena with one moving, shooting Red SWAT bot. The editable scene contains the **Blue Player > Blue SWAT Body** full-body character and a visible world rifle even before Play. At runtime, only the player's own first-person camera excludes that body; other cameras can see it. The first-person view has separate low-poly gloved hands. To regenerate the training scene, choose **FPS > Create Blue Red Arena Scene**; this overwrites the generated scene.

## Unity MCP

The MCP package is declared in `Packages/manifest.json` and the project-local client endpoint is in `.mcp.json`. Restart Unity, then open **Window > MCP for Unity**, choose **Auto-Setup**, and press **Start Bridge**. The client connects to `http://127.0.0.1:8080/mcp`.

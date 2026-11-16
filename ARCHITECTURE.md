# 燼淵 Ashen Abyss — architecture contract

Read `Y:\DESIGN.md` first (world, classes, regions, monsters). This file is the contract between modules that are
being written in parallel. If you own a module, implement exactly the public API listed for it; you may add more
members, but never rename or drop the listed ones. Other modules are compiled against these signatures.

## 0. Ground rules
- Engine: Godot 4.7.2 .NET, C# (`net10.0`, `LangVersion latest`, nullable annotations disabled), namespace `AshenAbyss`.
  Project root `Y:\game` (= `res://`). Sources in `Y:\game\src`, shaders in `Y:\game\shaders`, data in `Y:\game\data`.
- ALWAYS use `Y:\` paths (a `subst` drive; the real path is too long for many tools). If `Y:\` is missing:
  `cmd /c subst Y: "<project folder>"`.
  Prefer PowerShell over Git Bash.
- One Godot-derived class (Node/Control/Resource…) per file, file name = class name, class declared `partial`.
  Plain C# classes (data, logic, static helpers) may share files. No `.tscn` scenes except `scenes/Main.tscn`:
  build node trees in code.
- Every user-visible string is Traditional Chinese (zh-TW) and ORIGINAL to this game. Never reference or imitate
  the names, characters, text, logos or music of any existing game, film or book.
- File ownership is strict: edit only the files listed for your module. If you need a change in a file you do not
  own, do NOT edit it — describe the exact change under "INTEGRATION NOTES" in your final report.
- While modules are being written in parallel you must NOT run `dotnet build`, Godot, or the game (other agents'
  half-written files would break your build and builds would collide). Check your work by reading it carefully
  against the real signatures in the existing sources. A later integration stage compiles and runs everything.
- The game pauses through `Game.I.Paused` (the SceneTree is never paused). Every `_Process` that advances gameplay
  must start with `if (Game.I.Paused) return;`. UI code keeps running.
- Gameplay happens on the XZ plane at y = 0. Logic uses `Vector2 (x, z)`; convert with `World.To3` / `World.To2`.
  An actor facing angle 0 looks toward +Z; `Actor.Forward`.
- Logging: `Log.Info/Warn/Error(category, message[, exception])`, `Log.Debug` for verbose. Log every state change a
  future debugger would want (area loads, saves, boss phases, purchases, errors). Wrap risky callbacks with
  `Log.Guard(category, () => …)`. Never let an exception escape `_Process`.
- Performance target: 60 fps at 1440p internal on an integrated Radeon 8060S. No per-frame allocations in hot
  loops, share materials/meshes, use MultiMesh for repeated props, keep shadow-casting lights to a handful.
- Blender (asset agents only): NEVER start `blender.exe` directly. Always
  `& Y:\tools\blender\run_blender.ps1 -A @('-b','--factory-startup','--python','<script.py>','--','<args>')`
  (it limits the machine to 2 Blender processes, caps threads and keeps the PC awake; too many parallel Blender
  jobs hung this laptop before). Do not start more than one Blender run at a time from your own session.

## 1. Existing code (read it; do not modify unless you own it)
| file | what it is |
|---|---|
| `Defs.cs` | enums `ClassId` (8), `Element` (6), `Rarity`, `ItemKind`, `EquipSlot`, `SkillCat`, `Faction`, `Stat`; `StatInfo`, `StatBlock`, `Names` (class names/keys/colours/resources), `Palette` (rarity/element/UI colours), `Rng` |
| `Log.cs`, `WinKeys.cs` | file logger; PrtSc low-level hook (`WinKeys.Start/Stop/ConsumeScreenshotRequest`) |
| `Items.cs` | `Item`, `Affix`, `ItemDb` (bases, affix `Pools`, `Aspects`, `Uniques`, `Gems`, `Generate`, crafting costs), `ItemText.Tooltip` (BBCode) |
| `SkillDef.cs`, `SkillsData.cs` | declarative skills (`SkillDef`, `SkillUp`, `BuffDef`, `ZoneSpec`, `StatusId`, `St`), `SkillDb` (64 skills, passives, `ClassSpec`s) |
| `MonstersData.cs` | `MonsterDef`, `MonsterDb`, `EliteAffix` list, `RegionDef`/`DungeonDef`/`ZoneDb` (7 regions × 3 dungeons), `DungeonGoal`, `Balance` |
| `Save.cs` | `SaveData` (partial), `SaveSystem` (8 slots), `SlotInfo`, `Settings` |
| `Hero.cs` | `Hero` (partial): stats (`Recalc`, `S`), buffs, skill points/upgrades/respec, spec, XP/paragon levels, inventory, equip, salvage/upgrade/enchant/imprint/sockets |
| `World.cs` | `NavGrid` (2 m cells, obstacles, collision, LOS, A*), `World` registry (`P`, `Monsters`, `Minions`, `Grid`, `Root`, queries, `Damage`/`MonsterKilled` events, `Event`/`Emit`, `FootstepSfx`) |
| `Actor.cs`, `Combat.cs` | actor base (statuses, barrier, hits), damage formulas |
| `Player.cs`, `SkillExec.cs` | hero controller, cast state machine, hooks; runs all skills from data |
| `Monster.cs` | monster/minion AI by role, elites, stagger, death |
| `Projectile.cs`, `GroundZone.cs`, `Interactable.cs` | shots, ground areas/traps, clickable world objects |
| `CharModel.cs` | loads `res://assets/chars/<id>.glb`, shared animation library, char shader, tint/flash/dissolve |
| `Talents.cs` | OLD single talent board — to be replaced by the paragon module (which deletes it) |
| `shaders/` | `char`, `spectral`, `fx_ring`, `fx_telegraph`, `fx_zone`, `fx_slash`, `fx_flash`, `fx_beam` |

`World.Emit(event, arg)` events (emitters → listeners such as quests, objectives, renown, HUD):
`area`(areaId) · `kill`(monsterDefId) · `elite`(defId) · `boss`(bossDefId) · `miniboss`(dungeonId) ·
`dungeon`(dungeonId, on completion) · `stronghold`(regionId) · `altar`(altarId) · `event`(regionId) ·
`talk`(npcId) · `waypoint`(areaId) · `levelup`(level) · `rift`(tier) · `death`("") · `objective`("") (progress changed).

## 2. Modules, owners and files
| key | files it owns | summary |
|---|---|---|
| spine | `Game.cs`, `CameraRig.cs`, `Gfx.cs`, `TestBot.cs`, `scenes/Main.tscn`, `project.godot`, `AshenAbyss.csproj` | bootstrap, state flow, saving, pause, screenshot, graphics settings, test harness |
| fx | `Fx.cs` (+ new `shaders/fx_*.gdshader`) | all visual effects |
| audio | `Snd.cs`, `AudioMan.cs` | sound effects, music, ambience, voice playback |
| levelgen | `LevelGen.cs` | procedural layouts → `LevelData` |
| levelrender | `LevelThemes.cs`, `LevelBuilder.cs`, `shaders/env_*.gdshader` | themes, terrain/wall/prop meshes, lighting, weather |
| levelplay | `Level.cs`, `Populate.cs`, `WorldObjects.cs` | the level node, monster packs, interactables, objectives, events, town, rift |
| bosses | `EliteFx.cs`, `BossBrain.cs` | elite affix behaviour, boss fights |
| loot | `Loot.cs` | drops, ground loot, chests, vendor stock, gambling |
| story | `Quests.cs`, `Dialogue.cs`, `data/vo_lines.json` | campaign, side quests, renown, dialogue, voice script |
| paragon | `Paragon.cs`, `Hero.Paragon.cs`, `ParagonUi.cs` (deletes `Talents.cs`) | endgame boards, glyphs + their UI |
| ui-core | `UiTheme.cs`, `Icons.cs`, `Ui.cs`, `UiPanel.cs`, `Hud*.cs` | theme, panel manager, tooltips, the whole HUD |
| ui-menu | `MainMenu.cs`, `PauseMenu.cs`, `SettingsUi.cs`, `DeathUi.cs`, `CreditsData.cs` | title screen, character creation, slots, settings |
| ui-items | `InventoryUi.cs`, `VendorUi.cs`, `Hero.Crafting.cs`, `Crafting.cs` | inventory, stash, all vendors, tempering |
| ui-skills | `SkillTreeUi.cs` | skill tree, upgrades, passives, spec, bar assignment |
| ui-world | `MapUi.cs`, `DialogueUi.cs`, `QuestLogUi.cs`, `WorldTierUi.cs`, `RiftUi.cs` | maps and travel, dialogue, quest log, world tier, rift |

## 3. APIs

### 3.1 spine — `Game` (Node, root of `scenes/Main.tscn`)
```csharp
public partial class Game : Node {
    public static Game I;
    public Settings Cfg;
    public Hero Hero;                       // null in the menu
    public int Slot;
    public Player Player; public Level Level; public CameraRig Cam;
    public Hud Hud; public Ui Ui; public AudioMan Audio;
    public Node3D WorldRoot;                // parent of the current Level
    public CanvasLayer UiLayer;
    public enum GameState { Boot, Menu, Loading, Play }
    public GameState State;
    public int PauseLocks;                  // Ui increments while a pausing panel is open
    public bool Paused => State != GameState.Play || PauseLocks > 0;
    public bool UiBlocksMouse { get; }      // => Ui != null && (Ui.AnyOpen || Ui.MouseOverUi)
    public double Time;                     // unpaused play seconds since launch
    public int WorldTier => Hero?.D.Tier ?? 0;
    public event Action<string> AreaEntered;
    public void NewGame(int slot, string name, ClassId cls, bool hardcore);
    public void LoadGame(int slot);
    public void EnterArea(string areaId, string entry = null);  // loading screen, save, build Level, place player, music
    public void SaveNow(string reason);
    public void ToMenu();                   // save and return to the title screen
    public void Quit();
    public void OnLevelUp(int level);       // called by Player: HUD fanfare, autosave, World.Emit("levelup")
    public void OnPlayerDied();             // death screen; hardcore → mark save dead
    public void Respawn();                  // at the level's checkpoint (or town)
    public void TakeScreenshot();           // PNG to Pictures\AshenAbyss, toast, sound, log
    public void ApplySettings();            // window mode, vsync, volumes, graphics
    public void HitStop(float seconds);     // brief slow-motion punch, throttled
    public bool Arg(string name); public string ArgValue(string name);   // test-harness command-line switches
}
```
Area ids: `"town"`, a region id (`wastes`, `frost`, `crypt`, `mire`, `sands`, `sanctum`, `abyss` → its overworld),
a dungeon id from `ZoneDb` (e.g. `wastes_d1`, `wastes_boss`), or `"rift:<tier>"`. `entry` is the area the player
came from (so they appear at the matching entrance) or `"waypoint"`.
Autosave on: area change, level up, boss kill, quest step, every 3 minutes, quit. Window: fullscreen by default
(`Cfg.Fullscreen`), `--win` forces a 1600×900 window. PrtSc is polled every frame via
`WinKeys.ConsumeScreenshotRequest()`; F12 also takes a screenshot.
Test switches (after `--` on the command line): `--win`, `--new=<classKey>`, `--load=<slot>`, `--area=<id>`,
`--level=<n>`, `--gear` (equip level-appropriate rares), `--god`, `--bot` (TestBot plays by itself),
`--ui=<panelId>`, `--shot=<path.png>`, `--shot-at=<sec>[,sec…]`, `--quit-at=<sec>`, `--mute`, `--tier=<n>`.
```csharp
public partial class CameraRig : Node3D {       // isometric follow camera: pitch ≈ 52°, FOV 30°, distance ≈ 24 m × Zoom
    public Camera3D Cam;
    public float Zoom;                          // 0.7 … 1.6, mouse wheel
    public void Follow(Node3D target); public void Snap();
    public void Shake(float amount, float seconds);
    public void SetZoomTarget(float zoom, float seconds);   // e.g. 1.5 for bosses, 0.85 in town
}
public static class Gfx {
    public static void Apply(Settings s, Viewport vp);                 // render scale (FSR), AA, shadow atlases
    public static void TuneEnvironment(Godot.Environment env, Settings s);  // SSAO/SSIL/volumetric fog/glow/SDFGI by quality
    public static float AutoScale(Vector2I screen);                    // target ≈ 1440p internal
}
```

### 3.2 fx — `public static class Fx`
All positions are world space. Effects free themselves. Element colour comes from `Palette.Element`.
```csharp
void Burst(Vector3 pos, Element el, float radius, float intensity = 1f);   // explosion / nova: ring + particles + light flash
void Hit(Vector3 pos, Element el, bool crit);                              // small impact
void Slash(Actor a, float radius, float arcDeg, Element el);               // melee swipe at chest height, facing a.Facing
void Ring(Vector3 pos, float radius, Color c, float seconds);
void Beam(Vector3 from, Vector3 to, Element el, float width = 0.25f, float seconds = 0.2f);  // jagged for lightning
Node3D Telegraph(Vector2 pos, float radius, float delay, Color c, bool friendly = false);    // fills over delay, then frees itself
Node3D TelegraphCone(Actor a, float range, float arcDeg, float delay, Color c);
Node3D TelegraphLine(Vector2 from, Vector2 dir, float length, float width, float delay, Color c);
Node3D TelegraphRing(Vector2 pos, float inner, float outer, float delay, Color c);           // donut
void Meteor(Vector2 target, float delay, Element el, float radius);        // falling body + trail, lands after delay
Node3D ProjectileVisual(Element el, int size, float radius, string id, bool hostile = false); // size 0 small, 1 big, 2 ground wave
void ReleaseProjectile(Projectile p);                                      // detach trail so it fades, then free p
Node3D ZoneVisual(float radius, Element el, bool hostile, float seconds);
Node3D TrapVisual(Element el);
void Ash(Actor a); void Dust(Vector3 pos); void Heal(Actor a); void LevelUp(Actor a);
void Teleport(Vector3 pos, Element el);
void BuffAura(Actor a, Element el, float seconds, bool big);
void CastGlow(Actor a, Element el);
void DashTrail(Actor a, Element el, float seconds);
void Afterimage(Vector2 pos, Element el, float seconds);
Node3D RemnantWisp(Vector2 pos);                                           // floating soul wisp, lives until freed
Node3D LootBeam(Rarity r); Node3D Fire(float size, bool light); Node3D Portal(Color c, float radius);
void Shake(float amount, float seconds);                                   // → Game.I.Cam
void HitStop(float seconds);                                               // → Game.I.HitStop
Texture2D SoftDot { get; }  Texture2D NoiseTex { get; }                    // shared procedural textures
```
Hostile ground danger always uses `Palette.Danger` with a hard edge; friendly telegraphs are dimmer.

### 3.3 audio
```csharp
public static class Snd {
    void At(string name, Vector3 pos, float volume = 1f);      // positional one-shot; picks a random variation name_1..n
    void Ui(string name, float volume = 1f);                   // non-positional
    void Loop(string key, string name, Node3D follow);         // start/replace a looping sound that follows a node
    void StopLoop(string key);
}
public partial class AudioMan : Node {
    void PlayMusic(string track, float fade = 2f);             // "menu_theme", "town", "boss", "boss_final", or a zone base ("wastes" → wastes_base + wastes_combat)
    void SetCombat(float intensity);                           // 0..1, crossfades the zone's combat layer
    void PlayAmbience(string name, float fade = 2f);
    void PlayVoice(string voiceId, Action onDone = null);      // res://assets/audio/vo/<voiceId>.(ogg|wav); ducks music; missing file → onDone after a text-length delay
    void StopVoice();
    void ApplyVolumes(Settings s);
    void Mute(bool m);
}
```
Files: `res://assets/audio/sfx/<name>_<n>.wav` (n from 1), `amb/<name>.ogg`, `music/<name>.ogg`, `vo/<id>.ogg|wav`.
`res://assets/audio/manifest_audio.json` has `"groups": { "<name>": ["assets/audio/sfx/<name>_1.wav", …] }`.
Unknown names must be logged once and ignored, never thrown. Limit simultaneous instances per sound (3-4).

### 3.4 level
```csharp
public enum AreaKind { Town, Overworld, Halls, Cave, Arena }
public sealed class LevelRequest { public string AreaId, ThemeId; public AreaKind Kind; public int Seed, Size;   // Size = cells per side
    public bool Boss, Stronghold; public DungeonGoal Goal; public string[] DungeonIds; }
public sealed class Poi { public string Kind, Tag; public Vector2 Pos; public float Radius, Facing; public int Room; }
public sealed class SpawnGroup { public Vector2 Pos; public int Count, Room; public string Kind; }   // "pack","champions","elite","objective","stronghold"
public sealed class PropSlot { public Vector2 Pos; public string Category; public float Facing, Scale = 1f, Radius; public bool Blocks; }
public sealed class LevelData {
    public AreaKind Kind; public int Seed; public NavGrid Grid;
    public byte[] Deco;                 // per cell: 0 ground, 1 path/road, 2 alternate ground patch, 3 room interior, 4 arena, 5 shore (next to a pit)
    public List<Poi> Pois; public List<SpawnGroup> Spawns; public List<PropSlot> Props; public List<Rect2I> Rooms;
    public Poi Find(string kind); public IEnumerable<Poi> All(string kind);
}
public static class LevelGen { public static LevelData Generate(LevelRequest r); }
```
`NavGrid` cells: `Floor`, `Wall` (also thick forest/cliff outdoors), `Pit` (lava / water / chasm: blocks walking,
not shots). LevelGen fills the grid, calls `Grid.AddObstacle` for blocking props and `Grid.BuildPathing()` last.
Poi kinds: `start`, `exit` (Tag = area it leads to), `entrance` (Tag = dungeon id), `boss` (arena centre, Radius),
`bossdoor`, `stronghold` (centre, Radius), `waypoint`, `altar`, `chest`, `shrine`, `event`, `objective`,
`npc` (Tag = elder|smith|merchant|occultist|jeweler|villager), `stash`, `brazier`, `tierstatue`, `riftstone`.
PropSlot categories: `tree`, `rock`, `rock_big`, `clutter`, `breakable`, `furniture`, `light`, `pillar`,
`structure`, `foliage`. Guarantees: every Poi and spawn is on a walkable cell reachable from `start`; ≥ 4 m clear
around `start`; boss arenas are ≥ 11 m radius of open floor with one door; overworld ≈ 110×110 cells, dungeons
≈ 60-80 cells per side, town ≈ 50×50.
```csharp
public sealed class LevelTheme {
    public string Id, Ground, GroundAlt, Path, RoomFloor, ArenaFloor, WallTex, WallTopTex, PitKind /* lava|water|void|none */, Sky, Weather, Footstep;
    public bool Outdoor; public float WallHeight, FogDensity, AmbientEnergy, SunEnergy, TorchRange;
    public Color Ambient, FogColor, SunColor, Accent, TorchColor; public Vector3 SunDir;
    public string[] Trees, Rocks, BigRocks, Clutter, Breakables, Furniture, Lights, Pillars, Structures, Foliage;
}
public static class LevelThemes { public static LevelTheme Get(string id); public static LevelTheme ForArea(string areaId); }
public static class LevelBuilder {      // everything visible that is not an actor or interactable
    public static void Build(Level level, LevelData data, LevelTheme theme);   // terrain, walls/cliffs, pits, props, lights, WorldEnvironment (stored in level.Env), weather
    public static Node3D Prop(string modelName);                               // instance of res://assets/env/models/<name>/<name>.glb (cached), null if missing
    public static Material Surface(string texSet, float uvScale = 1f);         // cached PBR material from res://assets/env/tex/<set>/
}
```
Theme ids: `town`, `wastes`, `frost`, `barrows`, `mire`, `sands`, `sanctum`, `abyss`, `crypt`, `cave`, `graveyard`,
`icehall`, `icecave`, `wetcave`, `brasshall`, `sandtomb`, `cathedral`, `forge`, `lavacave`, `rift`.
Walls and tall props between the camera and the hero must not hide the fight: use the global shader uniform
`hero_pos` (vec3, set every frame by `Level`) to cut a soft see-through disc in occluders.
```csharp
public sealed class MapMarker { public string Kind, Label; public Vector2 Pos; public bool Visible = true; public object Ref; }
public partial class Level : Node3D {
    public string AreaId; public RegionDef Region; public DungeonDef Dungeon; public bool IsTown, IsRift; public int RiftTier, MonsterLevel;
    public LevelData Data; public NavGrid Grid; public LevelTheme Theme; public WorldEnvironment Env;
    public static Level Build(string areaId, string entry, int seed);      // sets World.Grid / World.Root, generates, builds, populates
    public Vector2 EntryPos(string entry); public Vector2 Checkpoint;       // respawn position
    public string ObjectiveText; public float ObjectiveProgress; public bool ObjectiveDone;
    public Monster Boss; public event Action Changed;
    public bool[] Seen; public List<MapMarker> Markers;                    // fog of war per cell + markers, for the minimap and MapUi
    public void Reveal(Vector2 pos, float radius);
    public float RiftTimeLeft, RiftProgress;                               // rift only
}
```
Interactables are created with small factory functions in `WorldObjects.cs` (`WorldObjects.Chest(pos, quality)`,
`Shrine`, `Altar`, `Waypoint`, `Entrance`, `Exit`, `Npc`, `Breakable`, `Idol`, `Prisoner`, `Brazier`…) that build
an `Interactable` with its model/effects and `OnUse` callback.

### 3.5 bosses
```csharp
public static class EliteFx { void Apply(Monster m); void Tick(Monster m, float dt); void OnHitTaken(Monster m, Hit h); void OnAttackHit(Monster m); void OnDeath(Monster m); }
public sealed class BossBrain { public BossBrain(Monster m); public int Phase; public void Tick(float dt); public void OnDamaged(); public void Interrupt(); public void OnDeath(); }
```
`Monster` already exposes `BossMove(goal, dt, stopAt)`, `BossIdle()`, `Busy(time, end, tick)`, `AffixT[]`,
`SpeedMul`, `AtkSpeedMul`, `DrMul`, `DmgMul`, `BlocksProjectiles`, `Stagger/Staggered`, `BaseDamage`.
Hostile damage goes through `Combat.MonsterHit` / `Combat.MonsterArea`, `Projectile.SpawnMonster`,
`GroundZone.SpawnMonster`; every dangerous attack is telegraphed with `Fx.Telegraph*` for ≥ 0.6 s.

### 3.6 loot — `public static class Loot`
```csharp
void OnMonsterDeath(Monster m);              // XP, kill counters, gold/items/globes/materials by tier, World.Emit("kill"/"elite"/"boss")
Interactable DropItem(Item it, Vector2 at);  // ground loot with rarity beam + label, pickup on click
void DropGold(int amount, Vector2 at); void DropGlobe(Vector2 at); void DropMaterials(int scrap, int dust, int cores, Vector2 at);
void OpenChest(Vector2 at, int quality);     // 0 common … 3 boss
Item Roll(int level, int quality, ClassId cls, ItemKind? kind = null);   // quality raises rarity odds
List<(Item item, int price)> VendorStock(string npcId, Hero h);         // refreshed per town visit
(Item item, int price) Gamble(ItemKind kind, Hero h);
```

### 3.7 story
```csharp
public sealed class DialogueLine { public string Speaker, Text, Voice; }
public sealed class DialogueScript { public string Id; public List<DialogueLine> Lines; public Action OnEnd; }
public static class DialogueDb { DialogueScript Get(string id); string Bark(string key); }   // barks: one-liners for HUD subtitles
public sealed class QuestDef { public string Id, Name, Giver; public bool Main; public List<QuestStep> Steps; /* rewards … */ }
public static class Quests {
    void Attach(Hero h);                     // subscribes to World.Event, reads/writes h.D.Quests / Flags / Renown
    void Detach();
    IReadOnlyList<string> Tracker();         // lines for the HUD tracker (current main step + active side quests)
    IEnumerable<QuestDef> Active(); IEnumerable<QuestDef> Completed();
    DialogueScript TalkTo(string npcId);     // what this NPC says right now (advances quests); null → open its service
    bool HasQuestFor(string npcId);          // show a marker above the NPC
    bool CanEnter(string areaId);            // act gating (wastes first; frost/crypt/mire any order; then sands, sanctum, abyss)
    int RenownTier(string regionId); int RenownPoints(string regionId);
    string NextObjectiveArea();              // for the map's quest marker
    event Action Changed;
}
```
`data/vo_lines.json`: `[{"id","speaker","voice","text"}]` for every voiced line (voice = a profile name such as
`narrator`, `elder`, `smith`, `merchant`, `occultist`, `jeweler`, `boss_<id>`, `hero_<classKey>`).

### 3.8 paragon
`Hero.Paragon.cs` implements `partial void ApplyParagonStats()` (adds board/glyph stats into `S`, adds hook ids to
`Keystones`) and the allocation API used by `ParagonUi`. Hook ids already honoured by combat code — keep them as
legendary nodes: `ks_fury`, `ks_resonance`, `ks_combo`, `ks_army`, `ks_bulwark`, `ks_cycle`. Points available =
`D.Paragon + D.BonusParagonPts − spent`; the system unlocks at `Balance.LevelCap`. Glyphs level through rift
clears (`World.Emit("rift", tier)`).

### 3.9 ui-core
```csharp
public static class UiTheme {
    Font Title, Body, Ui; Theme Theme;                         // Noto Serif TC for titles/body text, Noto Sans TC for numbers and small UI
    Color Bg, Panel, Border, Gold, Text, Dim;
    Label Label(string text, int size = 20, Color? color = null, bool title = false);
    Button Button(string text, Action onPress, int size = 22);
    RichTextLabel Rich(string bbcode, float width);
    PanelContainer Frame(bool heavy = false);                  // dark iron-and-ember styled panel
    StyleBox Box(Color bg, Color border, int borderPx = 2, int radius = 4);
    Control ItemSlot(Item it, int size, Action<Item> onLeft, Action<Item> onRight, Func<Item, string> tooltip);
}
public static class Icons { Texture2D Get(string name); Texture2D ForItem(Item it); Texture2D ForSkill(SkillDef s); Texture2D ForKind(ItemKind k, int weaponClass = -1); }
public partial class UiPanel : Control {     // base class of every panel
    public virtual string Id => ""; public virtual bool Pauses => true; public virtual bool Fullscreen => false;
    public virtual void OnOpen(object arg) { } public virtual void OnClose() { } public virtual void Refresh() { }
}
public partial class Ui : Control {
    void Register(UiPanel p);                                   // ui-core registers every panel class in its constructor
    void Open(string id, object arg = null); void Close(string id); void Toggle(string id); void CloseAll();
    bool IsOpen(string id); bool AnyOpen { get; } bool MouseOverUi { get; }
    void ShowTooltip(string bbcode, Vector2 screenPos, string compareBbcode = null); void HideTooltip();
    void Confirm(string text, Action onYes, string yes = "確定", string no = "取消");
    void Prompt(string title, string initial, Action<string> onOk);
}
public partial class Hud : Control {
    void Warn(string text); void Toast(string text, Color c);
    void AreaTitle(string name, string subtitle); void Subtitle(string speaker, string text, float seconds);
    void Flash(Color c, float seconds);                         // full-screen tint (level up, death)
}
```
Panel ids → classes: `inventory` InventoryUi · `skills` SkillTreeUi · `paragon` ParagonUi · `map` MapUi (arg
`"travel"` when opened from a waypoint) · `quests` QuestLogUi · `vendor` VendorUi (arg = `merchant|smith|occultist|
jeweler|stash`) · `dialogue` DialogueUi (arg = DialogueScript) · `pause` PauseMenu · `settings` SettingsUi ·
`death` DeathUi · `tier` WorldTierUi · `rift` RiftUi. `MainMenu` is not a panel: `Game` adds it in the Menu state.
Hotkeys handled by `Ui`: I/C inventory, K skills, P paragon, M/Tab map, J quests, Esc closes the top panel or opens
`pause`, T town portal (`Game.I.EnterArea("town")` when not in combat), Alt (held) shows all loot labels.
HUD (Hud draws by polling `Game.I` / `World`): life globe left, resource globe right (class colour, pips/sigils
above it), six-slot action bar with cooldown sweeps, charges and key labels between them, potion count, XP bar,
buff icons, minimap top-right with the quest tracker and dungeon objective under it, boss bar with stagger bar
top-centre, enemy nameplates/health bars (elite name + affixes), floating damage numbers (white normal, yellow
crit, blue overpower, small grey DoT; `World.Damage` event), loot labels, interact prompt, toasts, warnings,
area title card, subtitles, low-life vignette, level-up banner, skill/paragon points reminder.

Fonts: `res://assets/fonts/NotoSerifTC-{Regular,Medium,Bold,Black}.otf`, `NotoSansTC-{Regular,Medium,Bold}.otf`,
`Cinzel-Variable.ttf` (Latin display). Icons: `res://assets/icons/gi/<author>/<name>.svg` (game-icons.net, CC BY 3.0;
white-on-transparent after import is NOT guaranteed — tint with `Modulate`/shader); `res://assets/icons/index.txt`
lists every path, `Icons.Get("fireball")` resolves a bare name by searching the index and falls back to a drawn
placeholder. Skills carry an `Icon` name; item icons come from `Item.Icon` (`helm`, `chest`, `gloves`, `pants`,
`boots`, `amulet`, `ring`, `greatsword`, `staff`, `crossbow`, `scythe`, `club`, `gauntlet`, `swordshield`,
`grimoire`, `gem_<type>`) which `Icons.ForItem` maps to suitable game-icons names.

## 4. Assets
- Characters `res://assets/chars/<id>.glb` (heroes `hero_<classKey>`, forms `form_bear|form_wolf`, NPCs `npc_*`,
  monsters `mon_*`, bosses `boss_*`); missing files fall back to a placeholder automatically (`CharModel`).
- Environment: `res://assets/env/tex/<set>/{albedo,normal,arm,height}.jpg` (+`emission.jpg`), manifest
  `res://assets/env/tex/manifest.json` (`sets[].name/size_m/zones/use/avg_color`); props
  `res://assets/env/models/<name>/<name>.glb`, manifest `res://assets/env/models/manifest.json` (name, triangles,
  bounding size, zones, category, light info); skies `res://assets/env/sky/<name>.hdr` + `manifest.json`.
- Audio as in 3.3. Voice lines are produced after the script exists.

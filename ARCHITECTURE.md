# 燼淵 Ashen Abyss — 架構契約

請先閱讀 `Y:\DESIGN.md`（世界觀、職業、區域、怪物）。本文件是各個平行開發中模組之間的契約。如果你負責某個模組，
請完全照著該模組所列的公開 API 實作；你可以加入更多成員，但絕不可重新命名或刪除已列出的成員。其他模組都是依照這些
簽章來編譯的。

## 0. 基本規則
- 引擎：Godot 4.7.2 .NET，C#（`net10.0`、`LangVersion latest`、停用 nullable 註記），命名空間 `AshenAbyss`。
  專案根目錄 `Y:\game`（= `res://`）。原始碼在 `Y:\game\src`，著色器在 `Y:\game\shaders`，資料在 `Y:\game\data`。
- 一律使用 `Y:\` 路徑（這是 `subst` 磁碟機；實際路徑對許多工具來說太長）。如果 `Y:\` 不存在：
  `cmd /c subst Y: "<project folder>"`。
  優先使用 PowerShell，而非 Git Bash。
- 每個檔案只放一個衍生自 Godot 的類別（Node/Control/Resource…），檔名 = 類別名稱，類別宣告為 `partial`。
  一般的 C# 類別（資料、邏輯、靜態輔助類別）可以共用檔案。除了 `scenes/Main.tscn` 之外不使用任何 `.tscn` 場景：
  節點樹一律在程式碼中建立。
- 所有玩家看得到的字串都是正體中文（zh-TW），而且是本遊戲的原創內容。絕不可引用或模仿任何現有遊戲、電影或書籍的
  名稱、角色、文字、標誌或音樂。
- 檔案所有權嚴格劃分：只能編輯你的模組所列出的檔案。如果需要修改不屬於你的檔案，不要直接編輯——請在最終報告的
  「INTEGRATION NOTES」底下描述確切的修改內容。
- 各模組平行撰寫期間，不可執行 `dotnet build`、Godot 或遊戲本身（其他 agent 寫到一半的檔案會讓你的建置失敗，
  而且多個建置會互相衝突）。請對照現有原始碼中的實際簽章仔細閱讀，以此檢查你的成果。之後的整合階段會統一編譯並執行。
- 遊戲透過 `Game.I.Paused` 暫停（SceneTree 永遠不會暫停）。每個推進遊戲進程的 `_Process` 都必須以
  `if (Game.I.Paused) return;` 開頭。UI 程式碼則持續執行。
- 遊戲玩法發生在 y = 0 的 XZ 平面上。邏輯使用 `Vector2 (x, z)`；以 `World.To3` / `World.To2` 轉換。
  角色面向角度 0 時朝向 +Z；見 `Actor.Forward`。
- 紀錄：`Log.Info/Warn/Error(category, message[, exception])`，詳細訊息用 `Log.Debug`。凡是日後除錯者會想知道的
  狀態變化都要記錄（區域載入、存檔、首領階段、購買、錯誤）。有風險的回呼請用 `Log.Guard(category, () => …)` 包起來。
  絕不可讓例外逃出 `_Process`。
- 效能目標：在內建 Radeon 8060S 內顯上，以 1440p 內部解析度達到 60 fps。熱迴圈中不可有每幀配置，共用材質/網格，
  重複的場景物件使用 MultiMesh，投射陰影的光源控制在少數幾盞。
- Blender（僅限素材 agent）：絕不可直接啟動 `blender.exe`。一律使用
  `& Y:\tools\blender\run_blender.ps1 -A @('-b','--factory-startup','--python','<script.py>','--','<args>')`
  （它會把整台機器限制在 2 個 Blender 行程、限制執行緒數，並讓電腦保持喚醒；以前平行跑太多 Blender 工作曾讓這台
  筆電當掉）。在你自己的工作階段中，同一時間不要啟動超過一個 Blender 執行。

## 1. 現有程式碼（請閱讀；除非你是擁有者，否則不要修改）
| 檔案 | 內容 |
|---|---|
| `Defs.cs` | 列舉 `ClassId`（8）、`Element`（6）、`Rarity`、`ItemKind`、`EquipSlot`、`SkillCat`、`Faction`、`Stat`；`StatInfo`、`StatBlock`、`Names`（職業名稱/鍵值/顏色/資源）、`Palette`（稀有度/元素/UI 顏色）、`Rng` |
| `Log.cs`, `WinKeys.cs` | 檔案紀錄器；PrtSc 低階鍵盤掛勾（`WinKeys.Start/Stop/ConsumeScreenshotRequest`） |
| `Items.cs` | `Item`、`Affix`、`ItemDb`（基底、詞綴 `Pools`、`Aspects`、`Uniques`、`Gems`、`Generate`、打造費用）、`ItemText.Tooltip`（BBCode） |
| `SkillDef.cs`, `SkillsData.cs` | 宣告式技能（`SkillDef`、`SkillUp`、`BuffDef`、`ZoneSpec`、`StatusId`、`St`）、`SkillDb`（64 個技能、被動、`ClassSpec`） |
| `MonstersData.cs` | `MonsterDef`、`MonsterDb`、`EliteAffix` 清單、`RegionDef`/`DungeonDef`/`ZoneDb`（7 個區域 × 3 座地城）、`DungeonGoal`、`Balance` |
| `Save.cs` | `SaveData`（partial）、`SaveSystem`（8 個存檔欄位）、`SlotInfo`、`Settings` |
| `Hero.cs` | `Hero`（partial）：屬性（`Recalc`、`S`）、增益效果、技能點/升級/洗點、專精、經驗/燼紋等級、背包、裝備、拆解/強化/附魔/銘刻/鑲孔 |
| `World.cs` | `NavGrid`（2 m 格、障礙物、碰撞、視線、A*）、`World` 登錄表（`P`、`Monsters`、`Minions`、`Grid`、`Root`、查詢、`Damage`/`MonsterKilled` 事件、`Event`/`Emit`、`FootstepSfx`） |
| `Actor.cs`, `Combat.cs` | 角色基底（狀態、屏障、命中）、傷害公式 |
| `Player.cs`, `SkillExec.cs` | 英雄控制器、施法狀態機、掛勾；依資料執行所有技能 |
| `Monster.cs` | 依角色定位運作的怪物/召喚物 AI、菁英、硬直、死亡 |
| `Projectile.cs`, `GroundZone.cs`, `Interactable.cs` | 投射物、地面區域/陷阱、可點擊的世界物件 |
| `CharModel.cs` | 載入 `res://assets/chars/<id>.glb`、共用動畫庫、角色著色器、染色/閃光/溶解 |
| `Talents.cs` | 舊的單一天賦盤——將由 paragon 模組取代（並由該模組刪除） |
| `shaders/` | `char`, `spectral`, `fx_ring`, `fx_telegraph`, `fx_zone`, `fx_slash`, `fx_flash`, `fx_beam` |

`World.Emit(event, arg)` 事件（發送端 → 任務、目標、名望、HUD 等監聽端）：
`area`(areaId) · `kill`(monsterDefId) · `elite`(defId) · `boss`(bossDefId) · `miniboss`(dungeonId) ·
`dungeon`(dungeonId，完成時) · `stronghold`(regionId) · `altar`(altarId) · `event`(regionId) ·
`talk`(npcId) · `waypoint`(areaId) · `levelup`(level) · `rift`(tier) · `death`("") · `objective`("")（進度變動）。

## 2. 模組、擁有者與檔案
| 鍵值 | 擁有的檔案 | 摘要 |
|---|---|---|
| spine | `Game.cs`, `CameraRig.cs`, `Gfx.cs`, `TestBot.cs`, `scenes/Main.tscn`, `project.godot`, `AshenAbyss.csproj` | 啟動、狀態流程、存檔、暫停、截圖、圖形設定、測試框架 |
| fx | `Fx.cs`（+ 新的 `shaders/fx_*.gdshader`） | 所有視覺特效 |
| audio | `Snd.cs`, `AudioMan.cs` | 音效、音樂、環境音、語音播放 |
| levelgen | `LevelGen.cs` | 程序化佈局 → `LevelData` |
| levelrender | `LevelThemes.cs`, `LevelBuilder.cs`, `shaders/env_*.gdshader` | 主題、地形/牆壁/場景物件網格、照明、天氣 |
| levelplay | `Level.cs`, `Populate.cs`, `WorldObjects.cs` | 關卡節點、怪物群、可互動物件、目標、事件、城鎮、裂隙 |
| bosses | `EliteFx.cs`, `BossBrain.cs` | 菁英詞綴行為、首領戰 |
| loot | `Loot.cs` | 掉落、地上戰利品、寶箱、商人貨架、賭運 |
| story | `Quests.cs`, `Dialogue.cs`, `data/vo_lines.json` | 主線、支線任務、名望、對話、語音腳本 |
| paragon | `Paragon.cs`, `Hero.Paragon.cs`, `ParagonUi.cs`（刪除 `Talents.cs`） | 終局燼紋盤、燼符及其 UI |
| ui-core | `UiTheme.cs`, `Icons.cs`, `Ui.cs`, `UiPanel.cs`, `Hud*.cs` | 主題、面板管理器、工具提示、整個 HUD |
| ui-menu | `MainMenu.cs`, `PauseMenu.cs`, `SettingsUi.cs`, `DeathUi.cs`, `CreditsData.cs` | 標題畫面、角色建立、存檔欄位、設定 |
| ui-items | `InventoryUi.cs`, `VendorUi.cs`, `Hero.Crafting.cs`, `Crafting.cs` | 背包、倉庫、所有商人、淬火 |
| ui-skills | `SkillTreeUi.cs` | 技能樹、升級、被動、專精、技能列配置 |
| ui-world | `MapUi.cs`, `DialogueUi.cs`, `QuestLogUi.cs`, `WorldTierUi.cs`, `RiftUi.cs` | 地圖與移動、對話、任務日誌、世界階級、裂隙 |

## 3. API

### 3.1 spine — `Game`（Node，`scenes/Main.tscn` 的根節點）
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
區域 id：`"town"`、區域 id（`wastes`、`frost`、`crypt`、`mire`、`sands`、`sanctum`、`abyss` → 該區域的野外地圖）、
`ZoneDb` 中的地城 id（例如 `wastes_d1`、`wastes_boss`），或 `"rift:<tier>"`。`entry` 是玩家來自的區域
（讓玩家出現在對應的入口處）或 `"waypoint"`。
自動存檔時機：切換區域、升級、擊殺首領、任務步驟、每 3 分鐘、離開遊戲。視窗：預設為全螢幕
（`Cfg.Fullscreen`），`--win` 會強制使用 1600×900 視窗。PrtSc 透過 `WinKeys.ConsumeScreenshotRequest()`
每幀輪詢；F12 也能截圖。
測試開關（放在命令列的 `--` 之後）：`--win`、`--new=<classKey>`、`--load=<slot>`、`--area=<id>`、
`--level=<n>`、`--gear`（裝備符合等級的稀有裝備）、`--god`、`--bot`（TestBot 自動遊玩）、
`--ui=<panelId>`、`--shot=<path.png>`、`--shot-at=<sec>[,sec…]`、`--quit-at=<sec>`、`--mute`、`--tier=<n>`。
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
所有位置都是世界座標。特效會自行釋放。元素顏色取自 `Palette.Element`。
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
敵方的地面危險區一律使用 `Palette.Danger` 並帶有銳利邊緣；友方的預警標示則較暗淡。

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
檔案：`res://assets/audio/sfx/<name>_<n>.wav`（n 從 1 開始）、`amb/<name>.ogg`、`music/<name>.ogg`、`vo/<id>.ogg|wav`。
`res://assets/audio/manifest_audio.json` 內含 `"groups": { "<name>": ["assets/audio/sfx/<name>_1.wav", …] }`。
未知的名稱只記錄一次並忽略，絕不可拋出例外。限制每個音效同時播放的實例數（3-4 個）。

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
`NavGrid` 格子類型：`Floor`、`Wall`（戶外的茂密森林/懸崖也算）、`Pit`（熔岩 / 水 / 深淵：擋住行走，
但不擋射擊）。LevelGen 負責填滿格網，對會阻擋的場景物件呼叫 `Grid.AddObstacle`，最後呼叫 `Grid.BuildPathing()`。
Poi 種類：`start`、`exit`（Tag = 通往的區域）、`entrance`（Tag = 地城 id）、`boss`（競技場中心，Radius）、
`bossdoor`、`stronghold`（中心，Radius）、`waypoint`、`altar`、`chest`、`shrine`、`event`、`objective`、
`npc`（Tag = elder|smith|merchant|occultist|jeweler|villager）、`stash`、`brazier`、`tierstatue`、`riftstone`。
PropSlot 類別：`tree`、`rock`、`rock_big`、`clutter`、`breakable`、`furniture`、`light`、`pillar`、
`structure`、`foliage`。保證事項：每個 Poi 與生成點都位於可從 `start` 抵達的可行走格上；`start` 周圍至少
淨空 4 m；首領競技場是半徑 ≥ 11 m 的開闊地面，且只有一道門；野外地圖約 110×110 格，地城每邊約 60-80 格，
城鎮約 50×50。
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
主題 id：`town`、`wastes`、`frost`、`barrows`、`mire`、`sands`、`sanctum`、`abyss`、`crypt`、`cave`、`graveyard`、
`icehall`、`icecave`、`wetcave`、`brasshall`、`sandtomb`、`cathedral`、`forge`、`lavacave`、`rift`。
位於鏡頭與英雄之間的牆壁和高大場景物件不可遮住戰鬥畫面：使用全域著色器 uniform
`hero_pos`（vec3，由 `Level` 每幀設定），在遮擋物上挖出一個邊緣柔和的透視圓。
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
可互動物件由 `WorldObjects.cs` 中的小型工廠函式建立（`WorldObjects.Chest(pos, quality)`、
`Shrine`、`Altar`、`Waypoint`、`Entrance`、`Exit`、`Npc`、`Breakable`、`Idol`、`Prisoner`、`Brazier`…），這些函式會建立
一個帶有模型/特效與 `OnUse` 回呼的 `Interactable`。

### 3.5 bosses
```csharp
public static class EliteFx { void Apply(Monster m); void Tick(Monster m, float dt); void OnHitTaken(Monster m, Hit h); void OnAttackHit(Monster m); void OnDeath(Monster m); }
public sealed class BossBrain { public BossBrain(Monster m); public int Phase; public void Tick(float dt); public void OnDamaged(); public void Interrupt(); public void OnDeath(); }
```
`Monster` 已提供 `BossMove(goal, dt, stopAt)`、`BossIdle()`、`Busy(time, end, tick)`、`AffixT[]`、
`SpeedMul`、`AtkSpeedMul`、`DrMul`、`DmgMul`、`BlocksProjectiles`、`Stagger/Staggered`、`BaseDamage`。
敵方傷害一律經由 `Combat.MonsterHit` / `Combat.MonsterArea`、`Projectile.SpawnMonster`、
`GroundZone.SpawnMonster`；每一個危險攻擊都要用 `Fx.Telegraph*` 預警至少 0.6 s。

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
`data/vo_lines.json`：每一句有配音的台詞都是 `[{"id","speaker","voice","text"}]`（voice = 語音設定檔名稱，例如
`narrator`、`elder`、`smith`、`merchant`、`occultist`、`jeweler`、`boss_<id>`、`hero_<classKey>`）。

### 3.8 paragon
`Hero.Paragon.cs` 實作 `partial void ApplyParagonStats()`（把燼紋盤/燼符的屬性加進 `S`，並把掛勾 id 加進
`Keystones`），以及 `ParagonUi` 所使用的配點 API。戰鬥程式碼已經支援的掛勾 id——請保留為傳奇節點：
`ks_fury`、`ks_resonance`、`ks_combo`、`ks_army`、`ks_bulwark`、`ks_cycle`。可用點數 =
`D.Paragon + D.BonusParagonPts − spent`；系統在 `Balance.LevelCap` 時解鎖。燼符透過通關裂隙
升級（`World.Emit("rift", tier)`）。

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
面板 id → 類別：`inventory` InventoryUi · `skills` SkillTreeUi · `paragon` ParagonUi · `map` MapUi（從燼塔路標開啟時
arg 為 `"travel"`）· `quests` QuestLogUi · `vendor` VendorUi（arg = `merchant|smith|occultist|
jeweler|stash`）· `dialogue` DialogueUi（arg = DialogueScript）· `pause` PauseMenu · `settings` SettingsUi ·
`death` DeathUi · `tier` WorldTierUi · `rift` RiftUi。`MainMenu` 不是面板：由 `Game` 在 Menu 狀態時加入。
由 `Ui` 處理的快速鍵：I/C 背包、K 技能、P 燼紋盤、M/Tab 地圖、J 任務、Esc 關閉最上層面板或開啟
`pause`、T 回城傳送門（不在戰鬥中時執行 `Game.I.EnterArea("town")`）、按住 Alt 顯示所有戰利品標籤。
HUD（Hud 以輪詢 `Game.I` / `World` 的方式繪製）：左邊是生命球，右邊是資源球（職業顏色，上方有點數/契印），
兩球之間是六格技能列，含冷卻掃描、充能次數與按鍵標籤，另有藥水數量、經驗條、增益圖示、右上角小地圖及其下方的
任務追蹤與地城目標、畫面上方中央含硬直條的首領血條、敵人名牌/血條（菁英名稱 + 詞綴）、浮動傷害數字（白色為一般、
黃色為暴擊、藍色為壓制、灰色小字為持續傷害；`World.Damage` 事件）、戰利品標籤、互動提示、浮動通知、警告、
區域標題卡、字幕、低血量暗角、升級橫幅、技能點/燼紋點提醒。

字型：`res://assets/fonts/NotoSerifTC-{Regular,Medium,Bold,Black}.otf`、`NotoSansTC-{Regular,Medium,Bold}.otf`、
`Cinzel-Variable.ttf`（拉丁字母標題字）。圖示：`res://assets/icons/gi/<author>/<name>.svg`（game-icons.net，CC BY 3.0；
不保證匯入後是透明底白色圖——請用 `Modulate`/著色器染色）；`res://assets/icons/index.txt`
列出所有路徑，`Icons.Get("fireball")` 會搜尋索引來解析單純的名稱，找不到時退回繪製的
預留圖。技能帶有一個 `Icon` 名稱；物品圖示來自 `Item.Icon`（`helm`、`chest`、`gloves`、`pants`、
`boots`、`amulet`、`ring`、`greatsword`、`staff`、`crossbow`、`scythe`、`club`、`gauntlet`、`swordshield`、
`grimoire`、`gem_<type>`），由 `Icons.ForItem` 對應到合適的 game-icons 名稱。

## 4. 素材
- 角色 `res://assets/chars/<id>.glb`（英雄 `hero_<classKey>`、形態 `form_bear|form_wolf`、NPC `npc_*`、
  怪物 `mon_*`、首領 `boss_*`）；缺少的檔案會自動退回預留模型（`CharModel`）。
- 環境：`res://assets/env/tex/<set>/{albedo,normal,arm,height}.jpg`（+`emission.jpg`），清單檔
  `res://assets/env/tex/manifest.json`（`sets[].name/size_m/zones/use/avg_color`）；場景物件
  `res://assets/env/models/<name>/<name>.glb`，清單檔 `res://assets/env/models/manifest.json`（名稱、三角面數、
  包圍尺寸、區域、類別、光源資訊）；天空 `res://assets/env/sky/<name>.hdr` + `manifest.json`。
- 音訊見 3.3。語音台詞在腳本完成後才製作。

// Builds Y:\BESTIARY.md and Y:\game\data\bestiary.json from the text sources in .\src (documentation tool, not game code).
// Run:  dotnet run Y:\tools\bestiary\build.cs            (optional args: <tool dir> <markdown out> <json out>)
#nullable disable
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

string root = args.Length > 0 ? args[0] : @"Y:\tools\bestiary";
string mdOut = args.Length > 1 ? args[1] : @"Y:\BESTIARY.md";
string jsonOut = args.Length > 2 ? args[2] : @"Y:\game\data\bestiary.json";
string src = Path.Combine(root, "src");
var inv = CultureInfo.InvariantCulture;
var errors = new List<string>();

string[] Lines(string f) => File.ReadAllLines(Path.Combine(src, f), Encoding.UTF8);
string Text(string f) => File.ReadAllText(Path.Combine(src, f), Encoding.UTF8).Replace("\r\n", "\n").TrimEnd() + "\n";
string[] Cells(string line) => Regex.Split(line, @"\s\|\s").Select(s => s.Trim()).ToArray();
double Num(string s) => double.Parse(s, inv);
string F(double d) => d.ToString("0.##", inv);

var regionOrder = new[] { "W", "F", "C", "M", "S", "N", "A" };
var regionOf = new Dictionary<string, string> { ["W"] = "wastes", ["F"] = "frost", ["C"] = "crypt", ["M"] = "mire", ["S"] = "sands", ["N"] = "sanctum", ["A"] = "abyss" };
var bodyOrder = new[] { "H", "B", "AR", "Q", "S", "FL", "FI", "G", "X" };
var bodyName = new Dictionary<string, string> { ["H"] = "humanoid biped", ["B"] = "oversized bruiser", ["AR"] = "arthropod / crawler", ["Q"] = "quadruped beast", ["S"] = "serpentine", ["FL"] = "flyer", ["FI"] = "floating / incorporeal", ["G"] = "giant / construct", ["X"] = "swarm / turret / siege" };
var bodyTarget = new Dictionary<string, string> { ["H"] = "about 45%", ["B"] = "12–15%", ["AR"] = "about 12%", ["Q"] = "8–10%", ["S"] = "about 6%", ["FL"] = "about 5%", ["FI"] = "about 5%", ["G"] = "about 5%", ["X"] = "3–4%" };
var rigName = new Dictionary<string, string> { ["A"] = "shared humanoid skeleton (available now)", ["B"] = "quadruped", ["C"] = "arthropod", ["D"] = "serpentine", ["E"] = "flyer", ["F"] = "rigid / floating" };
var existingKinds = new HashSet<string> { "Melee", "Ranged", "Caster", "Exploder", "Teleporter", "Brute", "Support", "Leaper" };

// ───────────────────────── parse ─────────────────────────
var families = new List<Fam>();
var types = new List<Typ>();
foreach (var file in new[] { "fam_a.txt", "fam_b.txt", "fam_c.txt" })
{
    Fam cur = null;
    foreach (var raw in Lines(file))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        if (line.StartsWith("@family "))
        {
            var c = Cells(line[8..]);
            if (c.Length != 4) { errors.Add($"family header: {line}"); continue; }
            cur = new Fam { Id = c[0], Zh = c[1], En = c[2], Scope = c[3] };
            families.Add(cur);
        }
        else if (line.StartsWith("concept: ")) cur.Concept = line[9..];
        else if (line.StartsWith("lore: ")) cur.Lore = line[6..];
        else if (line.StartsWith("has: ")) cur.Has = line[5..];
        else if (line.StartsWith("lacks: ")) cur.Lacks = line[7..];
        else
        {
            var c = Cells(line);
            if (c.Length != 20) { errors.Add($"type row has {c.Length} cells (need 20): {line[..Math.Min(60, line.Length)]}"); continue; }
            var rigParts = c[5].Split('-', 2);
            var t = new Typ
            {
                Id = c[0], Zh = c[1], En = c[2], Role = c[3], Ai = c[4], Rig = rigParts[0], RigNote = rigParts.Length > 1 ? rigParts[1] : "",
                Body = c[6], Build = c[7], VariantOf = c[8] == "-" ? null : c[8], Element = c[9], Status = c[10],
                Hp = Num(c[11]), Dmg = Num(c[12]), Speed = Num(c[13]), Scale = Num(c[14]), Anims = c[15],
                Regions = c[16].Split(',').Select(s => s.Trim()).ToList(), W = Num(c[17]), Stage = int.Parse(c[18]), Brief = c[19], Family = cur.Id,
            };
            types.Add(t); cur.Types.Add(t);
        }
    }
}
var typeById = new Dictionary<string, Typ>();
foreach (var t in types) { if (!typeById.TryAdd(t.Id, t)) errors.Add($"duplicate type id {t.Id}"); }
var famById = families.ToDictionary(f => f.Id);

var behaviours = new List<Beh>();
foreach (var raw in Lines("behaviours.txt"))
{
    var line = raw.Trim();
    if (line.Length == 0 || line.StartsWith('#')) continue;
    var c = Cells(line);
    if (c.Length != 8) { errors.Add($"behaviour row has {c.Length} cells: {line[..Math.Min(50, line.Length)]}"); continue; }
    behaviours.Add(new Beh { Token = c[0], Kind = c[1], Zh = c[2], Trigger = c[3], Telegraph = c[4], Effect = c[5], Cooldown = c[6], Data = c[7] });
}
var behByToken = behaviours.ToDictionary(b => b.Token);

var reskins = new List<Reskin>();
foreach (var raw in Lines("reskins.txt"))
{
    var line = raw.Trim();
    if (line.Length == 0 || line.StartsWith('#')) continue;
    var c = Cells(line);
    if (c.Length != 8) { errors.Add($"reskin row has {c.Length} cells: {line[..Math.Min(50, line.Length)]}"); continue; }
    reskins.Add(new Reskin { Id = c[0], Base = c[1], Region = c[2], Zh = c[3], Element = c[4], Status = c[5], State = c[6], Look = c[7] });
}
var reskinAt = new Dictionary<(string, string), Reskin>();
foreach (var r in reskins) reskinAt[(r.Base, r.Region)] = r;

var regions = new List<Reg>();
{
    Reg cur = null;
    foreach (var raw in Lines("regions.txt"))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        if (line.StartsWith("@region "))
        {
            var c = Cells(line[8..]);
            cur = new Reg { Id = c[0], Zh = c[1], Letter = c[2], Act = int.Parse(c[3]), Level = int.Parse(c[4]), Boss = c[5], Stronghold = c[6], StrongholdBoss = c[7], WorldBoss = c[8] };
            regions.Add(cur);
        }
        else if (line.StartsWith("families: "))
            foreach (var part in line[10..].Split(','))
            {
                var kv = part.Trim().Split('=');
                bool sig = kv[0].EndsWith('*');
                cur.Families.Add((kv[0].TrimEnd('*'), Num(kv[1]), sig));
            }
        else if (line.StartsWith("note: ")) cur.Note = line[6..];
        else if (line.StartsWith("dungeon "))
        {
            var c = Cells(line[8..]);
            cur.Dungeons.Add(new Dun { Id = c[0], Zh = c[1], Families = c[2].Split(',').Select(s => s.Trim()).ToList(), Boss = c[3], Note = c.Length > 4 ? c[4] : "" });
        }
    }
}

var named = new List<Named>();
foreach (var raw in Lines("named.txt"))
{
    var line = raw.Trim();
    if (line.Length == 0 || line.StartsWith('#')) continue;
    var c = Cells(line);
    if (c.Length != 9) { errors.Add($"named row has {c.Length} cells: {line[..Math.Min(50, line.Length)]}"); continue; }
    named.Add(new Named { Kind = c[0], Id = c[1], Zh = c[2], En = c[3], Base = c[4], Region = c[5], Where = c[6], Look = c[7], Mechanics = c[8].Split(" ;; ").Select(s => s.Trim()).ToList() });
}
var namedById = new Dictionary<string, Named>();
foreach (var n in named) if (!namedById.TryAdd(n.Id, n)) errors.Add($"duplicate named id {n.Id}");

var stages = new List<(int n, string title, string what)>();
foreach (var raw in Lines("stages.txt"))
{
    var line = raw.Trim();
    if (line.Length == 0 || line.StartsWith('#')) continue;
    var c = Cells(line);
    stages.Add((int.Parse(c[0]), c[1], c[2]));
}

// ───────────────────────── validate ─────────────────────────
List<string> Tokens(string ai) => ai.Split('+').Select(s => Regex.Replace(s, @"\(.*?\)", "").Trim()).ToList();
foreach (var t in types)
{
    if (t.Zh.Length < 2 || t.Zh.Length > 5) errors.Add($"{t.Id}: Chinese name '{t.Zh}' is not 2-5 characters");
    if (!bodyName.ContainsKey(t.Body)) errors.Add($"{t.Id}: body '{t.Body}'");
    if (!rigName.ContainsKey(t.Rig)) errors.Add($"{t.Id}: rig '{t.Rig}'");
    if (t.Build is not ("built" or "planned" or "base" or "variant")) errors.Add($"{t.Id}: build '{t.Build}'");
    if (t.Build == "variant" && t.VariantOf == null) errors.Add($"{t.Id}: variant without a base");
    if (t.VariantOf != null)
    {
        if (!typeById.TryGetValue(t.VariantOf, out var b)) errors.Add($"{t.Id}: variant of unknown '{t.VariantOf}'");
        else if (b.Rig != t.Rig) errors.Add($"{t.Id}: variant of {b.Id} but rigs differ ({t.Rig} vs {b.Rig})");
    }
    foreach (var r in t.Regions) if (!regionOf.ContainsKey(r)) errors.Add($"{t.Id}: region '{r}'");
    var tk = Tokens(t.Ai);
    if (!existingKinds.Contains(tk[0]) && !(behByToken.TryGetValue(tk[0], out var k0) && k0.Kind == "ai")) errors.Add($"{t.Id}: AI kind '{tk[0]}' is not defined");
    foreach (var x in tk.Skip(1)) if (!(behByToken.TryGetValue(x, out var k1) && k1.Kind == "trait")) errors.Add($"{t.Id}: trait '{x}' is not defined");
    if (Regex.Matches(t.Brief, @"[.!?](\s|$)").Count < 2 && t.Build is "base" or "variant") errors.Add($"{t.Id}: silhouette brief is shorter than two sentences");
}
foreach (var b in behaviours) b.UsedBy = types.Where(t => Tokens(t.Ai).Contains(b.Token)).Select(t => t.Id).ToList();
foreach (var b in behaviours) if (b.UsedBy.Count == 0) errors.Add($"behaviour {b.Token} is not used by any type");
foreach (var r in reskins)
{
    if (!typeById.TryGetValue(r.Base, out var b)) { errors.Add($"reskin {r.Id}: unknown base {r.Base}"); continue; }
    if (!b.Regions.Contains(r.Region)) errors.Add($"reskin {r.Id}: base {r.Base} is not listed for region {r.Region}");
    if (typeById.ContainsKey(r.Id)) errors.Add($"reskin id {r.Id} collides with a type id");
}
foreach (var n in named)
{
    if (!(n.Base.StartsWith("boss_") || n.Base.StartsWith("NEW") || typeById.ContainsKey(n.Base))) errors.Add($"named {n.Id}: unknown base {n.Base}");
}
foreach (var r in regions)
{
    double sum = r.Families.Sum(f => f.w);
    if (Math.Abs(sum - 100) > 0.01) errors.Add($"region {r.Id}: family weights sum to {sum}");
    foreach (var f in r.Families)
    {
        if (!famById.ContainsKey(f.id)) { errors.Add($"region {r.Id}: unknown family {f.id}"); continue; }
        if (!famById[f.id].Types.Any(t => t.Regions.Contains(r.Letter))) errors.Add($"region {r.Id}: family {f.id} has no type listed for {r.Letter}");
    }
    foreach (var fam in families)
        if (fam.Types.Any(t => t.Regions.Contains(r.Letter)) && !r.Families.Any(f => f.id == fam.Id)) errors.Add($"region {r.Id}: family {fam.Id} has types here but no weight");
    foreach (var d in r.Dungeons)
    {
        foreach (var f in d.Families) if (!r.Families.Any(x => x.id == f)) errors.Add($"dungeon {d.Id}: family {f} is not in region {r.Id}");
        if (!(d.Boss.StartsWith("boss_") || namedById.ContainsKey(d.Boss))) errors.Add($"dungeon {d.Id}: unknown boss {d.Boss}");
        if (d.Families.Count < 2 || d.Families.Count > 3) errors.Add($"dungeon {d.Id}: uses {d.Families.Count} families");
    }
    if (!namedById.ContainsKey(r.StrongholdBoss)) errors.Add($"region {r.Id}: unknown stronghold boss");
    if (!namedById.ContainsKey(r.WorldBoss)) errors.Add($"region {r.Id}: unknown world boss");
}
if (errors.Count > 0)
{
    Console.WriteLine("ERRORS:");
    foreach (var e in errors) Console.WriteLine("  " + e);
    return 1;
}

// ───────────────────────── derive ─────────────────────────
string SpawnId(Typ t, string letter) => reskinAt.TryGetValue((t.Id, letter), out var rs) ? rs.Id : t.Id;
foreach (var r in regions)
    foreach (var f in r.Families)
    {
        var here = famById[f.id].Types.Where(t => t.Regions.Contains(r.Letter)).ToList();
        double tw = here.Sum(t => t.W);
        foreach (var t in here) r.Spawns.Add((SpawnId(t, r.Letter), t.Id, f.id, Math.Round(f.w * t.W / tw, 2)));
    }
string BuildText(Typ t) => t.Build switch
{
    "built" => "existing (built)",
    "planned" => "existing (planned)",
    "base" => "base",
    _ => $"variant of `{t.VariantOf}`",
};
string RigText(Typ t) => t.RigNote.Length > 0 ? $"{t.Rig} ({t.RigNote})" : t.Rig;
string RegText(IEnumerable<string> letters) => string.Join(", ", regionOrder.Where(letters.Contains).Select(l => regionOf[l]));
string MainRole(Typ t) => Regex.Replace(t.Role, @"\s*\(.*\)", "");
string LackShort(Fam f) => f.Lacks.Split(" — ")[0];
List<string> FamRegions(Fam f) => regionOrder.Where(l => f.Types.Any(t => t.Regions.Contains(l))).ToList();
bool IsSig(Fam f, string letter) => regions.First(r => r.Letter == letter).Families.Any(x => x.id == f.Id && x.sig);
int maxStage = types.Max(t => t.Stage);
int ModelsIn(int n) => types.Count(t => t.Stage == n) + (n == 1 ? 1 : 0) - (n == 0 ? 1 : 0);   // the cinderling's own model is built in stage 1
var namedKinds = new[] { ("rare", "Named rare elites"), ("stronghold", "Stronghold bosses"), ("dungeon", "Side-dungeon bosses"), ("region", "Region bosses"), ("world", "World bosses"), ("pinnacle", "Pinnacle ladder bosses") };

// ───────────────────────── markdown ─────────────────────────
var md = new StringBuilder();
void L(string s = "") => md.Append(s).Append('\n');
md.Append(Text("intro.md"));
L();

// 1 ── summary
L("## 1. Summary");
L();
L($"**{families.Count} families, {types.Count} regular types** ({types.Count(t => t.Build is "built" or "planned") + 1} existing ids kept, {types.Count(t => t.Build is "base")} new bases, {types.Count(t => t.Build == "variant") - 1} new variants), plus {reskins.Count} regional reskins, {named.Count(n => n.Kind == "rare")} named rare elites, {named.Count(n => n.Kind == "stronghold")} stronghold bosses, {named.Count(n => n.Kind == "dungeon")} side-dungeon bosses, {named.Count(n => n.Kind == "region")} region bosses, {named.Count(n => n.Kind == "world")} world bosses and {named.Count(n => n.Kind == "pinnacle")} pinnacle bosses. Regions in bold are the ones where the family is a signature family.");
L();
L("| # | family id | 中文名 | English | concept | regions | types | roles it has | deliberately lacks | rigs |");
L("|---|---|---|---|---|---|---|---|---|---|");
int fi = 0;
foreach (var f in families)
{
    fi++;
    string regs = string.Join(", ", FamRegions(f).Select(l => IsSig(f, l) ? $"**{regionOf[l]}**" : regionOf[l]));
    string rigs = string.Join(", ", f.Types.Select(t => t.Rig).Distinct().OrderBy(x => x));
    L($"| {fi} | `{f.Id}` | {f.Zh} | {f.En} | {f.Concept} | {regs} | {f.Types.Count} | {f.Has} | {LackShort(f)} | {rigs} |");
}
L();
L("Global families (five, re-skinned per region): `bone`, `husk`, `cult`, `wraith`, `reaver`. `dusk` is the wildlife catch-all with a different set of beasts in each region. `hauler` is a deliberate \"one giant in someone else's pack\" family.");
L();

// 2 ── families
L("## 2. Families");
L();
fi = 0;
foreach (var f in families)
{
    fi++;
    L($"### 2.{fi} {f.Zh} `{f.Id}` — {f.En}");
    L();
    L($"*{f.Concept}*");
    L();
    L($"**What they were, what the dusk made them.** {f.Lore}");
    L();
    L($"**Roles it has:** {f.Has}.  ");
    L($"**Deliberately lacks:** {f.Lacks}  ");
    L($"**Regions:** {string.Join(", ", FamRegions(f).Select(l => IsSig(f, l) ? $"**{regionOf[l]}** (signature)" : regionOf[l]))}.  ");
    L($"**Rigs:** {string.Join(", ", f.Types.GroupBy(t => t.Rig).OrderBy(g => g.Key).Select(g => $"{g.Key} ×{g.Count()}"))}.");
    L();
    L("| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |");
    L("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
    foreach (var t in f.Types)
        L($"| `{t.Id}` | {t.Zh} | {t.En} | {t.Role} | {t.Ai} | {RigText(t)} | {BuildText(t)} | {t.Element} | {t.Status} | {F(t.Hp)} | {F(t.Dmg)} | {F(t.Speed)} | {F(t.Scale)} | {t.Anims} | {RegText(t.Regions)} |");
    L();
    L("| id | 中文名 | silhouette brief for the modeller |");
    L("|---|---|---|");
    foreach (var t in f.Types) L($"| `{t.Id}` | {t.Zh} | {t.Brief} |");
    L();
}

// 3 ── regions
L("## 3. Region spawn tables");
L();
L("Weights are shares of 100 at pack level: the chance that a pack's core is drawn from that family. Inside a family each type has a relative weight by role (swarmers 5, line melee 3–4, ranged and casters about 2, special roles 1–1.5, bruisers 1, mothers and giants below 1); the flat list under each table is the product of the two and is what goes into `RegionDef.Spawns`. Where a regional reskin exists the reskin's id is listed. Each dungeon draws from the two or three families named for it.");
L();
L("| region | 名稱 | signature families | shared families | families | regular types present |");
L("|---|---|---|---|---|---|");
foreach (var r in regions)
    L($"| `{r.Id}` | {r.Zh} | {string.Join(", ", r.Families.Where(f => f.sig).Select(f => $"`{f.id}`"))} | {string.Join(", ", r.Families.Where(f => !f.sig).Select(f => $"`{f.id}`"))} | {r.Families.Count} | {types.Count(t => t.Regions.Contains(r.Letter))} |");
L();
int ri = 0;
foreach (var r in regions)
{
    ri++;
    L($"### 3.{ri} {r.Zh} `{r.Id}` — act {r.Act}, level {r.Level}");
    L();
    L(r.Note);
    L();
    L("| family | 中文名 | | weight | types that spawn here |");
    L("|---|---|---|---|---|");
    foreach (var f in r.Families)
    {
        var here = famById[f.id].Types.Where(t => t.Regions.Contains(r.Letter)).Select(t => $"`{SpawnId(t, r.Letter)}`");
        L($"| `{f.id}` | {famById[f.id].Zh} | {(f.sig ? "signature" : "shared")} | {F(f.w)} | {string.Join(", ", here)} |");
    }
    L();
    L("Flat list for `RegionDef.Spawns`:");
    L();
    L("```");
    var sb = new StringBuilder();
    int col = 0;
    foreach (var s in r.Spawns)
    {
        string item = $"(\"{s.id}\", {F(s.w)}f), ";
        if (col + item.Length > 118) { L(sb.ToString().TrimEnd()); sb.Clear(); col = 0; }
        sb.Append(item); col += item.Length;
    }
    if (sb.Length > 0) L(sb.ToString().TrimEnd().TrimEnd(','));
    L("```");
    L();
    L("| dungeon | 名稱 | families | boss | note |");
    L("|---|---|---|---|---|");
    foreach (var d in r.Dungeons)
    {
        string boss = namedById.TryGetValue(d.Boss, out var nb) ? $"`{nb.Id}` {nb.Zh}" : $"`{d.Boss}`";
        L($"| `{d.Id}` | {d.Zh} | {string.Join(", ", d.Families.Select(x => $"`{x}`"))} | {boss} | {d.Note} |");
    }
    L();
    var sh = namedById[r.StrongholdBoss]; var wb = namedById[r.WorldBoss]; var rb = namedById[r.Boss];
    L($"Stronghold {r.Stronghold}: `{sh.Id}` {sh.Zh}. Region boss: `{rb.Id}` {rb.Zh}. World boss: `{wb.Id}` {wb.Zh}. Named rare elites: {string.Join(", ", named.Where(n => n.Kind == "rare" && n.Region == r.Id).Select(n => $"`{n.Id}` {n.Zh}"))}.");
    L();
}
L("### 3.8 The rift (深淵裂隙)");
L();
L("A rift floor draws two or three families at random from the whole roster — one \"line\" family (any with swarmer and melee roles), one \"pressure\" family (casters, bombers, turrets or burrowers) and, from rift tier 5, one giant or mother as a guest. This is where the single-region families get their reuse.");
L();
L("### 3.9 Regional reskins of the global families");
L();
L("Same model and behaviour as the base type; material swap, element and status change (`MonsterDef.Variant`). The ten marked *existing* are already in `MonstersData.cs`.");
L();
L("| reskin id | base | region | 中文名 | element | status | state | look |");
L("|---|---|---|---|---|---|---|---|");
foreach (var r in reskins) L($"| `{r.Id}` | `{r.Base}` | {regionOf[r.Region]} | {r.Zh} | {r.Element} | {r.Status} | {r.State} | {r.Look} |");
L();
md.Append(Text("skins.md"));
L();

// 4 ── named
L("## 4. Named monsters");
L();
L("Rare elites are fixed named spawns in the open region (a low chance per visit, announced on the map when found); they keep their base creature's behaviour, roll two ordinary affixes on top, and add the mechanics listed. Stronghold and side-dungeon bosses replace today's scaled-up regular monsters (`RegionDef.StrongholdBoss`, `DungeonDef.MiniBoss`). \"Look\" is the dressing that turns the base model into the named one.");
L();
int ni = 0;
foreach (var (kind, title) in namedKinds)
{
    ni++;
    var list = named.Where(n => n.Kind == kind).ToList();
    L($"### 4.{ni} {title} ({list.Count})");
    L();
    if (kind == "pinnacle") { L("The endgame ladder is 燼階, the Ember Stair: eight rungs fought in order, each an \"ember-memory\" (燼憶) of a boss as the abyss remembers it. Seven are remixes of the region bosses; rung 7 is new."); L(); }
    if (kind == "world") { L("One per pair of neighbouring regions (the abyss has its own). Each is huge (×4–5), spawns on a timer in a dedicated field and is built on a different rig."); L(); }
    if (kind == "region") { L("These exist today with full `BossKit`s and are kept as they are; listed for completeness."); L(); }
    foreach (var n in list)
    {
        string baseTxt = n.Base.StartsWith("NEW") ? n.Base : $"`{n.Base}`" + (typeById.TryGetValue(n.Base, out var bt) ? $" {bt.Zh}" : "");
        L($"- **`{n.Id}` {n.Zh}** — {n.En}  ");
        L($"  Base: {baseTxt}. Where: {n.Where} ({n.Region}).  ");
        L($"  Look: {n.Look}  ");
        if (n.Mechanics.Count == 1) L($"  Signature mechanic: {n.Mechanics[0]}");
        else { L("  Signature mechanics:"); for (int i = 0; i < n.Mechanics.Count; i++) L($"  {i + 1}. {n.Mechanics[i]}"); }
    }
    L();
}

// 5 ── behaviours
L("## 5. New AI behaviours");
L();
L("Existing kinds (`Melee`, `Ranged`, `Caster`, `Exploder`, `Teleporter`, `Brute`, `Support`, `Leaper`) are unchanged. The twelve new kinds below extend `MonAI`; the traits are data flags on `MonsterDef` that can be combined with any kind. \"x\" after a number is a multiple of the monster's `BaseDamage`. Every dangerous effect is telegraphed with `Fx.Telegraph*` for at least 0.6 s, damage goes through `Combat.MonsterHit` / `Combat.MonsterArea`, ground effects through `GroundZone.SpawnMonster`.");
L();
foreach (var (kind, title) in new[] { ("ai", "5.1 New AI kinds"), ("trait", "5.2 Traits") })
{
    L($"### {title}");
    L();
    foreach (var b in behaviours.Where(x => x.Kind == kind))
    {
        L($"#### {b.Token} {b.Zh}");
        L();
        L($"- **Trigger:** {b.Trigger}");
        L($"- **Telegraph:** {b.Telegraph}");
        L($"- **Effect:** {b.Effect}");
        L($"- **Cooldown:** {b.Cooldown}");
        L($"- **Data fields:** {(b.Data == "none" ? "none" : string.Join(", ", b.Data.Split(',').Select(s => $"`{s.Trim()}`")))}");
        L($"- **Used by ({b.UsedBy.Count}):** {string.Join(", ", b.UsedBy.Select(x => $"`{x}`"))}");
        L();
    }
}

// 6 ── production
L("## 6. Production plan");
L();
L("The order below reaches full coverage fastest for the work spent: first everything that needs no new sculpt, then the Rig A bases that unlock the most variants, then the variants, then the oversized bodies, and only then the new rigs — cheapest and widest-reaching first. Rig A is complete at the end of stage 4 with " + types.Count(t => t.Rig == "A") + " of the " + types.Count + " types, which is enough to ship every region with its signature families; rigs B–F then add the other body plans without changing any spawn table (the types are already listed and simply start appearing).");
L();
L("| stage | what | models built | regular types available | new behaviours first needed |");
L("|---|---|---|---|---|");
var seenTokens = new HashSet<string>();
int cum = 0, cumModels = 0;
foreach (var (n, title, what) in stages.Where(s => s.n <= maxStage))
{
    var st = types.Where(t => t.Stage == n).ToList();
    cum += st.Count; cumModels += ModelsIn(n);
    var fresh = st.SelectMany(t => Tokens(t.Ai)).Where(x => !existingKinds.Contains(x)).Distinct().Where(seenTokens.Add).ToList();
    L($"| {n} | {title} | {ModelsIn(n)} (total {cumModels}) | {cum} | {(fresh.Count == 0 ? "—" : string.Join(", ", fresh))} |");
}
L();
foreach (var (n, title, what) in stages)
{
    L($"**Stage {n} — {title}.** {what}");
    L();
    if (n > maxStage) continue;
    var st = types.Where(t => t.Stage == n).ToList();
    if (n == 0) { L($"On disk: {string.Join(", ", st.Where(t => t.Id != "imp").Select(t => $"`mon_{t.Id}`"))}."); L(); continue; }
    var planned = st.Where(t => t.Build == "planned").ToList();
    if (planned.Count > 0) L($"- Finish ({planned.Count}): {string.Join(", ", planned.Select(t => $"`{t.Id}` {t.Zh}"))}.");
    var bases = st.Where(t => t.Build == "base").ToList();
    if (bases.Count > 0) L($"- Bases ({bases.Count}): {string.Join(", ", bases.Select(t => $"`{t.Id}` {t.Zh}"))}.");
    var vars = st.Where(t => t.Build == "variant").ToList();
    if (n == 1) vars.Insert(0, typeById["imp"]);
    if (vars.Count > 0)
    {
        L($"- Variants ({vars.Count}):");
        foreach (var g in vars.GroupBy(t => t.VariantOf)) L($"  - on `{g.Key}`: {string.Join(", ", g.Select(t => $"`{t.Id}` {t.Zh}"))}");
    }
    L();
}
L("### 6.1 Rig A bases, by how many variants each unlocks");
L();
L("| base | 中文名 | body | state | variants it unlocks |");
L("|---|---|---|---|---|");
foreach (var b in types.Where(t => t.Rig == "A" && t.Build != "variant").OrderByDescending(b => types.Count(t => t.VariantOf == b.Id)).ThenBy(b => b.Stage))
{
    var v = types.Where(t => t.VariantOf == b.Id).ToList();
    L($"| `{b.Id}` | {b.Zh} | {bodyName[b.Body]} | {(b.Build == "base" ? $"new (stage {b.Stage})" : b.Build == "built" ? "built" : "planned")} | {(v.Count == 0 ? "—" : $"{v.Count}: " + string.Join(", ", v.Select(t => $"`{t.Id}`")))} |");
}
L();
L("Note: `skeleton_thrower` is itself a built variant of `skeleton`; `cult_censer` hangs off `cult_caster`.");
L();
L("### 6.2 Rigs B–F and the creatures on each");
L();
L("Each rig is one small skeleton with the standard clip set (`Idle / Walk / Run / Attack1 / Attack2 / Hit / Death / Spawn`). A \"body build\" is a base sculpt on that skeleton; the creatures listed after it reuse its builder with new parts and proportions.");
L();
L("| rig | body build (base) | creatures built on it | count |");
L("|---|---|---|---|");
foreach (var rig in new[] { "F", "B", "C", "D", "E" })
{
    var onRig = types.Where(t => t.Rig == rig).ToList();
    bool first = true;
    foreach (var b in onRig.Where(t => t.Build == "base"))
    {
        var v = onRig.Where(t => t.VariantOf == b.Id).ToList();
        L($"| {(first ? $"**{rig}** {rigName[rig]} — {onRig.Count} creatures" : "")} | `{b.Id}` {b.Zh} | {(v.Count == 0 ? "—" : string.Join(", ", v.Select(t => $"`{t.Id}` {t.Zh}")))} | {1 + v.Count} |");
        first = false;
    }
}
L();
L("### 6.3 Regular types available per region after each stage");
L();
L("| region | " + string.Join(" | ", Enumerable.Range(0, maxStage + 1).Select(n => $"stage {n}")) + " |");
L("|---|" + string.Concat(Enumerable.Repeat("---|", maxStage + 1)));
foreach (var r in regions)
    L($"| {r.Zh} `{r.Id}` | " + string.Join(" | ", Enumerable.Range(0, maxStage + 1).Select(n => types.Count(t => t.Stage <= n && t.Regions.Contains(r.Letter)).ToString())) + " |");
L();
int rigABases = types.Count(t => t.Rig == "A" && t.Build == "base");
int rigAVars = types.Count(t => t.Rig == "A" && t.Build == "variant");
int otherRigs = types.Count(t => t.Rig != "A");
int builtNow = types.Count(t => t.Build == "built");
int plannedNow = types.Count(t => t.Build == "planned");
L("### 6.4 Totals");
L();
L("| | models |");
L("|---|---|");
L($"| On disk today | {builtNow} |");
L($"| Planned in the current 17, still to build | {plannedNow} |");
L($"| New Rig A bases | {rigABases} |");
L($"| New Rig A variants (including the cinderling's own model) | {rigAVars} |");
L($"| Rigs B–F ({types.Count(t => t.Rig != "A" && t.Build == "base")} bases + {types.Count(t => t.Rig != "A" && t.Build == "variant")} variants) | {otherRigs} |");
L($"| **Regular models at completion** | **{types.Count}** |");
L($"| Still to build | {types.Count - builtNow} |");
L($"| Named content: new models | 8 (3 rigid side-dungeon bosses, 4 world bosses, 1 pinnacle boss) |");
L($"| Named content: dressing passes on existing bodies | {named.Count(n => n.Kind == "rare") + named.Count(n => n.Kind == "stronghold") + named.Count(n => n.Kind == "dungeon") - 3 + 7} (28 rare elites, 7 stronghold bosses, 11 side-dungeon bosses, 7 pinnacle remixes) |");
L();

// 7 ── appendix
L("## 7. Machine-readable appendix");
L();
L("`Y:\\game\\data\\bestiary.json` (UTF-8, generated from the same source as this document) contains:");
L();
L("- `meta` — title, counts, the region-letter, rig and body-plan legends.");
L("- `families[]` — id, name_zh, name_en, scope, concept, lore, roles_have, roles_lack, regions, signature_in, rigs, types.");
L("- `types[]` — every regular type with all fields of the tables in section 2: id, name_zh, name_en, family, role, ai, ai_kind, ai_traits, rig, rig_note, body_plan, build, variant_of, model, element, status, hp, dmg, speed, scale, anims, regions, spawn_weight, stage, silhouette.");
L("- `reskins[]` — the regional reskins of section 3.9.");
L("- `regions[]` — act, level, families with weights and signature flags, the flat spawn list, dungeons with their families and bosses, stronghold, region boss, world boss and rare elites.");
L("- `named` — `rare_elites`, `stronghold_bosses`, `dungeon_bosses`, `region_bosses`, `world_bosses`, `pinnacle_bosses`: id, name_zh, name_en, base, region, where, look, mechanics.");
L("- `behaviours[]` — the specs of section 5 with the types that use each.");
L("- `production[]` — the stages of section 6 with their model lists.");
L();

// 8 ── counts
L("## 8. Count check");
L();
L($"Computed by the build from the tables above ({types.Count} regular types in {families.Count} families).");
L();
L("**Types per family** (target 4–6; smallest 2–3; largest 8–10 for the catch-alls)");
L();
L("| family | 中文名 | types | humanoid | bruiser | other body plans | rigs |");
L("|---|---|---|---|---|---|---|");
foreach (var f in families)
{
    var other = f.Types.Where(t => t.Body is not ("H" or "B")).GroupBy(t => t.Body).Select(g => $"{bodyName[g.Key]} ×{g.Count()}");
    L($"| `{f.Id}` | {f.Zh} | {f.Types.Count} | {f.Types.Count(t => t.Body == "H")} | {f.Types.Count(t => t.Body == "B")} | {string.Join(", ", other)} | {string.Join(", ", f.Types.GroupBy(t => t.Rig).OrderBy(g => g.Key).Select(g => $"{g.Key}×{g.Count()}"))} |");
}
L($"| **total** | {families.Count} families | **{types.Count}** | {types.Count(t => t.Body == "H")} | {types.Count(t => t.Body == "B")} | {types.Count(t => t.Body is not ("H" or "B"))} | |");
L();
L($"Smallest family: {families.Min(f => f.Types.Count)} types ({string.Join(", ", families.Where(f => f.Types.Count == families.Min(x => x.Types.Count)).Select(f => f.Id))}). Largest: {families.Max(f => f.Types.Count)} ({string.Join(", ", families.Where(f => f.Types.Count == families.Max(x => x.Types.Count)).Select(f => f.Id))}). Families with 4–6 types: {families.Count(f => f.Types.Count is >= 4 and <= 6)} of {families.Count}.");
L();
L("**Per body plan**");
L();
L("| body plan | types | share | target |");
L("|---|---|---|---|");
foreach (var b in bodyOrder) L($"| {bodyName[b]} | {types.Count(t => t.Body == b)} | {(100.0 * types.Count(t => t.Body == b) / types.Count).ToString("0.0", inv)}% | {bodyTarget[b]} |");
L($"| **total** | **{types.Count}** | 100% | |");
L();
L("**Per rig**");
L();
L("| rig | types | existing | new bases | new variants | body plans on it |");
L("|---|---|---|---|---|---|");
foreach (var rig in rigName.Keys)
{
    var on = types.Where(t => t.Rig == rig).ToList();
    L($"| {rig} — {rigName[rig]} | {on.Count} | {on.Count(t => t.Build is "built" or "planned") + (rig == "A" ? 1 : 0)} | {on.Count(t => t.Build == "base")} | {on.Count(t => t.Build == "variant") - (rig == "A" ? 1 : 0)} | {string.Join(", ", on.GroupBy(t => t.Body).OrderByDescending(g => g.Count()).Select(g => $"{bodyName[g.Key]} ×{g.Count()}"))} |");
}
L($"| **total** | **{types.Count}** | {types.Count(t => t.Build is "built" or "planned") + 1} | {types.Count(t => t.Build == "base")} | {types.Count(t => t.Build == "variant") - 1} | rigs B–F together: {types.Count(t => t.Rig != "A")} |");
L();
L("(The cinderling `imp` is counted as an existing id; its own model is one of the stage-1 variants.)");
L();
L("**Per region**");
L();
L("| region | families | signature | shared | regular types present | of which Rig A | dungeons (families each) | rare elites |");
L("|---|---|---|---|---|---|---|---|");
foreach (var r in regions)
{
    var here = types.Where(t => t.Regions.Contains(r.Letter)).ToList();
    L($"| {r.Zh} `{r.Id}` | {r.Families.Count} | {r.Families.Count(f => f.sig)} | {r.Families.Count(f => !f.sig)} | {here.Count} | {here.Count(t => t.Rig == "A")} | {string.Join(" / ", r.Dungeons.Select(d => d.Families.Count))} | {named.Count(n => n.Kind == "rare" && n.Region == r.Id)} |");
}
L();
L($"Families present in the most regions: {string.Join(", ", families.OrderByDescending(f => FamRegions(f).Count).Take(6).Select(f => $"`{f.Id}` {FamRegions(f).Count}"))}. Single-region families: {families.Count(f => FamRegions(f).Count == 1)}.");
L();
L("**Per role and per AI kind**");
L();
L("| main role | types | | AI kind | types |");
L("|---|---|---|---|---|");
var roleCounts = types.GroupBy(MainRole).OrderByDescending(g => g.Count()).Select(g => (g.Key, g.Count())).ToList();
var kindCounts = types.GroupBy(t => Tokens(t.Ai)[0]).OrderByDescending(g => g.Count()).Select(g => (g.Key, g.Count())).ToList();
for (int i = 0; i < Math.Max(roleCounts.Count, kindCounts.Count); i++)
    L($"| {(i < roleCounts.Count ? roleCounts[i].Item1 : "")} | {(i < roleCounts.Count ? roleCounts[i].Item2.ToString() : "")} | | {(i < kindCounts.Count ? kindCounts[i].Item1 + (existingKinds.Contains(kindCounts[i].Item1) ? "" : " (new)") : "")} | {(i < kindCounts.Count ? kindCounts[i].Item2.ToString() : "")} |");
L();
L("**Named content**");
L();
L("| kind | count | target |");
L("|---|---|---|");
L($"| named rare elites | {named.Count(n => n.Kind == "rare")} | 28 (4 per region) |");
L($"| stronghold bosses | {named.Count(n => n.Kind == "stronghold")} | 7 |");
L($"| side-dungeon bosses | {named.Count(n => n.Kind == "dungeon")} | 14 (2 per region) |");
L($"| region bosses (existing) | {named.Count(n => n.Kind == "region")} | 7 |");
L($"| world bosses | {named.Count(n => n.Kind == "world")} | 3–4 |");
L($"| pinnacle ladder bosses | {named.Count(n => n.Kind == "pinnacle")} | 8 |");
L($"| regional reskins | {reskins.Count} ({reskins.Count(r => r.State == "existing")} existing, {reskins.Count(r => r.State == "new")} new) | — |");
L($"| new AI kinds / traits | {behaviours.Count(b => b.Kind == "ai")} / {behaviours.Count(b => b.Kind == "trait")} | — |");
L();
md.Append(Text("notes.md"));

File.WriteAllText(mdOut, md.ToString().Replace("\n", "\r\n"), new UTF8Encoding(false));

// ───────────────────────── json ─────────────────────────
JsonArray Arr(IEnumerable<string> xs) { var a = new JsonArray(); foreach (var x in xs) a.Add((JsonNode)JsonValue.Create(x)); return a; }
JsonObject NamedObj(Named n) => new()
{
    ["id"] = n.Id, ["name_zh"] = n.Zh, ["name_en"] = n.En, ["kind"] = n.Kind, ["base"] = n.Base, ["region"] = n.Region, ["where"] = n.Where, ["look"] = n.Look, ["mechanics"] = Arr(n.Mechanics),
};
var j = new JsonObject
{
    ["meta"] = new JsonObject
    {
        ["title"] = "燼淵 Ashen Abyss — bestiary",
        ["note"] = "Generated by Y:\\tools\\bestiary\\build.cs from Y:\\tools\\bestiary\\src. Design data: not loaded by the game yet.",
        ["counts"] = new JsonObject
        {
            ["families"] = families.Count, ["regular_types"] = types.Count, ["reskins"] = reskins.Count,
            ["rare_elites"] = named.Count(n => n.Kind == "rare"), ["stronghold_bosses"] = named.Count(n => n.Kind == "stronghold"), ["dungeon_bosses"] = named.Count(n => n.Kind == "dungeon"),
            ["region_bosses"] = named.Count(n => n.Kind == "region"), ["world_bosses"] = named.Count(n => n.Kind == "world"), ["pinnacle_bosses"] = named.Count(n => n.Kind == "pinnacle"),
            ["by_rig"] = new JsonObject(rigName.Keys.Select(r => KeyValuePair.Create(r, (JsonNode)types.Count(t => t.Rig == r)))),
            ["by_body_plan"] = new JsonObject(bodyOrder.Select(b => KeyValuePair.Create(b, (JsonNode)types.Count(t => t.Body == b)))),
            ["by_region"] = new JsonObject(regions.Select(r => KeyValuePair.Create(r.Id, (JsonNode)types.Count(t => t.Regions.Contains(r.Letter))))),
            ["by_family"] = new JsonObject(families.Select(f => KeyValuePair.Create(f.Id, (JsonNode)f.Types.Count))),
        },
        ["rigs"] = new JsonObject(rigName.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)kv.Value))),
        ["body_plans"] = new JsonObject(bodyOrder.Select(b => KeyValuePair.Create(b, (JsonNode)bodyName[b]))),
        ["existing_ai_kinds"] = Arr(existingKinds),
        ["clip_set_rigs_b_to_f"] = Arr(new[] { "Idle", "Walk", "Run", "Attack1", "Attack2", "Hit", "Death", "Spawn" }),
    },
};
var jf = new JsonArray();
foreach (var f in families)
    jf.Add(new JsonObject
    {
        ["id"] = f.Id, ["name_zh"] = f.Zh, ["name_en"] = f.En, ["scope"] = f.Scope, ["concept"] = f.Concept, ["lore"] = f.Lore,
        ["roles_have"] = Arr(f.Has.Split(", ")), ["roles_lack"] = f.Lacks,
        ["regions"] = Arr(FamRegions(f).Select(l => regionOf[l])), ["signature_in"] = Arr(FamRegions(f).Where(l => IsSig(f, l)).Select(l => regionOf[l])),
        ["rigs"] = Arr(f.Types.Select(t => t.Rig).Distinct().OrderBy(x => x)), ["types"] = Arr(f.Types.Select(t => t.Id)),
    });
j["families"] = jf;
var jt = new JsonArray();
foreach (var t in types)
{
    var tk = Tokens(t.Ai);
    jt.Add(new JsonObject
    {
        ["id"] = t.Id, ["name_zh"] = t.Zh, ["name_en"] = t.En, ["family"] = t.Family, ["role"] = t.Role,
        ["ai"] = t.Ai, ["ai_kind"] = tk[0], ["ai_traits"] = Arr(tk.Skip(1)),
        ["rig"] = t.Rig, ["rig_note"] = t.RigNote, ["body_plan"] = bodyName[t.Body],
        ["build"] = t.Build, ["variant_of"] = t.VariantOf, ["model"] = "mon_" + t.Id,
        ["element"] = t.Element, ["status"] = t.Status, ["hp"] = t.Hp, ["dmg"] = t.Dmg, ["speed"] = t.Speed, ["scale"] = t.Scale,
        ["anims"] = t.Anims, ["regions"] = Arr(regionOrder.Where(t.Regions.Contains).Select(l => regionOf[l])), ["spawn_weight"] = t.W, ["stage"] = t.Stage, ["silhouette"] = t.Brief,
    });
}
j["types"] = jt;
var jr = new JsonArray();
foreach (var r in reskins)
    jr.Add(new JsonObject { ["id"] = r.Id, ["base"] = r.Base, ["region"] = regionOf[r.Region], ["name_zh"] = r.Zh, ["element"] = r.Element, ["status"] = r.Status, ["state"] = r.State, ["look"] = r.Look });
j["reskins"] = jr;
var jreg = new JsonArray();
foreach (var r in regions)
{
    var fams = new JsonArray();
    foreach (var f in r.Families)
        fams.Add(new JsonObject { ["id"] = f.id, ["weight"] = f.w, ["signature"] = f.sig, ["types"] = Arr(famById[f.id].Types.Where(t => t.Regions.Contains(r.Letter)).Select(t => SpawnId(t, r.Letter))) });
    var sp = new JsonArray();
    foreach (var s in r.Spawns) sp.Add(new JsonObject { ["id"] = s.id, ["base"] = s.baseId, ["family"] = s.fam, ["w"] = s.w });
    var dn = new JsonArray();
    foreach (var d in r.Dungeons) dn.Add(new JsonObject { ["id"] = d.Id, ["name_zh"] = d.Zh, ["families"] = Arr(d.Families), ["boss"] = d.Boss, ["note"] = d.Note });
    jreg.Add(new JsonObject
    {
        ["id"] = r.Id, ["name_zh"] = r.Zh, ["act"] = r.Act, ["level"] = r.Level, ["note"] = r.Note, ["families"] = fams, ["spawns"] = sp, ["dungeons"] = dn,
        ["stronghold"] = r.Stronghold, ["stronghold_boss"] = r.StrongholdBoss, ["region_boss"] = r.Boss, ["world_boss"] = r.WorldBoss,
        ["rare_elites"] = Arr(named.Where(n => n.Kind == "rare" && n.Region == r.Id).Select(n => n.Id)),
    });
}
j["regions"] = jreg;
var jn = new JsonObject();
foreach (var (kind, key) in new[] { ("rare", "rare_elites"), ("stronghold", "stronghold_bosses"), ("dungeon", "dungeon_bosses"), ("region", "region_bosses"), ("world", "world_bosses"), ("pinnacle", "pinnacle_bosses") })
{
    var a = new JsonArray();
    foreach (var n in named.Where(x => x.Kind == kind)) a.Add(NamedObj(n));
    jn[key] = a;
}
j["named"] = jn;
var jb = new JsonArray();
foreach (var b in behaviours)
    jb.Add(new JsonObject { ["id"] = b.Token, ["type"] = b.Kind, ["name_zh"] = b.Zh, ["trigger"] = b.Trigger, ["telegraph"] = b.Telegraph, ["effect"] = b.Effect, ["cooldown"] = b.Cooldown,
        ["data_fields"] = Arr(b.Data == "none" ? Array.Empty<string>() : b.Data.Split(',').Select(s => s.Trim())), ["used_by"] = Arr(b.UsedBy) });
j["behaviours"] = jb;
var jp = new JsonArray();
foreach (var (n, title, what) in stages)
    jp.Add(new JsonObject { ["stage"] = n, ["title"] = title, ["what"] = what, ["models"] = n <= maxStage ? ModelsIn(n) : 8,
        ["types"] = Arr(types.Where(t => t.Stage == n).Select(t => t.Id)) });
j["production"] = jp;

Directory.CreateDirectory(Path.GetDirectoryName(jsonOut));
using (var fs = File.Create(jsonOut))
using (var jw = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    j.WriteTo(jw);

// ───────────────────────── console summary ─────────────────────────
Console.WriteLine($"families {families.Count}  types {types.Count}  reskins {reskins.Count}  named {named.Count}");
Console.WriteLine("per family: " + string.Join(" ", families.Select(f => $"{f.Id}={f.Types.Count}")));
Console.WriteLine("per rig:    " + string.Join(" ", rigName.Keys.Select(r => $"{r}={types.Count(t => t.Rig == r)}")));
Console.WriteLine("per body:   " + string.Join(" ", bodyOrder.Select(b => $"{b}={types.Count(t => t.Body == b)}")));
Console.WriteLine("per region: " + string.Join(" ", regions.Select(r => $"{r.Id}={types.Count(t => t.Regions.Contains(r.Letter))}/{r.Families.Count}fam")));
Console.WriteLine("per stage:  " + string.Join(" ", Enumerable.Range(0, maxStage + 1).Select(n => $"{n}={types.Count(t => t.Stage == n)}")));
Console.WriteLine("named:      " + string.Join(" ", namedKinds.Select(k => $"{k.Item1}={named.Count(n => n.Kind == k.Item1)}")));
Console.WriteLine($"wrote {mdOut} ({new FileInfo(mdOut).Length / 1024} KB) and {jsonOut} ({new FileInfo(jsonOut).Length / 1024} KB)");
return 0;

// ───────────────────────── model ─────────────────────────
class Fam { public string Id, Zh, En, Scope, Concept, Lore, Has, Lacks; public List<Typ> Types = new(); }
class Typ
{
    public string Id, Zh, En, Role, Ai, Rig, RigNote, Body, Build, VariantOf, Element, Status, Anims, Brief, Family;
    public double Hp, Dmg, Speed, Scale, W; public int Stage; public List<string> Regions;
}
class Beh { public string Token, Kind, Zh, Trigger, Telegraph, Effect, Cooldown, Data; public List<string> UsedBy = new(); }
class Reskin { public string Id, Base, Region, Zh, Element, Status, State, Look; }
class Dun { public string Id, Zh, Boss, Note; public List<string> Families; }
class Reg
{
    public string Id, Zh, Letter, Boss, Stronghold, StrongholdBoss, WorldBoss, Note; public int Act, Level;
    public List<(string id, double w, bool sig)> Families = new(); public List<Dun> Dungeons = new();
    public List<(string id, string baseId, string fam, double w)> Spawns = new();
}
class Named { public string Kind, Id, Zh, En, Base, Region, Where, Look; public List<string> Mechanics; }

using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AshenAbyss;

/// <summary>One block of the credits roll: an optional title and its lines (a line starting with <see cref="CreditsData.DimMark"/> is rendered dim).</summary>
public sealed class CreditSection
{
    public string Title;
    public bool Heading;                 // a chapter heading, rendered larger than a normal section title
    public readonly List<string> Lines = new();
}

/// <summary>
/// The credits shown by the title screen: the game's own credit, then every third-party asset and its licence.
/// The asset records are read from the project's credits folder (credits\*.md, Markdown) when it can be found;
/// anything those files do not mention is filled in from the built-in list below, so the roll is never empty.
/// </summary>
public static class CreditsData
{
    public const string GameTitle = "燼淵", GameTitleLatin = "ASHEN ABYSS";
    public const char DimMark = '\u0001';
    private const int MaxLines = 4000, MaxFileBytes = 512 * 1024;

    private static List<CreditSection> _cache;
    /// <summary>Where the third-party list came from (a folder path, or the built-in list) — for the log and the roll's footer.</summary>
    public static string Source { get; private set; } = "內建清單";

    public static string Version
    {
        get
        {
            try
            {
                string v = ProjectSettings.GetSetting("application/config/version").AsString();
                return string.IsNullOrWhiteSpace(v) ? "0.4.0" : v;
            }
            catch (Exception) { return "0.4.0"; }
        }
    }

    /// <summary>Forgets the cached roll so the files are read again next time.</summary>
    public static void Reload() => _cache = null;

    public static List<CreditSection> Sections()
    {
        if (_cache != null) return _cache;
        var list = new List<CreditSection>();
        AddGame(list);

        var third = new List<CreditSection>();
        string haystack = "";
        try { haystack = ReadFiles(third); }
        catch (Exception e) { Log.Error("credits", "reading the credit files failed, using the built-in list", e); third.Clear(); haystack = ""; }

        if (third.Count > 0)
        {
            list.Add(new CreditSection { Title = "第三方素材與授權", Heading = true });
            list.AddRange(third);
        }
        AddBuiltIn(list, haystack, third.Count > 0);
        AddClosing(list);
        Log.Info("credits", $"credits built: {list.Count} sections, {list.Sum(s => s.Lines.Count)} lines, source={Source}");
        _cache = list;
        return list;
    }

    // ───────────────────────── the game's own credit ─────────────────────────

    private static void AddGame(List<CreditSection> list)
    {
        var head = new CreditSection { Title = $"{GameTitle}　{GameTitleLatin}", Heading = true };
        head.Lines.Add("原創黑暗奇幻動作角色扮演遊戲");
        head.Lines.Add(DimMark + $"版本 {Version}");
        list.Add(head);

        var team = new CreditSection { Title = "製作" };
        team.Lines.Add("世界觀與遊戲設計　　燼淵製作組");
        team.Lines.Add("程式與系統　　燼淵製作組");
        team.Lines.Add("場景、角色與特效整合　　燼淵製作組");
        team.Lines.Add("音樂編寫與音效設計　　燼淵製作組");
        team.Lines.Add("文本與配音腳本　　燼淵製作組");
        list.Add(team);

        var own = new CreditSection { Title = "原創聲明" };
        own.Lines.Add("瑟蘭的世界、其中的人物、名稱與所有文字，皆為本作原創。");
        own.Lines.Add("如與任何既有作品相似，純屬巧合。");
        list.Add(own);
    }

    private static void AddClosing(List<CreditSection> list)
    {
        var thanks = new CreditSection { Title = "致謝" };
        thanks.Lines.Add("謝謝每一位以開放授權分享作品的創作者——");
        thanks.Lines.Add("你們留下的火種，照亮了這趟旅途。");
        thanks.Lines.Add("");
        thanks.Lines.Add("也謝謝走到這裡的你。");
        list.Add(thanks);

        var end = new CreditSection { Title = "願你的燼火不熄", Heading = true };
        end.Lines.Add(DimMark + $"素材清單來源：{Source}");
        list.Add(end);
    }

    // ───────────────────────── built-in third-party list ─────────────────────────

    private sealed class BuiltIn
    {
        public string Title;
        public string[] Keys;            // if any of these appears in the credit files, the entry is already covered
        public string[] Lines;
    }

    private static readonly BuiltIn[] BuiltIns =
    {
        new()
        {
            Title = "遊戲引擎", Keys = new[] { "godot" },
            Lines = new[]
            {
                "Godot Engine 4",
                "© Juan Linietsky、Ariel Manzur 與 Godot Engine 貢獻者",
                "\u0001MIT 授權　godotengine.org",
            },
        },
        new()
        {
            Title = "角色與動畫", Keys = new[] { "quaternius" },
            Lines = new[]
            {
                "Universal Animation Library 1、2　　Quaternius",
                "Universal Base Characters　　Quaternius",
                "\u0001CC0 1.0 公眾領域貢獻宣告　quaternius.com",
            },
        },
        new()
        {
            Title = "環境材質、模型與天空", Keys = new[] { "poly haven", "polyhaven" },
            Lines = new[]
            {
                "Poly Haven　材質、模型與 HDRI",
                "\u0001CC0 1.0 公眾領域貢獻宣告　polyhaven.com",
            },
        },
        new()
        {
            Title = "環境材質", Keys = new[] { "ambientcg" },
            Lines = new[]
            {
                "ambientCG　PBR 材質　　Lennart Demes",
                "\u0001CC0 1.0 公眾領域貢獻宣告　ambientcg.com",
            },
        },
        new()
        {
            Title = "圖示", Keys = new[] { "game-icons" },
            Lines = null,                // filled at runtime with the authors actually used
        },
        new()
        {
            Title = "字型", Keys = new[] { "noto" },
            Lines = new[]
            {
                "Noto Serif TC、Noto Sans TC　　Google 與 Adobe",
                "\u0001SIL Open Font License 1.1",
            },
        },
        new()
        {
            Title = "拉丁標題字型", Keys = new[] { "cinzel" },
            Lines = new[]
            {
                "Cinzel　　Natanael Gama（The Cinzel Project Authors）",
                "\u0001SIL Open Font License 1.1",
            },
        },
        new()
        {
            Title = "音色庫", Keys = new[] { "musescore" },
            Lines = new[]
            {
                "MuseScore General SoundFont　　S. Christian Collins",
                "源自 FluidR3Mono（Michael Cowgill）與 Fluid R3（Frank Wen）",
                "\u0001MIT 授權",
            },
        },
        new()
        {
            Title = "音樂合成", Keys = new[] { "meltysynth" },
            Lines = new[]
            {
                "MeltySynth　SoundFont 合成器　　Nobuaki Tanaka（sinshu）",
                "\u0001MIT 授權",
            },
        },
        new()
        {
            Title = "執行環境", Keys = new[] { ".net foundation", "dotnet" },
            Lines = new[]
            {
                ".NET　　Microsoft 與 .NET Foundation",
                "\u0001MIT 授權",
            },
        },
    };

    private static void AddBuiltIn(List<CreditSection> list, string haystack, bool filesPresent)
    {
        bool headed = false;
        foreach (var b in BuiltIns)
        {
            bool covered = false;
            foreach (var k in b.Keys) if (haystack.Contains(k, StringComparison.Ordinal)) { covered = true; break; }
            if (covered) continue;
            if (!headed)
            {
                list.Add(new CreditSection { Title = filesPresent ? "其他採用的開放資源" : "第三方素材與授權", Heading = true });
                headed = true;
            }
            var s = new CreditSection { Title = b.Title };
            if (b.Lines != null) s.Lines.AddRange(b.Lines);
            else
            {
                s.Lines.Add("game-icons.net　遊戲圖示集");
                s.Lines.Add(DimMark + "創用 CC 姓名標示 3.0（CC BY 3.0）　creativecommons.org/licenses/by/3.0");
                s.Lines.Add("作者");
                foreach (var row in Rows(IconAuthors(), 4)) s.Lines.Add(row);
            }
            list.Add(s);
        }
    }

    private static IEnumerable<string> Rows(IReadOnlyList<string> names, int perRow)
    {
        for (int i = 0; i < names.Count; i += perRow)
            yield return string.Join("　·　", names.Skip(i).Take(perRow));
    }

    // game-icons.net folder name → the author's credited name
    private static readonly Dictionary<string, string> AuthorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["andymeneely"] = "Andy Meneely", ["aussiesim"] = "Aussiesim", ["carl-olsen"] = "Carl Olsen", ["caro-asercion"] = "Caro Asercion",
        ["cathelineau"] = "Cathelineau", ["catsu"] = "Catsu", ["darkzaitzev"] = "DarkZaitzev", ["delapouite"] = "Delapouite",
        ["faithtoken"] = "Faithtoken", ["felbrigg"] = "Felbrigg", ["generalace135"] = "GeneralAce135", ["guard13007"] = "Guard13007",
        ["heavenly-dog"] = "HeavenlyDog", ["irongamer"] = "Irongamer", ["john-colburn"] = "John Colburn", ["john-redman"] = "John Redman",
        ["kier-heyl"] = "Kier Heyl", ["lorc"] = "Lorc", ["lord-berandas"] = "Lord Berandas", ["lucasms"] = "Lucas",
        ["pepijn-poolman"] = "Pepijn Poolman", ["pierre-leducq"] = "Pierre Leducq", ["priorblue"] = "PriorBlue", ["quoting"] = "Quoting",
        ["rihlsul"] = "Rihlsul", ["sbed"] = "Sbed", ["skoll"] = "Skoll", ["sparker"] = "Sparker", ["spencerdub"] = "SpencerDub",
        ["starseeker"] = "Starseeker", ["various-artists"] = "Various artists", ["viscious-speed"] = "Viscious Speed",
        ["willdabeast"] = "Willdabeast", ["zajkonur"] = "Zajkonur", ["zeromancer"] = "Zeromancer",
    };

    private static readonly string[] DefaultAuthors =
    {
        "Lorc", "Delapouite", "Skoll", "Caro Asercion", "Viscious Speed", "Sbed", "Aussiesim", "DarkZaitzev", "Cathelineau",
        "Quoting", "Faithtoken", "Lord Berandas", "PriorBlue", "Willdabeast", "Carl Olsen", "Felbrigg", "Lucas", "Guard13007",
        "John Redman", "Kier Heyl", "Pierre Leducq", "Rihlsul", "Andy Meneely", "GeneralAce135", "HeavenlyDog", "Zajkonur",
        "Zeromancer", "Catsu", "Irongamer", "John Colburn", "Pepijn Poolman", "Sparker", "SpencerDub", "Starseeker",
    };

    /// <summary>Authors of the game-icons.net icons shipped with the game (from the icon index when present), most prolific first.</summary>
    public static IReadOnlyList<string> IconAuthors()
    {
        try
        {
            const string index = "res://assets/icons/index.txt";
            if (Godot.FileAccess.FileExists(index))
            {
                string text = Godot.FileAccess.GetFileAsString(index);
                var count = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in text.Split('\n'))
                {
                    string line = raw.Trim().Replace('\\', '/');
                    int gi = line.IndexOf("gi/", StringComparison.OrdinalIgnoreCase);
                    if (gi < 0) continue;
                    int start = gi + 3, end = line.IndexOf('/', start);
                    if (end <= start) continue;
                    string folder = line.Substring(start, end - start);
                    count[folder] = count.TryGetValue(folder, out int n) ? n + 1 : 1;
                }
                if (count.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var kv in count.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        string name = AuthorNames.TryGetValue(kv.Key, out var n) ? n : Pretty(kv.Key);
                        if (!names.Contains(name)) names.Add(name);
                    }
                    return names;
                }
            }
        }
        catch (Exception e) { Log.Warn("credits", "icon index unreadable, using the built-in author list: " + e.Message); }
        return DefaultAuthors;
    }

    private static string Pretty(string folder)
    {
        var parts = folder.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        return parts.Length == 0 ? folder : string.Join(' ', parts);
    }

    // ───────────────────────── credit files (Markdown) ─────────────────────────

    private static List<string> CandidateDirs()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        void Add(string p)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                string full = Path.GetFullPath(p);
                if (seen.Add(full)) list.Add(full);
            }
            catch (Exception) { }
        }
        string res = null;
        try { res = ProjectSettings.GlobalizePath("res://"); } catch (Exception) { }
        if (!string.IsNullOrEmpty(res))
        {
            Add(Path.Combine(res, "credits"));                 // shipped inside the project
            Add(Path.Combine(res, "..", "credits"));           // the workspace folder next to the project
        }
        try
        {
            string exe = OS.GetExecutablePath();
            if (!string.IsNullOrEmpty(exe)) Add(Path.Combine(Path.GetDirectoryName(exe) ?? "", "credits"));   // next to an exported build
        }
        catch (Exception) { }
        Add(@"Y:\credits");
        return list;
    }

    /// <summary>Parses the first credits folder that has Markdown files. Returns their lower-cased text for coverage checks.</summary>
    private static string ReadFiles(List<CreditSection> into)
    {
        foreach (var dir in CandidateDirs())
        {
            if (!Directory.Exists(dir)) continue;
            var files = Directory.GetFiles(dir, "*.md");
            if (files.Length == 0) continue;
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var all = new StringBuilder();
            int lines = 0;
            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    if (fi.Length == 0 || fi.Length > MaxFileBytes) { Log.Warn("credits", $"skipping {fi.Name} ({fi.Length} bytes)"); continue; }
                    string text = File.ReadAllText(f, Encoding.UTF8);
                    all.Append(text).Append('\n');
                    lines += ParseMarkdown(text, Path.GetFileNameWithoutExtension(f), into, MaxLines - lines);
                    if (lines >= MaxLines) { Log.Warn("credits", $"credit roll truncated at {MaxLines} lines"); break; }
                }
                catch (Exception e) { Log.Error("credits", $"cannot read {f}", e); }
            }
            into.RemoveAll(s => s.Lines.Count == 0 && !s.Heading);
            if (into.Count == 0) continue;
            Source = dir;
            return all.ToString().ToLowerInvariant();
        }
        return "";
    }

    /// <summary>A forgiving Markdown reader: headings become sections, table rows and list items become lines.</summary>
    private static int ParseMarkdown(string text, string fallbackTitle, List<CreditSection> into, int budget)
    {
        int added = 0;
        CreditSection cur = null;
        int lastTableLine = -1;            // index in cur.Lines of the previous table row (a header candidate)
        bool comment = false, blankPending = false;
        foreach (var rawLine in text.Replace("\r", "").Split('\n'))
        {
            if (added >= budget) break;
            string line = rawLine.Trim();
            if (comment) { if (line.Contains("-->")) comment = false; continue; }
            if (line.StartsWith("<!--", StringComparison.Ordinal)) { if (!line.Contains("-->")) comment = true; continue; }
            if (line.StartsWith("```", StringComparison.Ordinal)) continue;
            if (line.Length == 0) { blankPending = cur != null && cur.Lines.Count > 0; lastTableLine = -1; continue; }

            if (line[0] == '#')
            {
                int level = 0;
                while (level < line.Length && line[level] == '#') level++;
                string title = Inline(line.Substring(level).Trim().TrimEnd('#').Trim());
                if (title.Length == 0) continue;
                cur = new CreditSection { Title = title, Heading = false };
                // a file's own top heading is kept as a normal section title: the roll already has its chapter heading
                into.Add(cur);
                lastTableLine = -1; blankPending = false;
                continue;
            }

            if (cur == null) { cur = new CreditSection { Title = Pretty(fallbackTitle) }; into.Add(cur); }

            // list markers in front of a row or an item carry no meaning in the roll
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal))
                line = line.Substring(2).Trim();
            if (line.Length == 0) continue;

            if (line.Contains('|'))
            {
                if (IsRule(line))
                {
                    // the row above a |---|---| separator is the column header: not worth a line in the roll
                    if (lastTableLine >= 0 && lastTableLine == cur.Lines.Count - 1) { cur.Lines.RemoveAt(lastTableLine); added--; }
                    lastTableLine = -1;
                    continue;
                }
                var cells = new List<string>();
                foreach (var c in line.Trim('|').Split('|'))
                {
                    string cell = Inline(c.Trim());
                    if (cell.Length > 0 && cell != "-" && cell != "—") cells.Add(cell);
                }
                if (cells.Count == 0) continue;
                if (blankPending) { cur.Lines.Add(""); blankPending = false; }
                cur.Lines.Add(string.Join("　·　", cells));
                lastTableLine = cur.Lines.Count - 1;
                added++;
                continue;
            }

            lastTableLine = -1;
            if (IsRule(line)) continue;                         // horizontal rule
            string body = line;
            if (body[0] == '>') body = body.TrimStart('>', ' ');
            else
            {
                int dot = body.IndexOf(". ", StringComparison.Ordinal);
                if (dot > 0 && dot <= 3 && body.Take(dot).All(char.IsDigit)) body = body.Substring(dot + 2);
            }
            body = Inline(body);
            if (body.Length == 0) continue;
            if (blankPending) { cur.Lines.Add(""); blankPending = false; }
            cur.Lines.Add(body);
            added++;
        }
        return Math.Max(added, 0);
    }

    private static bool IsRule(string line)
    {
        bool mark = false;
        foreach (char ch in line)
        {
            if (ch == '-' || ch == '*' || ch == '_' || ch == '=') mark = true;
            else if (ch != '|' && ch != ':' && ch != ' ' && ch != '\t') return false;
        }
        return mark && line.Length >= 3;
    }

    /// <summary>Flattens inline Markdown: [text](url) → text（url）, images dropped, emphasis markers removed.</summary>
    private static string Inline(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOf('[') >= 0)
        {
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                bool image = s[i] == '!' && i + 1 < s.Length && s[i + 1] == '[';
                int open = image ? i + 1 : i;
                if (s[open] == '[')
                {
                    int close = s.IndexOf(']', open + 1);
                    if (close > 0 && close + 1 < s.Length && s[close + 1] == '(')
                    {
                        int end = s.IndexOf(')', close + 2);
                        if (end > 0)
                        {
                            string label = s.Substring(open + 1, close - open - 1).Trim();
                            string url = s.Substring(close + 2, end - close - 2).Trim();
                            if (!image) sb.Append(label.Length == 0 || label == url ? url : $"{label}（{url}）");
                            i = end + 1;
                            continue;
                        }
                    }
                }
                sb.Append(s[i]);
                i++;
            }
            s = sb.ToString();
        }
        return s.Replace("**", "").Replace("`", "").Replace("<br>", " ").Replace("<br/>", " ").Trim();
    }

    // ───────────────────────── output ─────────────────────────

    private static string Esc(string s) => s.Replace("[", "[lb]");

    /// <summary>The whole roll as centred BBCode for a RichTextLabel.</summary>
    public static string BbCode()
    {
        string gold = MenuKit.Gold.ToHtml(false), ember = Palette.Ember.ToHtml(false);
        string ivory = MenuKit.Ivory.ToHtml(false), faint = MenuKit.Faint.Lightened(0.15f).ToHtml(false);
        var sb = new StringBuilder(16384);
        sb.Append("[center]");
        bool first = true;
        foreach (var s in Sections())
        {
            if (s.Heading)
            {
                if (!first) sb.Append("\n\n\n");
                sb.Append("[font_size=46][color=#").Append(gold).Append(']').Append(Esc(s.Title ?? "")).Append("[/color][/font_size]\n");
                sb.Append("[font_size=14][color=#").Append(ember).Append("]◆[/color][/font_size]\n");
            }
            else if (!string.IsNullOrEmpty(s.Title))
            {
                sb.Append("\n[font_size=27][color=#").Append(ember).Append(']').Append(Esc(s.Title)).Append("[/color][/font_size]\n");
            }
            foreach (var l in s.Lines)
            {
                if (l.Length == 0) { sb.Append('\n'); continue; }
                if (l[0] == DimMark) sb.Append("[font_size=17][color=#").Append(faint).Append(']').Append(Esc(l.Substring(1))).Append("[/color][/font_size]\n");
                else sb.Append("[color=#").Append(ivory).Append(']').Append(Esc(l)).Append("[/color]\n");
            }
            first = false;
        }
        sb.Append("[/center]");
        return sb.ToString();
    }

    /// <summary>The roll as plain text (for logs or a text file).</summary>
    public static string PlainText()
    {
        var sb = new StringBuilder(8192);
        foreach (var s in Sections())
        {
            if (!string.IsNullOrEmpty(s.Title)) sb.Append(s.Heading ? "\n== " : "\n-- ").Append(s.Title).Append(s.Heading ? " ==\n" : " --\n");
            foreach (var l in s.Lines) sb.Append(l.Length > 0 && l[0] == DimMark ? l.Substring(1) : l).Append('\n');
        }
        return sb.ToString();
    }
}

/// <summary>What the character-creation screen says about a class beyond the shared <see cref="Names"/> tables.</summary>
public sealed class ClassBlurb
{
    public string Role, Weapon, ResourceNote, DifficultyNote, Flourish;
    public int Difficulty;               // 1 平易 … 3 艱深
    public string[] Suggestions;         // default names
}

public enum MenuTone { Normal, Primary, Danger }

/// <summary>A row of mutually exclusive toggle buttons (used for every on/off or few-way choice in the menus).</summary>
public sealed class MenuSeg
{
    public readonly HBoxContainer Root = new();
    public readonly List<Button> Buttons = new();
    public int Index { get; private set; } = -1;
    private readonly Action<int> _onPick;

    public MenuSeg(string[] options, Action<int> onPick, int size = 20, float minWidth = 116f)
    {
        _onPick = onPick;
        Root.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var b = new Button
            {
                Text = options[i], ToggleMode = true, FocusMode = Control.FocusModeEnum.All,
                CustomMinimumSize = new Vector2(minWidth, size + 22), MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };
            bool plates = UiTheme.Art && UiArt.Has("tab") && UiArt.Has("tab_hover") && UiArt.Has("tab_on");
            if (plates)
            {
                // forged option plates: dark iron at rest, lit under the pointer, the warm ember face with a bright hairline when chosen
                b.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("tab", 14f, 5f));
                b.AddThemeStyleboxOverride("hover", UiTheme.ArtBox("tab_hover", 14f, 5f));
                b.AddThemeStyleboxOverride("pressed", UiTheme.ArtBox("tab_on", 14f, 5f));
                b.AddThemeStyleboxOverride("hover_pressed", UiTheme.ArtBox("tab_on", 14f, 5f));
                b.AddThemeStyleboxOverride("disabled", UiTheme.ArtBox("seg_off", () => new UiNine("tab", null, 0f, 14f, 5f) { FrameMod = new Color(0.62f, 0.62f, 0.62f, 0.6f) }));
            }
            else
            {
                b.AddThemeStyleboxOverride("normal", MenuKit.Box(MenuKit.RowBg, MenuKit.Line.Darkened(0.25f), 1, 2, 14, 5));
                b.AddThemeStyleboxOverride("hover", MenuKit.Box(new Color(0.12f, 0.10f, 0.085f, 0.95f), Palette.Ember.Darkened(0.3f), 1, 2, 14, 5));
                b.AddThemeStyleboxOverride("pressed", MenuKit.Box(new Color(0.22f, 0.12f, 0.05f, 0.97f), Palette.Ember, 1, 2, 14, 5));
                b.AddThemeStyleboxOverride("hover_pressed", MenuKit.Box(new Color(0.26f, 0.14f, 0.06f, 0.97f), Palette.Ember.Lightened(0.15f), 1, 2, 14, 5));
                b.AddThemeStyleboxOverride("disabled", MenuKit.Box(new Color(0.05f, 0.048f, 0.046f, 0.7f), new Color(0.18f, 0.17f, 0.16f), 1, 2, 14, 5));
            }
            b.AddThemeStyleboxOverride("focus", MenuKit.FocusBox());
            var font = MenuKit.UiFont;
            if (font != null) b.AddThemeFontOverride("font", font);
            b.AddThemeFontSizeOverride("font_size", size);
            b.AddThemeColorOverride("font_color", MenuKit.Ash);
            b.AddThemeColorOverride("font_hover_color", MenuKit.Ivory);
            b.AddThemeColorOverride("font_focus_color", MenuKit.Ivory);
            b.AddThemeColorOverride("font_pressed_color", plates ? MenuKit.Gold.Lightened(0.3f) : MenuKit.Gold);
            b.AddThemeColorOverride("font_hover_pressed_color", plates ? MenuKit.Gold.Lightened(0.4f) : MenuKit.Gold);
            b.AddThemeColorOverride("font_disabled_color", MenuKit.Faint.Darkened(0.3f));
            b.Pressed += () =>
            {
                Select(idx);
                MenuKit.Click();
                if (_onPick != null) Log.Guard("menu", () => _onPick(idx));
            };
            Root.AddChild(b);
            Buttons.Add(b);
        }
    }

    /// <summary>Shows option <paramref name="i"/> as chosen without raising the callback.</summary>
    public void Select(int i)
    {
        Index = i;
        for (int j = 0; j < Buttons.Count; j++) Buttons[j].SetPressedNoSignal(j == i);
    }

    public void SetEnabled(bool on) { foreach (var b in Buttons) b.Disabled = !on; }
    public Button Current => Index >= 0 && Index < Buttons.Count ? Buttons[Index] : Buttons.Count > 0 ? Buttons[0] : null;
}

/// <summary>A blocking question laid over a menu. The host forwards Esc to <see cref="Cancel"/>.</summary>
public sealed class MenuModal
{
    public Control Root;
    public Button Yes, No;
    public bool Alive = true;
    public Action Closed;
    internal Action OnNo;

    public void Cancel()
    {
        if (!Alive) return;
        var no = OnNo;
        Close();
        if (no != null) Log.Guard("menu", no);
    }

    public void Close()
    {
        if (!Alive) return;
        Alive = false;
        if (GodotObject.IsInstanceValid(Root)) Root.QueueFree();
        var c = Closed;
        if (c != null) Log.Guard("menu", c);
    }
}

/// <summary>
/// Shared look and small widgets of the menu screens (title, creation, load, pause, settings, death): fonts with
/// fallbacks, style boxes, buttons, sliders, a modal question, and the text formatting used on save-slot cards.
/// Uses <see cref="UiTheme"/> where it is available and stands on its own when it is not.
/// </summary>
public static class MenuKit
{
    public static readonly Color Ivory = new(0.94f, 0.89f, 0.79f);
    public static readonly Color Ash = new(0.68f, 0.65f, 0.60f);
    public static readonly Color Faint = new(0.45f, 0.43f, 0.40f);
    public static readonly Color EmberDeep = new(0.80f, 0.29f, 0.07f);
    public static readonly Color Blood = new(0.84f, 0.18f, 0.13f);
    public static readonly Color Ink = new(0.026f, 0.024f, 0.023f);
    public static readonly Color PanelBg = new(0.043f, 0.040f, 0.038f, 0.93f);
    public static readonly Color GlassBg = new(0.030f, 0.028f, 0.027f, 0.80f);
    public static readonly Color RowBg = new(0.075f, 0.068f, 0.062f, 0.90f);
    public static readonly Color Line = new(0.34f, 0.27f, 0.19f);

    private static bool _probed, _frameWarned, _sndWarned;
    private static Font _title, _body, _ui, _black, _cinzel;
    private static Theme _theme;
    private static Color _gold = Palette.Gold;
    private static Texture2D _softDot, _grabber, _grabberHot, _grabberOff;
    private static ulong _quietUntil;

    private static void Probe()
    {
        if (_probed) return;
        _probed = true;
        try
        {
            _title = UiTheme.Title; _body = UiTheme.Body; _ui = UiTheme.Ui; _theme = UiTheme.Theme;
            var g = UiTheme.Gold;
            if (g.A > 0.01f && g.R + g.G + g.B > 0.2f) _gold = g;
        }
        catch (Exception e) { Log.Warn("menu", "UiTheme is not available, the menus use their own fonts: " + e.Message); }
        _title ??= LoadFont("NotoSerifTC-Bold.otf");
        _body ??= LoadFont("NotoSerifTC-Regular.otf");
        _ui ??= LoadFont("NotoSansTC-Regular.otf");
        Font any = _title ?? _body ?? _ui;
        if (any == null) { try { any = ThemeDB.FallbackFont; } catch (Exception) { } }
        _title ??= any; _body ??= any; _ui ??= any;
        _black = LoadFont("NotoSerifTC-Black.otf") ?? _title;
        _cinzel = LoadFont("Cinzel-Variable.ttf");
    }

    private static Font LoadFont(string file)
    {
        try
        {
            string p = "res://assets/fonts/" + file;
            if (ResourceLoader.Exists(p)) return GD.Load<Font>(p);
            Log.Warn("menu", $"font {file} is missing");
        }
        catch (Exception e) { Log.Warn("menu", $"font {file} failed to load: {e.Message}"); }
        return null;
    }

    public static Font TitleFont { get { Probe(); return _title; } }
    public static Font BodyFont { get { Probe(); return _body; } }
    public static Font UiFont { get { Probe(); return _ui; } }
    /// <summary>Noto Serif TC Black, for the game's name and the heaviest headings.</summary>
    public static Font BlackFont { get { Probe(); return _black; } }
    public static Theme Theme { get { Probe(); return _theme; } }
    public static Color Gold { get { Probe(); return _gold; } }

    /// <summary>A font with extra space between glyphs (and optionally a tighter line box).</summary>
    public static Font Spaced(Font f, int glyphSpacing, int trimTop = 0, int trimBottom = 0)
    {
        if (f == null) return null;
        return new FontVariation { BaseFont = f, SpacingGlyph = glyphSpacing, SpacingTop = -trimTop, SpacingBottom = -trimBottom };
    }

    /// <summary>Cinzel (Latin display) at the given weight; falls back to the title font when the file is missing.</summary>
    public static Font Latin(int weight = 600, int glyphSpacing = 0)
    {
        Probe();
        if (_cinzel == null) return glyphSpacing == 0 ? _title : Spaced(_title, glyphSpacing);
        var fv = new FontVariation { BaseFont = _cinzel, SpacingGlyph = glyphSpacing };
        try
        {
            var ts = TextServerManager.GetPrimaryInterface();
            if (ts != null) fv.VariationOpentype = new Godot.Collections.Dictionary { { ts.NameToTag("wght"), weight } };
        }
        catch (Exception) { }
        return fv;
    }

    // ───────────────────────── text ─────────────────────────

    public static Label Text(string text, int size, Color color, Font font = null)
    {
        Probe();
        var l = new Label { Text = text ?? "", MouseFilter = Control.MouseFilterEnum.Ignore };
        var f = font ?? _body;
        if (f != null) l.AddThemeFontOverride("font", f);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    /// <summary>A label that wraps inside <paramref name="width"/> pixels (0 = wrap to whatever the parent gives it).</summary>
    public static Label Wrap(string text, int size, Color color, float width = 0f, Font font = null)
    {
        var l = Text(text, size, color, font);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (width > 0f) l.CustomMinimumSize = new Vector2(width, 0);
        else l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return l;
    }

    public static void Shadow(Label l, Color c, int outline = 6, int offsetY = 2)
    {
        l.AddThemeColorOverride("font_shadow_color", c);
        l.AddThemeConstantOverride("shadow_offset_x", 0);
        l.AddThemeConstantOverride("shadow_offset_y", offsetY);
        l.AddThemeConstantOverride("shadow_outline_size", outline);
    }

    /// <summary>Breaks a long line every <paramref name="perLine"/> characters (tooltips do not wrap by themselves).</summary>
    public static string HardWrap(string s, int perLine)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= perLine) return s ?? "";
        var sb = new StringBuilder(s.Length + s.Length / perLine + 2);
        int n = 0;
        foreach (char ch in s)
        {
            if (ch == '\n') { n = 0; sb.Append(ch); continue; }
            if (n >= perLine && !char.IsPunctuation(ch)) { sb.Append('\n'); n = 0; }
            sb.Append(ch);
            n++;
        }
        return sb.ToString();
    }

    // ───────────────────────── boxes and frames ─────────────────────────

    /// <summary>
    /// A bordered box. With the raster interface art it is a bevelled line frame tinted with the border colour over
    /// the flat background (see UiTheme.Box); without it, or for round boxes, the flat box of old.
    /// </summary>
    public static StyleBox Box(Color bg, Color border, int borderPx = 1, int radius = 2, float padH = 0f, float padV = 0f)
    {
        if (UiTheme.Art && radius <= 5 && borderPx > 0 && border.A > 0.02f && UiArt.Has("boxline"))
        {
            var n = new UiNine("boxline") { Back = bg, BackInset = 1f, FrameMod = border };
            n.ContentMarginLeft = padH; n.ContentMarginRight = padH; n.ContentMarginTop = padV; n.ContentMarginBottom = padV;
            return n;
        }
        return FlatBox(bg, border, borderPx, radius, padH, padV);
    }

    public static StyleBoxFlat FlatBox(Color bg, Color border, int borderPx = 1, int radius = 2, float padH = 0f, float padV = 0f)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = radius > 0 };
        s.SetBorderWidthAll(borderPx);
        s.SetCornerRadiusAll(radius);
        s.ContentMarginLeft = padH; s.ContentMarginRight = padH; s.ContentMarginTop = padV; s.ContentMarginBottom = padV;
        return s;
    }

    public static StyleBox FocusBox()
    {
        if (UiTheme.Art) return UiTheme.ArtBox("menu_focus", () => new UiNine("btn_focus") { Grow = 2f });
        var s = new StyleBoxFlat { DrawCenter = false, BorderColor = Gold, AntiAliasing = true };
        s.SetBorderWidthAll(2);
        s.SetCornerRadiusAll(3);
        s.SetExpandMarginAll(2f);
        return s;
    }

    /// <summary>The game's standard panel (UiTheme.Frame) with a fallback of our own when the theme is not there.</summary>
    public static PanelContainer Frame(bool heavy = false)
    {
        Probe();
        PanelContainer p = null;
        try { p = UiTheme.Frame(heavy); }
        catch (Exception e)
        {
            if (!_frameWarned) { _frameWarned = true; Log.Warn("menu", "UiTheme.Frame failed, using the menu's own frame: " + e.Message); }
        }
        if (p == null)
        {
            p = new PanelContainer();
            var sb = FlatBox(PanelBg, heavy ? Palette.Ember.Darkened(0.45f) : Line, heavy ? 2 : 1, 3);
            sb.ShadowColor = new Color(0, 0, 0, 0.55f);
            sb.ShadowSize = heavy ? 18 : 8;
            p.AddThemeStyleboxOverride("panel", sb);
        }
        return p;
    }

    /// <summary>A translucent dark pane that lets the 3D backdrop glow through (title-screen screens).</summary>
    public static PanelContainer Glass(float alpha = 0.80f, Color? border = null)
    {
        var p = new PanelContainer();
        if (UiTheme.Art)
        {
            // the sub-panel moulding round a translucent soot field: the backdrop still glows through
            float a = Mathf.Clamp(alpha + 0.06f, 0f, 1f);
            p.AddThemeStyleboxOverride("panel", UiTheme.ArtBox("glass|" + a.ToString("0.00"), () => new UiNine("panel", "soot", 3f) { FieldMod = new Color(1f, 1f, 1f, a) }));
            return p;
        }
        var sb = FlatBox(new Color(GlassBg.R, GlassBg.G, GlassBg.B, alpha), border ?? new Color(Line.R, Line.G, Line.B, 0.75f), 1, 3);
        sb.ShadowColor = new Color(0, 0, 0, 0.45f);
        sb.ShadowSize = 14;
        p.AddThemeStyleboxOverride("panel", sb);
        return p;
    }

    public static MarginContainer Pad(Control child, int left, int top, int right, int bottom)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_bottom", bottom);
        if (child != null) m.AddChild(child);
        return m;
    }

    public static MarginContainer Pad(Control child, int all) => Pad(child, all, all, all, all);

    public static Control Spacer(float w = 0f, float h = 0f, bool expand = false)
    {
        var c = new Control { CustomMinimumSize = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (expand) { c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; c.SizeFlagsVertical = Control.SizeFlags.ExpandFill; }
        return c;
    }

    /// <summary>
    /// A rule between sections. With the raster art it is the forged gold line with tapering ends (piece "rule");
    /// <paramref name="c"/> gives its strength (alpha) and, for warm colours such as blood red, its tint — cool
    /// colours would only muddy the gold, so they leave it as it is. Without the art: the flat hairline of old.
    /// </summary>
    public static Control Rule(Color c, float height = 1f)
    {
        if (!UiTheme.Art || !UiArt.Has("rule")) return FlatRule(c, height);
        Color mod = Colors.White;
        float max = MathF.Max(c.R, MathF.Max(c.G, c.B));
        if (max > 0.01f && c.R >= c.B)
        {
            var hue = new Color(c.R / max, c.G / max, c.B / max);
            mod = Colors.White.Lerp(hue, 1f - MathF.Min(hue.G, hue.B) > 0.6f ? 0.9f : 0.3f);
        }
        mod.A = Mathf.Clamp(c.A + 0.15f, 0.3f, 1f);
        var s = new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
        s.AddThemeConstantOverride("separation", 4);
        s.AddThemeStyleboxOverride("separator", UiTheme.ArtBox("menu_rule|" + mod.ToHtml(true), () => new UiNine("rule") { Line = true, FrameMod = mod }));
        return s;
    }

    /// <summary>The flat hairline (what <see cref="Rule"/> falls back to without the art).</summary>
    public static ColorRect FlatRule(Color c, float height = 1f)
    {
        return new ColorRect { Color = c, CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
    }

    /// <summary>
    /// A fixed ornament of the art set ("title_rule", "rule_gem", "emblem" …) centred in a control of the given
    /// height; it is drawn smaller when the control is narrower than the piece. Null without the art or the piece:
    /// the caller then keeps its old drawing.
    /// </summary>
    public static Control Ornament(string piece, float height, Color? mod = null, float scale = 1f)
    {
        if (!UiTheme.Art) return null;
        var p = UiArt.P(piece);
        if (p == null) return null;
        var c = new Control { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        Color m = mod ?? Colors.White;
        bool anchored = p.Anchor != Vector2.Zero;
        c.Draw += () =>
        {
            float w = p.BoxSize.X * scale;
            float s = w > c.Size.X && w > 0f ? scale * c.Size.X / w : scale;
            var at = new Vector2(MathF.Round(c.Size.X * 0.5f), MathF.Round(c.Size.Y * 0.5f));
            if (anchored) UiArt.SpriteAt(c, p, at, m, s);
            else UiArt.SpriteCentered(c, p, at, m, s);
        };
        return c;
    }

    /// <summary>
    /// A small enamel tag (專家, 已殞落, 毀損): the forged plaque with its hairline and a veil in the tag's colour.
    /// <paramref name="size"/> is a finished font size (already through UiTheme.Fs).
    /// </summary>
    public static PanelContainer Badge(string text, Color c, int size, Color? textColor = null)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        if (UiTheme.Art && UiArt.Has("plaque"))
        {
            float max = MathF.Max(0.01f, MathF.Max(c.R, MathF.Max(c.G, c.B)));
            Color tint = Colors.White.Lerp(new Color(c.R / max, c.G / max, c.B / max), 0.85f);
            p.AddThemeStyleboxOverride("panel", UiTheme.ArtBox("menu_badge|" + c.ToHtml(false), () =>
                new UiNine("plaque", null, 0f, 12f, 1f) { FrameMod = tint, Back = new Color(c.R * 0.5f, c.G * 0.5f, c.B * 0.5f, 0.3f), BackInset = 4f, BackOver = true }));
        }
        else p.AddThemeStyleboxOverride("panel", Box(new Color(c.R * 0.22f, c.G * 0.22f, c.B * 0.22f, 0.95f), c, 1, 2, 9, 1));
        p.AddChild(Text(text, size, textColor ?? c.Lightened(0.3f), UiFont));
        return p;
    }

    /// <summary>
    /// A key cap: the forged cap piece (a raised flat box without the art) around the name of a key.
    /// <paramref name="size"/> is a finished font size (already through UiTheme.Fs).
    /// </summary>
    public static PanelContainer KeyCap(string text, int size)
    {
        var p = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (UiTheme.Art && UiArt.Has("keycap")) p.AddThemeStyleboxOverride("panel", UiTheme.ArtBox("keycap", 11f, 3f));
        else
        {
            var sb = FlatBox(new Color(0.115f, 0.105f, 0.095f, 0.98f), Line.Lightened(0.1f), 1, 4, 10, 2);
            sb.BorderWidthBottom = 3;
            p.AddThemeStyleboxOverride("panel", sb);
        }
        var l = Text(text, size, Ivory, UiFont);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        p.AddChild(l);
        return p;
    }

    /// <summary>
    /// A line of key hints for the foot of a screen: a part written "[Enter]" becomes a key cap, an empty part is the
    /// gap between two hints, anything else is a caption in <paramref name="caption"/>.
    /// </summary>
    public static HBoxContainer KeyHints(int size, Color caption, params string[] parts)
    {
        var row = HBox(6);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) row.AddChild(Spacer(16, 0));
            else if (part.Length > 2 && part[0] == '[' && part[^1] == ']') row.AddChild(KeyCap(part[1..^1], size));
            else
            {
                var l = Text(part, size, caption, UiFont);
                l.VerticalAlignment = VerticalAlignment.Center;
                row.AddChild(l);
            }
        }
        return row;
    }

    /// <summary>
    /// Dresses a toggle button as a forged row plate: dark iron at rest, lit under the pointer, the warm ember face
    /// with a bright hairline when chosen (pieces row / row_hover / row_on; the tab plates stand in until they are
    /// installed). <paramref name="tint"/> lays a fine line frame in that colour over the hovered and the chosen
    /// plate — a class colour, say. Returns false without the art: the caller then sets its flat boxes.
    /// </summary>
    public static bool Plate(Button b, float padL, float padT, float padR, float padB, Color? tint = null)
    {
        if (!UiTheme.Art) return false;
        string fam = UiArt.Has("row") && UiArt.Has("row_hover") && UiArt.Has("row_on") ? "row"
            : UiArt.Has("tab") && UiArt.Has("tab_hover") && UiArt.Has("tab_on") ? "tab" : null;
        if (fam == null) return false;
        string tail = $"|{(tint.HasValue ? tint.Value.ToHtml(false) : "-")}|{padL}|{padT}|{padR}|{padB}";
        UiNine Make(int state)
        {
            string piece = state >= 2 ? fam + "_on" : state == 1 ? fam + "_hover" : fam;
            return UiTheme.ArtBox("menu_plate|" + piece + "|" + state + tail, () =>
            {
                var n = new UiNine(piece).Margins(padL, padT, padR, padB);
                if (tint.HasValue && state > 0 && UiArt.Has("boxline"))
                {
                    n.Over1 = UiArt.P("boxline");
                    n.Over1Mod = new Color(tint.Value, state == 1 ? 0.55f : state == 2 ? 0.9f : 1f);
                }
                return n;
            });
        }
        b.AddThemeStyleboxOverride("normal", Make(0));
        b.AddThemeStyleboxOverride("hover", Make(1));
        b.AddThemeStyleboxOverride("pressed", Make(2));
        b.AddThemeStyleboxOverride("hover_pressed", Make(3));
        b.AddThemeStyleboxOverride("disabled", UiTheme.ArtBox("menu_plate_off|" + fam + tail, () => new UiNine(fam) { FrameMod = new Color(0.62f, 0.62f, 0.62f, 0.6f) }.Margins(padL, padT, padR, padB)));
        return true;
    }

    public static VBoxContainer VBox(int separation)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", separation);
        return v;
    }

    public static HBoxContainer HBox(int separation)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", separation);
        return h;
    }

    public static void Place(Control c, float anchorL, float anchorT, float anchorR, float anchorB, float l, float t, float r, float b)
    {
        c.AnchorLeft = anchorL; c.AnchorTop = anchorT; c.AnchorRight = anchorR; c.AnchorBottom = anchorB;
        c.OffsetLeft = l; c.OffsetTop = t; c.OffsetRight = r; c.OffsetBottom = b;
    }

    // ───────────────────────── buttons ─────────────────────────

    /// <summary>A boxed command button. <paramref name="onPress"/> runs guarded, after the click sound.</summary>
    public static Button Btn(string text, Action onPress, MenuTone tone = MenuTone.Normal, int size = 22, float minWidth = 180f)
    {
        Probe();
        var b = new Button
        {
            Text = text, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(minWidth, size + 26),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        Color edge = tone == MenuTone.Primary ? Palette.Ember.Darkened(0.2f) : tone == MenuTone.Danger ? Blood.Darkened(0.25f) : Line;
        Color hot = tone == MenuTone.Danger ? Blood : Palette.Ember;
        Color txt = tone == MenuTone.Primary ? Gold : tone == MenuTone.Danger ? new Color(0.96f, 0.50f, 0.42f) : Ivory;
        Color pressBg = tone == MenuTone.Danger ? new Color(0.24f, 0.06f, 0.04f, 0.97f) : new Color(0.22f, 0.12f, 0.05f, 0.97f);
        if (UiTheme.Art)
        {
            // forged plates; the tone warms (primary) or reddens (danger) the metal a little, the lettering carries the rest
            Color mod = tone == MenuTone.Primary ? new Color(1f, 0.94f, 0.84f) : tone == MenuTone.Danger ? new Color(1f, 0.76f, 0.72f) : Colors.White;
            string k = "|" + tone;
            b.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("mbtn" + k, () => new UiNine("btn", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("hover", UiTheme.ArtBox("mbtn_hover" + k, () => new UiNine("btn_hover", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("pressed", UiTheme.ArtBox("mbtn_pressed" + k, () => new UiNine("btn_pressed", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("hover_pressed", UiTheme.ArtBox("mbtn_pressed" + k, () => new UiNine("btn_pressed", null, 0f, 18f, 6f) { FrameMod = mod }));
            b.AddThemeStyleboxOverride("disabled", UiTheme.ArtBox("btn_disabled", 18f, 6f));
            b.AddThemeStyleboxOverride("focus", UiTheme.ArtBox("btn_focus", 0f, 0f));
        }
        else
        {
            b.AddThemeStyleboxOverride("normal", Box(RowBg, edge, 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("hover", Box(new Color(0.125f, 0.105f, 0.088f, 0.96f), hot, 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("pressed", Box(pressBg, hot.Lightened(0.15f), 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("disabled", Box(new Color(0.05f, 0.048f, 0.046f, 0.72f), new Color(0.19f, 0.18f, 0.17f), 1, 2, 18, 6));
            b.AddThemeStyleboxOverride("focus", FocusBox());
        }
        if (_title != null) b.AddThemeFontOverride("font", _title);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", txt);
        b.AddThemeColorOverride("font_hover_color", txt.Lightened(0.2f));
        b.AddThemeColorOverride("font_focus_color", txt.Lightened(0.2f));
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_disabled_color", Faint.Darkened(0.25f));
        b.Pressed += () =>
        {
            Click();
            if (onPress != null) Log.Guard("menu", onPress);
        };
        b.MouseEntered += () => { if (!b.Disabled) Hover(); };
        return b;
    }

    /// <summary>True when the title-menu entries are forged plates (menu_plate / menu_plate_hover) instead of bare text.</summary>
    public static bool EntryPlates => UiTheme.Art && UiArt.Has("menu_plate") && UiArt.Has("menu_plate_hover");

    /// <summary>
    /// An entry of the title menu. With the raster art: a forged plate with pointed ends (the pieces are strips of a
    /// fixed height, 56 design px, so the button should be about that tall), quiet at rest and lit — ember stones,
    /// bright hairline — under the pointer or the focus. Without it: bare text, an ember diamond marks the focused one.
    /// </summary>
    public static Button Entry(string text, int size, Action onPress)
    {
        Probe();
        var b = new Button
        {
            Text = text, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0, size + 22), MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        bool plates = EntryPlates;
        var empty = new StyleBoxEmpty { ContentMarginLeft = plates ? 58 : 42, ContentMarginRight = plates ? 58 : 16, ContentMarginTop = 4, ContentMarginBottom = 4 };
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) b.AddThemeStyleboxOverride(st, empty);
        if (plates)
        {
            // the text keeps clear of the pointed caps (38 px) and their stones on either side
            var idle = UiTheme.ArtBox("entry_idle", () => new UiNine("menu_plate") { FrameMod = new Color(1f, 1f, 1f, 0.62f) }.Margins(58f, 4f, 58f, 4f));
            var hotBox = UiTheme.ArtBox("entry_hot", () => new UiNine("menu_plate_hover").Margins(58f, 4f, 58f, 4f));
            b.AddThemeStyleboxOverride("normal", idle);
            b.AddThemeStyleboxOverride("disabled", UiTheme.ArtBox("entry_off", () => new UiNine("menu_plate") { FrameMod = new Color(0.7f, 0.7f, 0.7f, 0.4f) }.Margins(58f, 4f, 58f, 4f)));
            b.AddThemeStyleboxOverride("hover", hotBox);
            b.AddThemeStyleboxOverride("pressed", hotBox);
            b.AddThemeStyleboxOverride("hover_pressed", hotBox);
            b.AddThemeStyleboxOverride("focus", hotBox);
        }
        if (_title != null) b.AddThemeFontOverride("font", _title);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Ash);
        b.AddThemeColorOverride("font_hover_color", Ivory.Lightened(0.25f));
        b.AddThemeColorOverride("font_focus_color", Ivory.Lightened(0.25f));
        b.AddThemeColorOverride("font_pressed_color", Palette.Ember);
        b.AddThemeColorOverride("font_hover_pressed_color", Palette.Ember);
        b.AddThemeColorOverride("font_disabled_color", new Color(0.30f, 0.29f, 0.28f));
        b.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        b.AddThemeConstantOverride("shadow_offset_x", 0);
        b.AddThemeConstantOverride("shadow_offset_y", 2);
        b.AddThemeConstantOverride("shadow_outline_size", 6);
        var pts = new Vector2[4];
        b.Draw += () =>
        {
            if (plates || b.Disabled || !(b.HasFocus() || b.IsHovered())) return;
            float cy = b.Size.Y * 0.5f + 1f, cx = 18f, r = size * 0.21f;
            pts[0] = new Vector2(cx, cy - r); pts[1] = new Vector2(cx + r, cy); pts[2] = new Vector2(cx, cy + r); pts[3] = new Vector2(cx - r, cy);
            b.DrawColoredPolygon(pts, Palette.Ember);
        };
        b.MouseEntered += () => { if (!b.Disabled && !b.HasFocus()) b.GrabFocus(); };
        b.FocusEntered += Hover;
        b.Pressed += () =>
        {
            Click();
            if (onPress != null) Log.Guard("menu", onPress);
        };
        return b;
    }

    /// <summary>A tick box drawn by hand so it scales with the font instead of relying on theme icons.</summary>
    public static Button Check(string text, bool initial, Action<bool> onToggle, int size = 22, Color? accent = null)
    {
        Probe();
        Color acc = accent ?? Palette.Ember;
        var b = new Button
        {
            Text = text, ToggleMode = true, ButtonPressed = initial, Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0, size + 18),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        var empty = new StyleBoxEmpty { ContentMarginLeft = size + 22, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4 };
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }) b.AddThemeStyleboxOverride(st, empty);
        b.AddThemeStyleboxOverride("focus", FocusBox());
        if (_title != null) b.AddThemeFontOverride("font", _title);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Ivory);
        b.AddThemeColorOverride("font_hover_color", Ivory.Lightened(0.2f));
        b.AddThemeColorOverride("font_focus_color", Ivory.Lightened(0.2f));
        b.AddThemeColorOverride("font_pressed_color", acc.Lightened(0.25f));
        b.AddThemeColorOverride("font_hover_pressed_color", acc.Lightened(0.35f));
        UiArt.Piece boxOff = UiTheme.Art ? UiArt.P("check_off") : null, boxOn = UiTheme.Art ? UiArt.P("check_on") : null;
        b.Draw += () =>
        {
            float s = size + 2f, y = (b.Size.Y - s) * 0.5f;
            var r = new Rect2(6f, y, s, s);
            if (boxOff != null && boxOn != null)
            {
                UiArt.Sprite(b, b.ButtonPressed ? boxOn : boxOff, r.Grow(2f), b.IsHovered() ? Colors.White : new Color(0.92f, 0.9f, 0.88f));
                return;
            }
            b.DrawRect(r, new Color(0.05f, 0.045f, 0.04f, 0.92f), true);
            b.DrawRect(r, b.ButtonPressed ? acc : (b.IsHovered() ? Ash : Line), false, 2f);
            if (b.ButtonPressed) b.DrawRect(new Rect2(6f + 5f, y + 5f, s - 10f, s - 10f), acc, true);
        };
        b.Toggled += on =>
        {
            Click();
            if (onToggle != null) Log.Guard("menu", () => onToggle(on));
        };
        return b;
    }

    public static HSlider Slider(double min, double max, double step, float minWidth = 320f)
    {
        var s = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step, CustomMinimumSize = new Vector2(minWidth, 30),
            FocusMode = Control.FocusModeEnum.All, Scrollable = false, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        if (UiTheme.Art && UiArt.Has("slider_track"))
        {
            // forged groove, lit fill and a rhombic knob: the theme's own slider art
            s.AddThemeStyleboxOverride("slider", UiTheme.ArtBox("slider_track", 0f, 5f));
            s.AddThemeStyleboxOverride("grabber_area", UiTheme.ArtBox("slider_fill", 0f, 5f));
            s.AddThemeStyleboxOverride("grabber_area_highlight", UiTheme.ArtBox("slider_fill", 0f, 5f));
            var g0 = UiArt.Icon("slider_grab"); var g1 = UiArt.Icon("slider_grab_hover");
            if (g0 != null && g1 != null)
            {
                s.AddThemeIconOverride("grabber", g0);
                s.AddThemeIconOverride("grabber_highlight", g1);
                s.AddThemeIconOverride("grabber_disabled", g0);
                return s;
            }
        }
        var track = FlatBox(new Color(0.09f, 0.082f, 0.076f), Line.Darkened(0.2f), 1, 3);
        track.ContentMarginTop = 4; track.ContentMarginBottom = 4;
        s.AddThemeStyleboxOverride("slider", track);
        s.AddThemeStyleboxOverride("grabber_area", Box(Palette.Ember.Darkened(0.35f), Palette.Ember.Darkened(0.35f), 0, 3));
        s.AddThemeStyleboxOverride("grabber_area_highlight", Box(Palette.Ember.Darkened(0.1f), Palette.Ember.Darkened(0.1f), 0, 3));
        _grabber ??= Knob(Ivory.Darkened(0.12f), EmberDeep);
        _grabberHot ??= Knob(Colors.White, Palette.Ember);
        _grabberOff ??= Knob(Faint.Darkened(0.2f), Faint.Darkened(0.5f));
        s.AddThemeIconOverride("grabber", _grabber);
        s.AddThemeIconOverride("grabber_highlight", _grabberHot);
        s.AddThemeIconOverride("grabber_disabled", _grabberOff);
        return s;
    }

    private static Texture2D Knob(Color fill, Color ring)
    {
        const int n = 22;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        float c = (n - 1) * 0.5f, rOut = n * 0.5f - 0.5f, rIn = rOut - 3f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp(rOut - d + 0.5f, 0f, 1f);
                float inner = Mathf.Clamp(rIn - d + 0.5f, 0f, 1f);
                var col = ring.Lerp(fill, inner);
                img.SetPixel(x, y, new Color(col.R, col.G, col.B, a));
            }
        return ImageTexture.CreateFromImage(img);
    }

    // ───────────────────────── textures ─────────────────────────

    /// <summary>A soft round sprite (white, alpha falls off to the edge) for particles and glows.</summary>
    public static Texture2D SoftDot
    {
        get
        {
            if (_softDot == null)
            {
                var g = new Gradient
                {
                    Offsets = new[] { 0f, 0.35f, 1f },
                    Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) },
                };
                _softDot = new GradientTexture2D
                {
                    Gradient = g, Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
                    FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
                };
            }
            return _softDot;
        }
    }

    /// <summary>A linear gradient texture; <paramref name="vertical"/> runs top → bottom, otherwise left → right.</summary>
    public static Texture2D Fade(bool vertical, params (float at, Color c)[] stops)
    {
        var g = new Gradient { Offsets = stops.Select(s => s.at).ToArray(), Colors = stops.Select(s => s.c).ToArray() };
        return new GradientTexture2D
        {
            Gradient = g, Width = vertical ? 4 : 256, Height = vertical ? 256 : 4, Fill = GradientTexture2D.FillEnum.Linear,
            FillFrom = Vector2.Zero, FillTo = vertical ? new Vector2(0, 1) : new Vector2(1, 0),
        };
    }

    public static TextureRect Stretched(Texture2D tex)
    {
        var r = new TextureRect
        {
            Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        r.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return r;
    }

    // ───────────────────────── sound ─────────────────────────

    /// <summary>Suppresses hover blips for a moment (screen changes move the focus by themselves).</summary>
    public static void Quiet(int ms = 220) => _quietUntil = Time.GetTicksMsec() + (ulong)ms;
    public static void Click() => Play("ui_click", 0.9f);
    public static void Hover() { if (Time.GetTicksMsec() >= _quietUntil) Play("ui_hover", 0.45f); }
    public static void OpenSound() => Play("ui_open", 0.8f);
    public static void CloseSound() => Play("ui_close", 0.8f);
    public static void ErrorSound() => Play("ui_error", 0.9f);
    public static void Whoosh() => Play("menu_whoosh", 0.6f);

    private static void Play(string name, float volume)
    {
        try { Snd.Ui(name, volume); }
        catch (Exception e)
        {
            if (!_sndWarned) { _sndWarned = true; Log.Warn("menu", "interface sounds are unavailable: " + e.Message); }
        }
    }

    // ───────────────────────── modal question ─────────────────────────

    /// <summary>
    /// Lays a question over <paramref name="host"/>. With <paramref name="no"/> == null it is a notice with one button.
    /// Keyboard focus is trapped between the two buttons; the safe answer is focused for dangerous questions.
    /// </summary>
    public static MenuModal Modal(Control host, string title, string body, string yes, string no, Action onYes, Action onNo = null, MenuTone tone = MenuTone.Primary)
    {
        var m = new MenuModal { OnNo = onNo };
        var root = new Control { Name = "Modal", MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.66f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var frame = Frame(true);
        center.AddChild(frame);
        var col = VBox(18);
        frame.AddChild(Pad(col, 34, 28, 34, 26));
        // design sizes through the text scale (the question used to stay at the standard size whatever the setting);
        // the text column and the buttons grow with it
        var head = Text(title, UiTheme.Fs(30), tone == MenuTone.Danger ? new Color(0.96f, 0.46f, 0.38f) : Gold, TitleFont);
        head.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(head);
        col.AddChild(Rule(new Color(Line.R, Line.G, Line.B, 0.8f)));
        var text = Wrap(body, UiTheme.Fs(22), Ivory, 580f);
        UiTheme.MinSize(text, 580f, 0f);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(text);
        col.AddChild(Spacer(0, 4));
        var row = HBox(22);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        col.AddChild(row);

        m.Yes = UiTheme.MinSize(Btn(yes, () => { m.Close(); onYes?.Invoke(); }, tone, UiTheme.Fs(22), 190f), 190f, 48f);
        m.Yes.Name = "Yes";
        row.AddChild(m.Yes);
        if (no != null)
        {
            m.No = UiTheme.MinSize(Btn(no, m.Cancel, MenuTone.Normal, UiTheme.Fs(22), 190f), 190f, 48f);
            m.No.Name = "No";
            row.AddChild(m.No);
            NodePath toNo = new("../No"), toYes = new("../Yes");
            m.Yes.FocusNeighborLeft = toNo; m.Yes.FocusNeighborRight = toNo; m.Yes.FocusNeighborTop = toNo; m.Yes.FocusNeighborBottom = toNo;
            m.Yes.FocusNext = toNo; m.Yes.FocusPrevious = toNo;
            m.No.FocusNeighborLeft = toYes; m.No.FocusNeighborRight = toYes; m.No.FocusNeighborTop = toYes; m.No.FocusNeighborBottom = toYes;
            m.No.FocusNext = toYes; m.No.FocusPrevious = toYes;
        }
        else
        {
            NodePath self = new(".");
            m.Yes.FocusNeighborLeft = self; m.Yes.FocusNeighborRight = self; m.Yes.FocusNeighborTop = self; m.Yes.FocusNeighborBottom = self;
            m.Yes.FocusNext = self; m.Yes.FocusPrevious = self;
        }

        m.Root = root;
        host.AddChild(root);
        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.14f);
        Quiet();
        var first = tone == MenuTone.Danger && m.No != null ? m.No : m.Yes;
        Callable.From(() => { if (m.Alive && GodotObject.IsInstanceValid(first)) first.GrabFocus(); }).CallDeferred();
        OpenSound();
        return m;
    }

    // ───────────────────────── formatting ─────────────────────────

    public static bool ValidClass(ClassId c) => (int)c >= 0 && (int)c < Names.ClassCount;
    public static string ClassName(ClassId c) => ValidClass(c) ? Names.Class(c) : "無名者";
    public static Color ClassColor(ClassId c) => ValidClass(c) ? Names.ClassColor(c) : Ash;

    public static string PlayTime(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        long minutes = (long)(seconds / 60.0), hours = minutes / 60;
        minutes %= 60;
        return hours > 0 ? $"{hours} 小時 {minutes} 分" : minutes > 0 ? $"{minutes} 分" : "不足 1 分";
    }

    public static string TierName(int tier)
    {
        var names = ZoneDb.TierNames;
        return names[Math.Clamp(tier, 0, names.Length - 1)];
    }

    public static string AreaName(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return "未知之地";
        try { return ZoneDb.AreaName(areaId); }
        catch (Exception) { return areaId; }
    }

    public static string LevelLine(ClassId c, int level, int paragon) =>
        $"{ClassName(c)} · 等級 {level}" + (paragon > 0 ? $" · 燼紋 {paragon}" : "");

    /// <summary>One line describing a save slot, for captions and questions.</summary>
    public static string SlotLine(SlotInfo s)
    {
        if (s == null || !s.Exists) return "空欄位";
        if (s.Corrupt) return "毀損的存檔";
        string name = string.IsNullOrEmpty(s.Name) ? "無名者" : s.Name;
        return $"「{name}」{LevelLine(s.Class, s.Level, s.Paragon)}" + (s.Hardcore ? (s.Dead ? " · 已殞落" : " · 專家") : "");
    }

    // ───────────────────────── names ─────────────────────────

    public const int NameMax = 12;

    /// <summary>Trims the name and collapses runs of white space into single spaces.</summary>
    public static string CleanName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        bool gap = false;
        foreach (char ch in raw.Trim())
        {
            if (char.IsWhiteSpace(ch)) { gap = sb.Length > 0; continue; }
            if (gap) { sb.Append(' '); gap = false; }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>Null when the (already cleaned) name is acceptable, otherwise the reason to show the player.</summary>
    public static string NameError(string name)
    {
        if (string.IsNullOrEmpty(name)) return "名字不可留白——灰燼總得記住些什麼。";
        if (new StringInfo(name).LengthInTextElements > NameMax) return $"名字最多 {NameMax} 個字。";
        foreach (char ch in name)
        {
            bool ok = char.IsLetterOrDigit(ch) || char.IsSurrogate(ch) || ch == ' ' || ch == '・' || ch == '·' || ch == '-' || ch == '\'' || ch == '_';
            if (!ok) return "名字只能使用文字、數字、空格與「・」「-」。";
        }
        return null;
    }

    // ───────────────────────── class notes ─────────────────────────

    private static readonly ClassBlurb[] Blurbs =
    {
        new()
        {
            Role = "重甲近戰 · 巨劍", Weapon = "巨劍、巨斧、巨錘、長柄刀，或雙持單手武器", Difficulty = 1, Flourish = "Sword_Attack",
            ResourceNote = "攻擊累積，脫離戰鬥後逐漸消散。",
            DifficultyNote = "皮厚、直接、容錯高。適合初次踏入瑟蘭的持燼者。",
            Suggestions = new[] { "卡倫", "布蕾妲", "鐸恩", "瑟琳" },
        },
        new()
        {
            Role = "遠程施法 · 火、霜、雷", Weapon = "法杖，或魔杖、匕首、權杖配法器", Difficulty = 2, Flourish = "Spell_Simple_Shoot",
            ResourceNote = "隨時間穩定恢復。",
            DifficultyNote = "火力驚人但身軀脆弱，需要拿捏距離與冷卻。",
            Suggestions = new[] { "諾薇", "艾恩", "瑟菲爾", "歐琳" },
        },
        new()
        {
            Role = "游擊射手 · 弓弩與短刃", Weapon = "長弓、弩，或雙持劍與匕首", Difficulty = 2, Flourish = "Pistol_Shoot",
            ResourceNote = "恢復迅速；命中累積連擊點，用以強化殺招。",
            DifficultyNote = "仰賴走位與連擊點的節奏，停下腳步就是破綻。",
            Suggestions = new[] { "凱絲", "洛珂", "緹雅", "渡鴉" },
        },
        new()
        {
            Role = "召喚 · 骸骨僕從與暗影", Weapon = "巨鐮、巨劍，或單手武器配盾或法器", Difficulty = 1, Flourish = "Spell_Simple_Shoot",
            ResourceNote = "緩慢恢復；倒下的敵人留下殘魂，可供汲取與引爆。",
            DifficultyNote = "僕從為你擋下刀刃，讓你從容施咒。",
            Suggestions = new[] { "墨瑞", "希妲", "薩菈", "骨語者" },
        },
        new()
        {
            Role = "蛻形 · 熊靈、狼靈與風暴", Weapon = "法杖、巨錘、巨斧，或單手武器配圖騰", Difficulty = 2, Flourish = "Melee_Hook",
            ResourceNote = "以基本攻擊累積，用於蛻形後的重擊。",
            DifficultyNote = "在熊靈、狼靈與人形之間切換，需要熟悉各形態的時機。",
            Suggestions = new[] { "烏恩", "苔莎", "柯爾", "白蹄" },
        },
        new()
        {
            Role = "拳刃武者 · 拳、掌與氣焰", Weapon = "雙持拳刃，或巨錘、巨斧", Difficulty = 3, Flourish = "Punch_Cross",
            ResourceNote = "以拳腳累積，再一口氣宣洩。",
            DifficultyNote = "貼身搏擊、出手極快——沒有兵刃，也沒有退路。",
            Suggestions = new[] { "鐵嵐", "赫岩", "拓", "燎" },
        },
        new()
        {
            Role = "劍盾聖衛 · 光環與屏障", Weapon = "劍、錘、連枷配盾或燈，或雙手巨劍、巨錘、長柄刀", Difficulty = 1, Flourish = "Sword_Block",
            ResourceNote = "以劍盾交擊累積，化為燼光庇護與審判。",
            DifficultyNote = "屏障與治療兼備，穩紮穩打。",
            Suggestions = new[] { "葛蕾", "羅恩", "伊芙", "守夜人" },
        },
        new()
        {
            Role = "契約術士 · 燼魔與咒焰", Weapon = "魔杖、匕首、權杖配魔典或法器，或法杖、巨鐮", Difficulty = 3, Flourish = "Spell_Simple_Shoot",
            ResourceNote = "隨時間恢復；施法累積契印，用以號令契約魔。",
            DifficultyNote = "同時經營咒力與契印，還得指揮契約魔的位置。",
            Suggestions = new[] { "維克", "莎緹", "厄珞", "燼語" },
        },
    };

    public static ClassBlurb Blurb(ClassId c) => Blurbs[Math.Clamp((int)c, 0, Blurbs.Length - 1)];

    public static string DifficultyWord(int d) => d <= 1 ? "平易" : d == 2 ? "適中" : "艱深";

    /// <summary>The three skills shown on the creation screen: the first core skill, the last mastery skill and the ultimate.</summary>
    public static List<SkillDef> SignatureSkills(ClassId c)
    {
        var res = new List<SkillDef>(3);
        try
        {
            var all = SkillDb.Of(c).ToList();
            void Add(SkillDef s) { if (s != null && !res.Contains(s) && res.Count < 3) res.Add(s); }
            Add(all.Where(s => s.Cat == SkillCat.Core).OrderBy(s => s.UnlockLevel).FirstOrDefault());
            Add(all.Where(s => s.Cat == SkillCat.Mastery).OrderByDescending(s => s.UnlockLevel).FirstOrDefault());
            Add(all.FirstOrDefault(s => s.Cat == SkillCat.Ultimate));
            foreach (var s in all) Add(s);
        }
        catch (Exception e) { Log.Error("menu", "skill data unavailable for the creation screen", e); }
        return res;
    }

    /// <summary>A skill's description with its rank-1 damage filled in.</summary>
    public static string SkillText(SkillDef s)
    {
        if (s == null || string.IsNullOrEmpty(s.Desc)) return "";
        try { return string.Format(CultureInfo.InvariantCulture, s.Desc, s.DmgPct(1)); }
        catch (FormatException) { return s.Desc; }
    }
}

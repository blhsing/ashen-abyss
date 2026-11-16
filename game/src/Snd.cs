using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>
/// Sound effects front door. Every call is safe at any time: before the audio manager exists, with unknown names
/// (logged once in category "audio", then ignored) and with nodes that have already been freed.
/// Names are sound groups from res://assets/audio/manifest_audio.json ("hit_flesh" → hit_flesh_1..n.wav).
/// </summary>
public static class Snd
{
    private static int _errors;

    /// <summary>Positional one-shot at a world position; picks a random variation, never the same one twice in a row.</summary>
    public static void At(string name, Vector3 pos, float volume = 1f)
    {
        var a = AudioMan.I;
        if (a == null || string.IsNullOrEmpty(name)) return;
        try { a.PlayAt(name, pos, volume); }
        catch (Exception e) { Fail("At", name, e); }
    }

    /// <summary>Non-positional one-shot on the Ui bus (menus, pickups, stingers).</summary>
    public static void Ui(string name, float volume = 1f)
    {
        var a = AudioMan.I;
        if (a == null || string.IsNullOrEmpty(name)) return;
        try { a.PlayUi(name, volume); }
        catch (Exception e) { Fail("Ui", name, e); }
    }

    /// <summary>
    /// Starts (or replaces) the looping sound registered under <paramref name="key"/>. It follows <paramref name="follow"/>
    /// until <see cref="StopLoop"/> is called or the node is freed (or, for an actor, dies). A null node gives a
    /// non-positional loop. Calling it again with the same arguments is free, so it may be called every frame.
    /// A null or unknown name simply stops whatever was playing under the key.
    /// </summary>
    public static void Loop(string key, string name, Node3D follow) => Loop(key, name, follow, 1f);

    public static void Loop(string key, string name, Node3D follow, float volume)
    {
        var a = AudioMan.I;
        if (a == null || string.IsNullOrEmpty(key)) return;
        try { a.LoopStart(key, name, follow, volume); }
        catch (Exception e) { Fail("Loop", name, e); }
    }

    public static void StopLoop(string key)
    {
        var a = AudioMan.I;
        if (a == null || string.IsNullOrEmpty(key)) return;
        try { a.LoopStop(key); }
        catch (Exception e) { Fail("StopLoop", key, e); }
    }

    /// <summary>Fades out every loop started through <see cref="Loop(string,string,Node3D)"/> (not the low-life heartbeat).</summary>
    public static void StopAllLoops()
    {
        var a = AudioMan.I;
        if (a == null) return;
        try { a.LoopStopAll(); }
        catch (Exception e) { Fail("StopAllLoops", "", e); }
    }

    /// <summary>True when the name resolves to at least one loadable file. Does not log.</summary>
    public static bool Has(string name)
    {
        try { return SoundBank.Get(name, true) != null; }
        catch (Exception e) { Fail("Has", name, e); return false; }
    }

    private static void Fail(string where, string name, Exception e)
    {
        if (_errors++ < 8) Log.Error("audio", $"Snd.{where}('{name}') failed", e);
    }
}

/// <summary>One named sound with its loaded variations and the bookkeeping that keeps swarms from machine-gunning it.</summary>
internal sealed class SoundGroup
{
    public string Name;
    public AudioStream[] Streams;      // loaded one-shot variations, never empty, no null entries
    public AudioStream LoopStream;     // looping duplicate of the first variation, built on demand
    public int Last = -1;              // variation played last
    public ulong LastMs;               // when it was last triggered (engine ticks)
    public bool Played;
    public float Length;               // longest variation, seconds
    public bool Tonal;                 // stereo stingers and interface sounds: played at their authored pitch and level
}

/// <summary>
/// Resolves sound names to audio streams: manifest groups first, then file-name probing, then a few aliases.
/// Also owns stream loading (imported resource, or the raw file when the project has not been imported yet) and the
/// loop / one-shot variants of a stream, which are always made on a duplicate so the cached resource stays untouched.
/// </summary>
internal static class SoundBank
{
    public const string AudioRoot = "res://assets/audio/";
    public const float DefaultVoiceSeconds = 2.8f;
    private const string ManifestPath = AudioRoot + "manifest_audio.json";
    private const string VoLinesPath = "res://data/vo_lines.json";

    private static readonly Dictionary<string, string[]> Manifest = new(StringComparer.Ordinal);    // group → res:// paths
    private static readonly Dictionary<string, bool> LoopFlags = new(StringComparer.Ordinal);       // res:// path → manifest "loop"
    private static readonly Dictionary<string, SoundGroup> Groups = new(StringComparer.Ordinal);    // null value = known missing
    private static readonly HashSet<string> MissingSounds = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);
    private static readonly Queue<string> Warm = new();
    private static readonly List<string> PathScratch = new(8);
    private static readonly List<AudioStream> StreamScratch = new(8);
    private static readonly Random R = new();
    private static readonly JsonDocumentOptions JsonOpt = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private static Dictionary<string, int> _voLen;
    private static bool _init;
    private static int _groupsLoaded, _filesLoaded;

    /// <summary>Names the game asks for that have no recording of their own.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["boss_attack"] = "boss_slam",
        ["boss_idle"] = "brute_idle",
        ["construct_idle"] = "construct_step",
    };

    // ───────────────────────── manifest ─────────────────────────

    public static void Init()
    {
        if (_init) return;
        _init = true;
        try
        {
            if (!Godot.FileAccess.FileExists(ManifestPath))
            {
                Log.Warn("audio", $"{ManifestPath} not found; sounds are resolved by probing file names");
                return;
            }
            string text = Godot.FileAccess.GetFileAsString(ManifestPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Warn("audio", $"{ManifestPath} is empty; sounds are resolved by probing file names");
                return;
            }
            using var doc = JsonDocument.Parse(text.TrimStart('﻿'), JsonOpt);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { Log.Warn("audio", "audio manifest has an unexpected shape; probing file names instead"); return; }

            if (root.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Object)
            {
                var list = new List<string>(8);
                foreach (var g in groups.EnumerateObject())
                {
                    if (g.Value.ValueKind != JsonValueKind.Array) continue;
                    list.Clear();
                    foreach (var p in g.Value.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.String) continue;
                        string path = ToRes(p.GetString());
                        if (path != null) list.Add(path);
                    }
                    if (list.Count > 0) Manifest[g.Name] = list.ToArray();
                }
            }

            int files = 0;
            if (root.TryGetProperty("files", out var fileList) && fileList.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in fileList.EnumerateArray())
                {
                    if (f.ValueKind != JsonValueKind.Object) continue;
                    if (!f.TryGetProperty("path", out var pe) || pe.ValueKind != JsonValueKind.String) continue;
                    string path = ToRes(pe.GetString());
                    if (path == null) continue;
                    files++;
                    if (f.TryGetProperty("loop", out var le) && (le.ValueKind == JsonValueKind.True || le.ValueKind == JsonValueKind.False))
                        LoopFlags[path] = le.GetBoolean();
                }
            }

            foreach (var name in Manifest.Keys) Warm.Enqueue(name);
            Log.Info("audio", $"manifest read: {Manifest.Count} sound groups, {files} files listed");
        }
        catch (Exception e)
        {
            Log.Error("audio", "audio manifest unreadable; sounds are resolved by probing file names", e);
        }
    }

    private static string ToRes(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim().Replace('\\', '/');
        return path.StartsWith("res://", StringComparison.Ordinal) ? path : "res://" + path.TrimStart('/');
    }

    /// <summary>Whether a music or ambience file should loop: the manifest's flag when it has one, else the fallback.</summary>
    public static bool ShouldLoop(string resPath, bool fallback)
    {
        Init();
        return resPath != null && LoopFlags.TryGetValue(resPath, out bool loop) ? loop : fallback;
    }

    // ───────────────────────── groups ─────────────────────────

    /// <summary>The group for a sound name, loading it on first use. Null when nothing can be played for it.</summary>
    public static SoundGroup Get(string name, bool quiet = false)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!Groups.TryGetValue(name, out var g))
        {
            Init();
            g = Build(name);
            if (g == null)
            {
                string alias = AliasOf(name);
                if (alias != null)
                {
                    g = Get(alias, true);
                    if (g != null) NoteOnce("alias", name, $"sound '{name}' has no files of its own, using '{alias}'");
                }
            }
            Groups[name] = g;
        }
        if (g == null && !quiet && MissingSounds.Add(name)) Log.Warn("audio", $"sound '{name}' not found, ignoring");
        return g;
    }

    private static string AliasOf(string name)
    {
        if (Aliases.TryGetValue(name, out string alias)) return alias;
        // an unknown floor surface still deserves footsteps
        if (name.StartsWith("footstep_", StringComparison.Ordinal) && name != "footstep_stone") return "footstep_stone";
        return null;
    }

    private static SoundGroup Build(string name)
    {
        PathScratch.Clear();
        if (Manifest.TryGetValue(name, out var listed)) PathScratch.AddRange(listed);
        else
        {
            string stem = AudioRoot + "sfx/" + name;
            for (int i = 1; i <= 8; i++)
            {
                string p = stem + "_" + i + ".wav";
                if (!Exists(p)) break;
                PathScratch.Add(p);
            }
            if (PathScratch.Count == 0)
            {
                if (Exists(stem + ".wav")) PathScratch.Add(stem + ".wav");
                else if (Exists(stem + ".ogg")) PathScratch.Add(stem + ".ogg");
            }
        }
        if (PathScratch.Count == 0) return null;

        StreamScratch.Clear();
        float length = 0f;
        bool tonal = false;
        foreach (string path in PathScratch)
        {
            var s = LoadStream(path);
            if (s == null)
            {
                if (Reported.Add("file:" + path)) Log.Warn("audio", $"sound file {path} is listed for '{name}' but cannot be loaded");
                continue;
            }
            s = OneShot(s);
            StreamScratch.Add(s);
            length = Mathf.Max(length, (float)s.GetLength());
            if (s is AudioStreamWav { Stereo: true }) tonal = true;
        }
        if (StreamScratch.Count == 0) return null;
        _groupsLoaded++;
        _filesLoaded += StreamScratch.Count;
        return new SoundGroup { Name = name, Streams = StreamScratch.ToArray(), Length = length, Tonal = tonal };
    }

    /// <summary>A random variation, never the one that played last (when there is more than one).</summary>
    public static AudioStream Pick(SoundGroup g)
    {
        int n = g.Streams.Length, i;
        if (n == 1) i = 0;
        else if (g.Last < 0) i = R.Next(n);
        else { i = R.Next(n - 1); if (i >= g.Last) i++; }
        g.Last = i;
        return g.Streams[i];
    }

    /// <summary>The group's first variation as a seamless loop.</summary>
    public static AudioStream Looping(SoundGroup g) => g.LoopStream ??= Looped(g.Streams[0]);

    /// <summary>Loads a few sound groups per call so the first fight does not hitch on disk reads. True while work remains.</summary>
    public static bool WarmStep(ulong budgetUsec)
    {
        if (Warm.Count == 0) return false;
        ulong t0 = Time.GetTicksUsec();
        while (Warm.Count > 0 && Time.GetTicksUsec() - t0 < budgetUsec) Get(Warm.Dequeue(), true);
        if (Warm.Count == 0) Log.Info("audio", $"sound bank warm: {_groupsLoaded} groups, {_filesLoaded} files in memory");
        return Warm.Count > 0;
    }

    // ───────────────────────── streams ─────────────────────────

    public static bool Exists(string resPath)
    {
        try { return ResourceLoader.Exists(resPath) || Godot.FileAccess.FileExists(resPath); }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// Loads an audio file. Uses the imported resource when there is one; otherwise decodes the raw .wav / .ogg so a
    /// project that has not been imported yet still makes sound. Null (silently) when the file does not exist.
    /// </summary>
    public static AudioStream LoadStream(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;
        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                var s = ResourceLoader.Load<AudioStream>(resPath);
                if (s != null) return s;
            }
            if (Godot.FileAccess.FileExists(resPath))
            {
                if (resPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    return AudioStreamWav.LoadFromFile(resPath, new Godot.Collections.Dictionary());
                if (resPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    return AudioStreamOggVorbis.LoadFromFile(resPath);
            }
        }
        catch (Exception e)
        {
            if (Reported.Add("load:" + resPath)) Log.Error("audio", $"loading {resPath} failed", e);
        }
        return null;
    }

    /// <summary>A looping version of the stream. The loop flag is set on a duplicate, never on the shared resource.</summary>
    public static AudioStream Looped(AudioStream src)
    {
        if (src == null) return null;
        try
        {
            switch (src)
            {
                case AudioStreamWav w:
                {
                    var c = (AudioStreamWav)w.Duplicate(false);
                    int frames = (int)Math.Round(w.GetLength() * w.MixRate);
                    if (c.LoopMode == AudioStreamWav.LoopModeEnum.Disabled) c.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                    if (c.LoopEnd <= c.LoopBegin || c.LoopEnd > frames) { c.LoopBegin = 0; c.LoopEnd = frames; }
                    return c;
                }
                case AudioStreamOggVorbis o:
                {
                    var c = (AudioStreamOggVorbis)o.Duplicate(false);
                    c.Loop = true;
                    return c;
                }
            }
        }
        catch (Exception e)
        {
            if (Reported.Add("loop:" + src.ResourcePath)) Log.Error("audio", $"cannot make a looping copy of {src.ResourcePath}", e);
        }
        return src;
    }

    /// <summary>A version of the stream that stops at its end (files with embedded loop points would otherwise never free their voice).</summary>
    public static AudioStream OneShot(AudioStream src)
    {
        try
        {
            switch (src)
            {
                case AudioStreamWav w when w.LoopMode != AudioStreamWav.LoopModeEnum.Disabled:
                {
                    var c = (AudioStreamWav)w.Duplicate(false);
                    c.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
                    return c;
                }
                case AudioStreamOggVorbis o when o.Loop:
                {
                    var c = (AudioStreamOggVorbis)o.Duplicate(false);
                    c.Loop = false;
                    return c;
                }
            }
        }
        catch (Exception e)
        {
            if (Reported.Add("oneshot:" + src?.ResourcePath)) Log.Error("audio", $"cannot make a one-shot copy of {src?.ResourcePath}", e);
        }
        return src;
    }

    // ───────────────────────── voice lines ─────────────────────────

    /// <summary>
    /// How long to wait before reporting a voice line as finished when it has no recording: a fixed default, stretched
    /// to the line's text length when data/vo_lines.json knows the line.
    /// </summary>
    public static float VoiceFallbackSeconds(string voiceId)
    {
        if (string.IsNullOrEmpty(voiceId)) return DefaultVoiceSeconds;
        if (_voLen == null) LoadVoLines();
        return _voLen.TryGetValue(voiceId, out int chars) && chars > 0
            ? Mathf.Clamp(1.0f + 0.22f * chars, 1.6f, 12f)
            : DefaultVoiceSeconds;
    }

    private static void LoadVoLines()
    {
        _voLen = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            if (!Godot.FileAccess.FileExists(VoLinesPath)) return;
            string text = Godot.FileAccess.GetFileAsString(VoLinesPath);
            if (string.IsNullOrWhiteSpace(text)) return;
            using var doc = JsonDocument.Parse(text.TrimStart('﻿'), JsonOpt);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                if (!e.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) continue;
                if (!e.TryGetProperty("text", out var tx) || tx.ValueKind != JsonValueKind.String) continue;
                string key = id.GetString();
                if (!string.IsNullOrEmpty(key)) _voLen[key] = (tx.GetString() ?? "").Length;
            }
        }
        catch (Exception e)
        {
            Log.Warn("audio", "vo_lines.json unreadable, missing voice lines use the default delay: " + e.Message);
        }
    }

    // ───────────────────────── logging ─────────────────────────

    /// <summary>Warns once that a music track, ambience bed or voice line does not exist.</summary>
    public static void MissingOnce(string kind, string name)
    {
        if (Reported.Add(kind + ":" + name)) Log.Warn("audio", $"{kind} '{name}' not found, ignoring");
    }

    public static void NoteOnce(string kind, string name, string message)
    {
        if (Reported.Add(kind + ":" + name)) Log.Info("audio", message);
    }
}

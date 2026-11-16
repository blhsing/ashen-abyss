using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// The whole audio side of the game: bus layout, the listener, pooled positional sound effects, loops that follow
/// nodes, adaptive music (a base layer plus a combat layer in exact sync), ambience beds, voice lines with ducking and
/// the low-life heartbeat. Sound effects are requested through <see cref="Snd"/>.
///
/// Buses (built at runtime): Master (gentle limiter) ← Music, Sfx, Amb, Voice, Ui.
/// The listener is not the camera: the camera hangs 24 m above the fight, so an AudioListener3D rides a little above
/// the hero instead, turned to the camera's heading so that screen-right is still heard on the right.
/// Everything tolerates missing files: each missing name is logged once (category "audio") and then ignored.
/// </summary>
public partial class AudioMan : Node
{
    public static AudioMan I;

    // ───────────────────────── tuning ─────────────────────────
    private const int SfxVoices = 28;                  // pooled positional one-shot players
    private const int UiVoices = 6;                    // pooled non-positional players
    private const int MaxLoopVoices = 10;              // positional loops that sound at once (the nearest ones)
    private const int MaxSameLoop = 4;                 // …of which at most this many share one sound
    private const float SfxUnit = 10f, SfxMaxDb = 1.5f, SfxRange = 46f;    // ≈ +1 dB on the hero, −7 dB at 15 m, −23 dB at 35 m
    private const float LoopUnit = 5f, LoopMaxDb = 0f, LoopRange = 28f;    // torches, portals, fires: local colour
    private const float FullPoolRange = 30f;           // with every voice busy, sounds further than this are dropped
    private const float EarHeight = 4f;                // listener height above the hero's feet (keeps on-screen panning gentle)
    private const ulong RetriggerMs = 40;              // minimum gap between two starts of the same sound
    private const ulong StealAfterMs = 150;            // an instance younger than this is never cut off by its own kind
    private const float SilentDb = -80f;
    private const float DuckDb = -8f;                  // music and ambience under a voice line
    private const float DuckAttack = 0.25f, DuckRelease = 0.6f;
    private const float CombatRise = 2f, CombatFall = 4f;                  // seconds for the combat layer to come and go
    private const float LowLifeOn = 0.30f, LowLifeOff = 0.36f;
    private const string HeartKey = "_audio_heartbeat";

    private const string MasterName = "Master", MusicName = "Music", SfxName = "Sfx", AmbName = "Amb", VoiceName = "Voice", UiName = "Ui";
    private static readonly StringName MasterBus = MasterName, MusicBus = MusicName, SfxBus = SfxName, AmbBus = AmbName, VoiceBus = VoiceName, UiBus = UiName;
    private static readonly char[] SplitChars = { '_', ':' };
    private static bool _busesReady;

    // ───────────────────────── state ─────────────────────────
    private sealed class Voice3 { public AudioStreamPlayer3D P; public SoundGroup G; public AudioStream S; public ulong StartMs; }
    private sealed class Voice2 { public AudioStreamPlayer P; public SoundGroup G; public AudioStream S; public ulong StartMs; }

    private sealed class LoopRec
    {
        public string Key;
        public SoundGroup G;
        public AudioStream Stream;
        public Node3D Follow;
        public Actor Actor;                    // set when the followed node is an actor: the loop ends when it dies
        public bool Positional, Gameplay, InTree, Want, Stopping;
        public Vector3 Pos, SetPos;
        public float Volume = 1f, Pitch = 1f, SetPitch = 1f, Fade, Db = 999f, Dist2;
        public AudioStreamPlayer3D P3;
        public AudioStreamPlayer P2;
        public bool HasVoice => P3 != null || P2 != null;
    }

    /// <summary>One music track or ambience bed with its own player, fading in or out.</summary>
    private sealed class Bed
    {
        public string Id;
        public AudioStreamPlayer Player;
        public AudioStreamSynchronized Layers; // zone music: stream 0 = base, stream 1 = combat
        public bool Loops = true;
        public float Fade, Target = 1f, Secs = 2f, Clock, Db = 999f, LayerDb = 999f;
    }

    private static readonly Comparison<LoopRec> ByDistance = (a, b) => a.Dist2.CompareTo(b.Dist2);

    private bool _built, _inTree, _muted, _volumesSet;
    private float _dbMaster, _dbMusic = -6f, _dbSfx, _dbVoice, _dbAmb = -4f;
    private ulong _lastUsec;
    private int _tickErrors;
    private float _warmDelay = 0.5f;
    private readonly Random _rng = new();

    private AudioListener3D _ear;
    private Vector3 _earPos = new(0f, EarHeight, 0f), _earFwd = Vector3.Forward;
    private bool _earClaimLogged;

    private readonly Voice3[] _sfx = new Voice3[SfxVoices];
    private readonly Voice2[] _ui = new Voice2[UiVoices];

    private readonly Dictionary<string, LoopRec> _loopByKey = new(StringComparer.Ordinal);
    private readonly List<LoopRec> _loops = new();
    private readonly List<LoopRec> _rankBuf = new();
    private readonly Stack<AudioStreamPlayer3D> _loopPool3 = new();
    private readonly Stack<AudioStreamPlayer> _loopPool2 = new();
    private float _rankT, _worldGain = 1f;
    private LoopRec _heart;
    private bool _heartOn;

    private Bed _music, _amb;
    private readonly List<Bed> _musicOut = new(), _ambOut = new();
    private string _musicId, _ambId;
    private float _musicFade = 2f, _ambFade = 2f;
    private bool _musicDirty, _ambDirty;
    private float _combat, _combatTarget, _autoT;
    private bool _combatManual;

    private AudioStreamPlayer _voice;
    private bool _voiceActive, _voiceTimed;
    private float _voiceClock, _voiceLeft;
    private Action _voiceCb;
    private string _voiceId;
    private float _duck;

    /// <summary>The track last requested with <see cref="PlayMusic"/> (null when silent).</summary>
    public string CurrentMusic => _musicId;
    public string CurrentAmbience => _ambId;
    /// <summary>Smoothed combat intensity 0..1 as currently heard.</summary>
    public float CombatLevel => _combat;
    /// <summary>True from <see cref="PlayVoice"/> until the line has ended (or its stand-in delay has passed).</summary>
    public bool VoiceActive => _voiceActive;
    /// <summary>Id of the voice line in progress, or null.</summary>
    public string CurrentVoice => _voiceActive ? _voiceId : null;
    public bool Muted => _muted;

    // ───────────────────────── lifecycle ─────────────────────────

    public AudioMan()
    {
        I = this;
        Name = "Audio";
        ProcessPriority = 100;      // after the actors have moved this frame, so the listener and loops sit where they are now
    }

    public override void _EnterTree()
    {
        I = this;
        _inTree = true;
    }

    public override void _ExitTree()
    {
        _inTree = false;
        if (I == this) I = null;
    }

    public override void _Ready()
    {
        try
        {
            EnsureBuses();
            SoundBank.Init();

            var vp = GetViewport();
            if (vp != null) vp.AudioListenerEnable3D = true;
            _ear = new AudioListener3D { Name = "Ear", Position = _earPos };
            AddChild(_ear);
            _ear.MakeCurrent();

            for (int i = 0; i < SfxVoices; i++) _sfx[i] = new Voice3 { P = New3D(SfxUnit, SfxMaxDb, SfxRange) };
            for (int i = 0; i < UiVoices; i++)
            {
                var p = new AudioStreamPlayer { Bus = UiBus };
                AddChild(p);
                _ui[i] = new Voice2 { P = p };
            }
            _voice = new AudioStreamPlayer { Bus = VoiceBus, Name = "Voice" };
            AddChild(_voice);
            _built = true;

            if (!_volumesSet) ApplyVolumes(Game.I?.Cfg ?? new Settings());
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--mute") { Mute(true); break; }
            Log.Info("audio", $"audio ready: {SfxVoices} positional voices, {UiVoices} interface voices, up to {MaxLoopVoices} loops");
        }
        catch (Exception e)
        {
            Log.Error("audio", "audio start-up failed; the game continues without sound", e);
        }
    }

    public override void _Process(double delta)
    {
        // Audio is infrastructure, like the UI: it keeps running while the game is paused so that music fades, voice
        // lines and menu sounds carry on. The pause is honoured inside: actor loops and the heartbeat fall silent
        // while the world is frozen, and nothing here advances gameplay.
        try { Tick(); }
        catch (Exception e)
        {
            if (_tickErrors++ < 6) Log.Error("audio", "audio tick failed", e);
        }
    }

    private void Tick()
    {
        ulong now = Time.GetTicksUsec();
        float dt = _lastUsec == 0 ? 1f / 60f : Mathf.Clamp((now - _lastUsec) / 1_000_000f, 0f, 0.1f);    // real time: fades ignore hit-stop
        _lastUsec = now;
        if (!_built)
        {
            TickVoice(dt);          // start-up failed: voice callbacks must still arrive or dialogue would stall
            return;
        }

        var game = Game.I;
        bool frozen = game != null && game.State == Game.GameState.Play && game.PauseLocks > 0;
        _worldGain = Mathf.MoveToward(_worldGain, frozen ? 0f : 1f, dt / 0.2f);

        UpdateEar();
        if (_warmDelay > 0f) _warmDelay -= dt; else SoundBank.WarmStep(1500);
        TickMusic(dt);
        TickAmbience(dt);
        TickVoice(dt);
        TickDuck(dt);
        TickHeartbeat();
        TickLoops(dt);
    }

    // ───────────────────────── buses and volumes ─────────────────────────

    private static void EnsureBuses()
    {
        if (_busesReady && AudioServer.GetBusIndex(UiBus) >= 0) return;
        AddBus(MusicName, MusicBus);
        AddBus(SfxName, SfxBus);
        AddBus(AmbName, AmbBus);
        AddBus(VoiceName, VoiceBus);
        AddBus(UiName, UiBus);

        bool limited = false;
        for (int i = 0, n = AudioServer.GetBusEffectCount(0); i < n; i++)
            if (AudioServer.GetBusEffect(0, i) is AudioEffectHardLimiter) { limited = true; break; }
        // gentle: nothing happens below −1 dBFS, peaks above are held with a slow release so it never pumps
        if (!limited) AudioServer.AddBusEffect(0, new AudioEffectHardLimiter { CeilingDb = -1f, PreGainDb = 0f, Release = 0.2f }, -1);

        _busesReady = true;
        Log.Info("audio", $"bus layout: Master (limiter) ← Music, Sfx, Amb, Voice, Ui; {AudioServer.BusCount} buses, mix rate {AudioServer.GetMixRate():0} Hz");
    }

    private static void AddBus(string name, StringName sn)
    {
        if (AudioServer.GetBusIndex(sn) >= 0) return;
        AudioServer.AddBus(-1);
        int i = AudioServer.BusCount - 1;
        AudioServer.SetBusName(i, name);
        AudioServer.SetBusSend(i, MasterBus);
    }

    /// <summary>Slider position 0..1 → decibels. Squared so the middle of the slider sounds like "half as loud".</summary>
    private static float SliderDb(float v)
    {
        if (float.IsNaN(v)) return 0f;
        v = Mathf.Clamp(v, 0f, 1f);
        return v <= 0.0005f ? SilentDb : Mathf.Max(SilentDb, Mathf.LinearToDb(v * v));
    }

    /// <summary>Takes the five volume sliders from the settings. The Ui bus follows the effects volume.</summary>
    public void ApplyVolumes(Settings s)
    {
        if (s == null) return;
        try
        {
            EnsureBuses();
            _dbMaster = SliderDb(s.Master);
            _dbMusic = SliderDb(s.Music);
            _dbSfx = SliderDb(s.Sfx);
            _dbVoice = SliderDb(s.Voice);
            _dbAmb = SliderDb(s.Ambience);
            _volumesSet = true;
            ApplyBusVolumes();
            Log.Info("audio", $"volumes dB: master={_dbMaster:0.0} music={_dbMusic:0.0} sfx={_dbSfx:0.0} voice={_dbVoice:0.0} amb={_dbAmb:0.0}");
        }
        catch (Exception e) { Log.Error("audio", "applying volumes failed", e); }
    }

    private void ApplyBusVolumes()
    {
        float duck = DuckDb * _duck;
        SetBus(MasterBus, _dbMaster);
        SetBus(MusicBus, _dbMusic + duck);
        SetBus(AmbBus, _dbAmb + duck);
        SetBus(SfxBus, _dbSfx);
        SetBus(UiBus, _dbSfx);
        SetBus(VoiceBus, _dbVoice);
    }

    private static void SetBus(StringName bus, float db)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i >= 0) AudioServer.SetBusVolumeDb(i, Mathf.Max(db, SilentDb));
    }

    /// <summary>Silences (or restores) everything at the Master bus; used by the --mute test switch.</summary>
    public void Mute(bool m)
    {
        try
        {
            EnsureBuses();
            AudioServer.SetBusMute(0, m);
            if (m != _muted) Log.Info("audio", m ? "master muted" : "master unmuted");
            _muted = m;
        }
        catch (Exception e) { Log.Error("audio", "mute failed", e); }
    }

    // ───────────────────────── listener ─────────────────────────

    private void UpdateEar()
    {
        Vector3 target = _earPos - new Vector3(0f, EarHeight, 0f);
        bool anchored = false;
        var hero = World.P;
        if (hero != null && IsInstanceValid(hero) && hero.IsInsideTree()) { target = hero.GlobalPosition; anchored = true; }

        Vector3 fwd = _earFwd;
        var cam = GetViewport()?.GetCamera3D();
        if (cam != null && IsInstanceValid(cam) && cam.IsInsideTree())
        {
            Basis b = cam.GlobalBasis;
            Vector3 look = -b.Z;
            if (!anchored)
            {
                Vector3 c = cam.GlobalPosition;
                target = look.Y < -0.05f ? c + look * (c.Y / -look.Y) : c;       // where the centre of the view meets the ground
            }
            var flat = new Vector3(look.X, 0f, look.Z);
            if (flat.LengthSquared() < 1e-4f) flat = new Vector3(b.Y.X, 0f, b.Y.Z);   // looking straight down: screen-up is "ahead"
            if (flat.LengthSquared() >= 1e-4f) fwd = flat.Normalized();
        }

        Vector3 pos = target + new Vector3(0f, EarHeight, 0f);
        if (pos != _earPos) { _earPos = pos; _ear.Position = pos; }
        if (fwd != _earFwd) { _earFwd = fwd; _ear.Basis = Basis.LookingAt(fwd, Vector3.Up); }

        // Positional sound is tuned for this listener. Another AudioListener3D (the camera rig carries one) may have
        // made itself current when it entered the tree; take the role back.
        if (!_ear.IsCurrent())
        {
            _ear.MakeCurrent();
            if (!_earClaimLogged) { _earClaimLogged = true; Log.Info("audio", "another audio listener was current; the hero listener took over"); }
        }
    }

    /// <summary>Listener position for this very moment (the hero may have moved since the last audio tick).</summary>
    private Vector3 EarNow()
    {
        var hero = World.P;
        if (hero != null && IsInstanceValid(hero) && hero.IsInsideTree()) return hero.GlobalPosition + new Vector3(0f, EarHeight, 0f);
        return _earPos;
    }

    // ───────────────────────── one-shots ─────────────────────────

    private AudioStreamPlayer3D New3D(float unit, float maxDb, float range)
    {
        var p = new AudioStreamPlayer3D
        {
            Bus = SfxBus,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            UnitSize = unit,
            MaxDb = maxDb,
            MaxDistance = range,
            AttenuationFilterCutoffHz = 5000f,
            AttenuationFilterDb = -14f,       // distant fights lose their bite rather than just their level
            MaxPolyphony = 1,
            AreaMask = 0,                     // no reverb areas in this game: skip the per-voice area query
        };
        AddChild(p);
        return p;
    }

    private float Jitter(float range) => ((float)_rng.NextDouble() * 2f - 1f) * range;

    internal void PlayAt(string name, Vector3 pos, float volume)
    {
        if (!_built || !_inTree || !(volume > 0.001f)) return;
        var g = SoundBank.Get(name);
        if (g == null) return;
        ulong now = Time.GetTicksMsec();
        if (g.Played && now - g.LastMs < RetriggerMs) return;
        float d2 = pos.DistanceSquaredTo(EarNow());
        if (!(d2 <= SfxRange * SfxRange)) return;         // out of earshot (also rejects NaN positions)

        int limit = g.Length > 2f ? 3 : 4, same = 0;
        Voice3 free = null, oldest = null, oldestSame = null;
        for (int i = 0; i < _sfx.Length; i++)
        {
            var v = _sfx[i];
            if (v.G == null) { free ??= v; continue; }
            if (!v.P.Playing) { v.G = null; free ??= v; continue; }
            if (oldest == null || v.StartMs < oldest.StartMs) oldest = v;
            if (v.G == g)
            {
                same++;
                if (oldestSame == null || v.StartMs < oldestSame.StartMs) oldestSame = v;
            }
        }

        Voice3 use;
        if (same >= limit)
        {
            if (now - oldestSame.StartMs < StealAfterMs) return;
            use = oldestSame;
        }
        else if (free != null) use = free;
        else
        {
            if (oldest == null || d2 > FullPoolRange * FullPoolRange) return;
            use = oldest;
        }

        var stream = SoundBank.Pick(g);
        var p = use.P;
        if (use.S != stream) { p.Stream = stream; use.S = stream; }
        p.Position = pos;
        float db = Mathf.LinearToDb(Mathf.Min(volume, 2f)), pitch = 1f;
        if (!g.Tonal)
        {
            db += Jitter(1f);
            pitch += Jitter(g.Length > 2.5f ? 0.02f : 0.04f);
        }
        p.VolumeDb = db;
        p.PitchScale = pitch;
        p.Play();
        use.G = g;
        use.StartMs = now;
        g.Played = true;
        g.LastMs = now;
    }

    internal void PlayUi(string name, float volume)
    {
        if (!_built || !_inTree || !(volume > 0.001f)) return;
        var g = SoundBank.Get(name);
        if (g == null) return;
        ulong now = Time.GetTicksMsec();
        if (g.Played && now - g.LastMs < RetriggerMs) return;

        int same = 0;
        Voice2 free = null, oldest = null, oldestSame = null;
        for (int i = 0; i < _ui.Length; i++)
        {
            var v = _ui[i];
            if (v.G == null) { free ??= v; continue; }
            if (!v.P.Playing) { v.G = null; free ??= v; continue; }
            if (oldest == null || v.StartMs < oldest.StartMs) oldest = v;
            if (v.G == g)
            {
                same++;
                if (oldestSame == null || v.StartMs < oldestSame.StartMs) oldestSame = v;
            }
        }

        Voice2 use;
        if (same >= 3)
        {
            if (now - oldestSame.StartMs < StealAfterMs) return;
            use = oldestSame;
        }
        else use = free ?? oldest;
        if (use == null) return;

        var stream = SoundBank.Pick(g);
        var p = use.P;
        if (use.S != stream) { p.Stream = stream; use.S = stream; }
        float db = Mathf.LinearToDb(Mathf.Min(volume, 2f)), pitch = 1f;
        if (!g.Tonal)
        {
            db += Jitter(0.5f);
            pitch += Jitter(0.025f);
        }
        p.VolumeDb = db;
        p.PitchScale = pitch;
        p.Play();
        use.G = g;
        use.StartMs = now;
        g.Played = true;
        g.LastMs = now;
    }

    // ───────────────────────── loops ─────────────────────────

    internal void LoopStart(string key, string name, Node3D follow, float volume) => StartLoop(key, name, follow, volume);

    private LoopRec StartLoop(string key, string name, Node3D follow, float volume)
    {
        if (!_built || string.IsNullOrEmpty(key)) return null;
        var g = string.IsNullOrEmpty(name) ? null : SoundBank.Get(name);
        bool positional = follow != null;
        if (g == null || (positional && (!IsInstanceValid(follow) || follow.IsQueuedForDeletion())))
        {
            LoopStop(key);
            return null;
        }
        volume = float.IsNaN(volume) ? 1f : Mathf.Clamp(volume, 0f, 2f);

        if (_loopByKey.TryGetValue(key, out var cur))
        {
            if (cur.G == g && cur.Follow == follow) { cur.Volume = volume; return cur; }
            cur.Stopping = true;                    // the old sound fades out under the new one
            _loopByKey.Remove(key);
        }
        var stream = SoundBank.Looping(g);
        if (stream == null) return null;

        var l = new LoopRec { Key = key, G = g, Stream = stream, Follow = follow, Actor = follow as Actor, Positional = positional, Volume = volume };
        l.Gameplay = l.Actor != null;
        if (positional && follow.IsInsideTree()) { l.InTree = true; l.Pos = follow.GlobalPosition; }
        _loopByKey[key] = l;
        _loops.Add(l);
        _rankT = 0f;                                // choose voices on the very next tick
        return l;
    }

    internal void LoopStop(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_loopByKey.Remove(key, out var l)) l.Stopping = true;
    }

    internal void LoopStopAll()
    {
        foreach (var l in _loops)
        {
            if (l == _heart) continue;
            l.Stopping = true;
            _loopByKey.Remove(l.Key);
        }
        if (_heart != null && !_heart.Stopping) _loopByKey[HeartKey] = _heart;
    }

    private void EndLoop(LoopRec l)
    {
        l.Stopping = true;
        l.Follow = null;
        l.Actor = null;
        if (_loopByKey.TryGetValue(l.Key, out var cur) && cur == l) _loopByKey.Remove(l.Key);
    }

    private void TickLoops(float dt)
    {
        if (_loops.Count == 0) return;
        _rankT -= dt;
        bool rank = _rankT <= 0f;
        if (rank) _rankT = 0.2f;

        // 1) follow the nodes; silent loops are only looked at on ranking ticks
        for (int i = 0; i < _loops.Count; i++)
        {
            var l = _loops[i];
            if (l.Stopping || !l.Positional) continue;
            if (!rank && l.P3 == null) continue;
            var f = l.Follow;
            if (f == null || !IsInstanceValid(f) || f.IsQueuedForDeletion() || (l.Actor != null && l.Actor.Dead)) { EndLoop(l); continue; }
            l.InTree = f.IsInsideTree();
            if (l.InTree) l.Pos = f.GlobalPosition;
        }

        // 2) hand the few real voices to the nearest loops, at most MaxSameLoop of one sound
        if (rank)
        {
            _rankBuf.Clear();
            for (int i = 0; i < _loops.Count; i++)
            {
                var l = _loops[i];
                l.Want = false;
                if (l.Stopping) continue;
                if (!l.Positional) { l.Want = true; continue; }
                if (!l.InTree) continue;
                float d2 = l.Pos.DistanceSquaredTo(_earPos);
                if (!(d2 < LoopRange * LoopRange)) continue;
                l.Dist2 = l.P3 != null ? d2 * 0.8f : d2;     // a loop that is already sounding keeps its voice a little longer
                _rankBuf.Add(l);
            }
            _rankBuf.Sort(ByDistance);
            int voices = 0;
            for (int i = 0; i < _rankBuf.Count && voices < MaxLoopVoices; i++)
            {
                var l = _rankBuf[i];
                int same = 0;
                for (int j = 0; j < i; j++) if (_rankBuf[j].Want && _rankBuf[j].G == l.G) same++;
                if (same >= MaxSameLoop) continue;
                l.Want = true;
                voices++;
            }
            _rankBuf.Clear();
        }

        // 3) voices, fades and positions
        for (int i = _loops.Count - 1; i >= 0; i--)
        {
            var l = _loops[i];
            bool want = l.Want && !l.Stopping;
            if (want && !l.HasVoice) GiveVoice(l);
            l.Fade = Mathf.MoveToward(l.Fade, want ? 1f : 0f, dt / (want ? 0.12f : 0.3f));
            if (l.HasVoice)
            {
                if (!want && l.Fade <= 0f) TakeVoice(l);
                else
                {
                    float gain = l.Fade * l.Volume * (l.Gameplay ? _worldGain : 1f);
                    float db = gain <= 0.0001f ? SilentDb : Mathf.LinearToDb(gain);
                    if (Mathf.Abs(db - l.Db) > 0.05f)
                    {
                        l.Db = db;
                        if (l.P3 != null) l.P3.VolumeDb = db; else l.P2.VolumeDb = db;
                    }
                    if (l.P3 != null && l.Pos != l.SetPos) { l.SetPos = l.Pos; l.P3.Position = l.Pos; }
                    if (l.Pitch != l.SetPitch)
                    {
                        l.SetPitch = l.Pitch;
                        if (l.P3 != null) l.P3.PitchScale = l.Pitch; else l.P2.PitchScale = l.Pitch;
                    }
                }
            }
            if (l.Stopping && !l.HasVoice) _loops.RemoveAt(i);
        }
    }

    private void GiveVoice(LoopRec l)
    {
        // several copies of one loop (a hall of torches) must not start in phase
        float from = 0f;
        for (int i = 0; i < _loops.Count; i++)
        {
            var o = _loops[i];
            if (o != l && o.G == l.G && o.HasVoice) { from = (float)(_rng.NextDouble() * Math.Max(0.0, l.G.Length - 0.1)); break; }
        }
        if (l.Positional)
        {
            var p = _loopPool3.Count > 0 ? _loopPool3.Pop() : New3D(LoopUnit, LoopMaxDb, LoopRange);
            p.Stream = l.Stream;
            p.Position = l.Pos;
            p.VolumeDb = SilentDb;
            p.PitchScale = l.Pitch;
            p.Play(from);
            l.P3 = p;
        }
        else
        {
            AudioStreamPlayer p;
            if (_loopPool2.Count > 0) p = _loopPool2.Pop();
            else { p = new AudioStreamPlayer { Bus = SfxBus }; AddChild(p); }
            p.Stream = l.Stream;
            p.VolumeDb = SilentDb;
            p.PitchScale = l.Pitch;
            p.Play(from);
            l.P2 = p;
        }
        l.Fade = 0f;
        l.Db = SilentDb;
        l.SetPos = l.Pos;
        l.SetPitch = l.Pitch;
    }

    private void TakeVoice(LoopRec l)
    {
        if (l.P3 != null)
        {
            l.P3.Stop();
            l.P3.Stream = null;
            _loopPool3.Push(l.P3);
            l.P3 = null;
        }
        if (l.P2 != null)
        {
            l.P2.Stop();
            l.P2.Stream = null;
            _loopPool2.Push(l.P2);
            l.P2 = null;
        }
        l.Fade = 0f;
    }

    // ───────────────────────── low-life heartbeat ─────────────────────────

    private void TickHeartbeat()
    {
        var hero = World.P;
        var game = Game.I;
        float frac = 1f;
        bool alive = false;
        if (game != null && game.State == Game.GameState.Play && hero != null && IsInstanceValid(hero) && !hero.Dead && hero.MaxLife > 0f)
        {
            frac = Mathf.Clamp(hero.Life / hero.MaxLife, 0f, 1f);
            alive = true;
        }
        bool want = alive && frac < (_heartOn ? LowLifeOff : LowLifeOn);
        if (want != _heartOn)
        {
            _heartOn = want;
            if (want)
            {
                _heart = StartLoop(HeartKey, "heartbeat_loop", null, 0.6f);
                if (_heart != null) _heart.Gameplay = true;          // falls silent while the game is paused
                Log.Debug("audio", "heartbeat on");
            }
            else
            {
                LoopStop(HeartKey);
                _heart = null;
                Log.Debug("audio", "heartbeat off");
            }
        }
        if (_heartOn && _heart != null)
        {
            float danger = 1f - Mathf.Clamp(frac / LowLifeOn, 0f, 1f);    // 0 at the threshold, 1 at death's door
            _heart.Volume = Mathf.Lerp(0.55f, 1f, danger);
            _heart.Pitch = Mathf.Lerp(1f, 1.16f, danger);
        }
    }

    // ───────────────────────── music ─────────────────────────

    private static string Norm(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();
        return id == "none" ? null : id;
    }

    /// <summary>
    /// Crossfades to a track: "menu_theme", "town", "boss", "boss_final", "intro_bed" (plays once), or a zone such as
    /// "wastes", which starts wastes_base and wastes_combat in exact sync with the combat layer silent until
    /// <see cref="SetCombat"/> raises it. Null, "" or "none" fades to silence. Asking for the current track does nothing.
    /// </summary>
    public void PlayMusic(string track, float fade = 2f)
    {
        track = Norm(track);
        if (track == _musicId) return;
        _musicId = track;
        _musicFade = float.IsNaN(fade) ? 2f : Mathf.Max(0.02f, fade);
        _musicDirty = true;
        Log.Info("audio", $"music → {track ?? "(silence)"} over {_musicFade:0.0}s");
    }

    /// <summary>
    /// Combat intensity 0..1 for the zone's combat layer; it rises over about two seconds and falls over about four.
    /// Until this is called for the first time the intensity is derived from the hero's own combat state; after that
    /// the caller owns it.
    /// </summary>
    public void SetCombat(float intensity)
    {
        _combatManual = true;
        _combatTarget = float.IsNaN(intensity) ? 0f : Mathf.Clamp(intensity, 0f, 1f);
    }

    private void TickMusic(float dt)
    {
        if (_musicDirty)
        {
            _musicDirty = false;
            SwitchBed(ref _music, _musicOut, _musicId, _musicFade, true);
        }

        if (!_combatManual)
        {
            _autoT -= dt;
            if (_autoT <= 0f) { _autoT = 0.25f; _combatTarget = AutoCombat(); }
        }
        _combat = Mathf.MoveToward(_combat, _combatTarget, dt / (_combatTarget > _combat ? CombatRise : CombatFall));
        if (_music?.Layers != null)
        {
            float g = Mathf.Sin(_combat * Mathf.Pi * 0.5f);
            float db = g <= 0.0001f ? SilentDb : Mathf.LinearToDb(g);
            if (Mathf.Abs(db - _music.LayerDb) > 0.1f)
            {
                _music.LayerDb = db;
                _music.Layers.SetSyncStreamVolume(1, db);
            }
        }

        TickBeds(ref _music, _musicOut, ref _musicId, dt, "music");
    }

    /// <summary>Stand-in for callers that never drive the combat layer: fighting with enemies close by.</summary>
    private float AutoCombat()
    {
        var game = Game.I;
        var hero = World.P;
        if (game == null || game.State != Game.GameState.Play) return 0f;
        if (game.PauseLocks > 0) return _combatTarget;
        if (hero == null || !IsInstanceValid(hero) || hero.Dead || hero.InCombat <= 0f || !hero.IsInsideTree()) return 0f;
        int near = World.CountMonstersNear(hero.Pos2, 18f);
        return near <= 0 ? 0f : Mathf.Clamp(0.55f + 0.09f * near, 0f, 1f);
    }

    private static bool FindMusic(string name, out AudioStream main, out AudioStream combat, out string path)
    {
        const string dir = SoundBank.AudioRoot + "music/";
        combat = null;
        path = dir + name + "_base.ogg";
        main = SoundBank.LoadStream(path);
        if (main != null)
        {
            combat = SoundBank.LoadStream(dir + name + "_combat.ogg");
            if (combat == null) SoundBank.MissingOnce("music", name + "_combat");
            return true;
        }
        path = dir + name + ".ogg";
        main = SoundBank.LoadStream(path);
        return main != null;
    }

    private Bed BuildMusic(string id)
    {
        string name = id;
        if (!FindMusic(name, out var main, out var combat, out string path))
        {
            // a dungeon or sub-area falls back to the music of its region: "wastes_d1" → "wastes"
            int cut = id.IndexOfAny(SplitChars);
            if (cut <= 0 || !FindMusic(name = id.Substring(0, cut), out main, out combat, out path))
            {
                SoundBank.MissingOnce("music", id);
                return null;
            }
            SoundBank.NoteOnce("music-fallback", id, $"music '{id}' not found, using '{name}'");
        }

        var player = new AudioStreamPlayer { Bus = MusicBus, VolumeDb = SilentDb };
        var bed = new Bed { Id = id, Player = player };
        string kind;
        if (combat != null)
        {
            // one playback mixes both layers, so they can never drift apart
            var sync = new AudioStreamSynchronized { StreamCount = 2 };
            sync.SetSyncStream(0, SoundBank.Looped(main));
            sync.SetSyncStreamVolume(0, 0f);
            sync.SetSyncStream(1, SoundBank.Looped(combat));
            sync.SetSyncStreamVolume(1, SilentDb);
            bed.Layers = sync;
            bed.LayerDb = SilentDb;
            player.Stream = sync;
            kind = "base + combat layers in sync";
        }
        else
        {
            bed.Loops = SoundBank.ShouldLoop(path, name != "intro_bed");
            player.Stream = bed.Loops ? SoundBank.Looped(main) : SoundBank.OneShot(main);
            kind = bed.Loops ? "single looping track" : "single track, plays once";
        }
        AddChild(player);
        player.Play();
        Log.Info("audio", $"music '{id}' started ({kind})");
        return bed;
    }

    // ───────────────────────── ambience ─────────────────────────

    /// <summary>
    /// Crossfades to an ambience bed (res://assets/audio/amb/&lt;name&gt;.ogg; "town" and "amb_town" both work).
    /// Null, "" or "none" fades the ambience out.
    /// </summary>
    public void PlayAmbience(string name, float fade = 2f)
    {
        name = Norm(name);
        if (name == _ambId) return;
        _ambId = name;
        _ambFade = float.IsNaN(fade) ? 2f : Mathf.Max(0.02f, fade);
        _ambDirty = true;
        Log.Info("audio", $"ambience → {name ?? "(silence)"} over {_ambFade:0.0}s");
    }

    private void TickAmbience(float dt)
    {
        if (_ambDirty)
        {
            _ambDirty = false;
            SwitchBed(ref _amb, _ambOut, _ambId, _ambFade, false);
        }
        TickBeds(ref _amb, _ambOut, ref _ambId, dt, "ambience");
    }

    private Bed BuildAmbience(string id)
    {
        const string dir = SoundBank.AudioRoot + "amb/";
        string core = id.StartsWith("amb_", StringComparison.Ordinal) ? id.Substring(4) : id;
        var s = SoundBank.LoadStream(dir + id + ".ogg");
        if (s == null && core != id) s = SoundBank.LoadStream(dir + core + ".ogg");
        if (s == null && core == id) s = SoundBank.LoadStream(dir + "amb_" + core + ".ogg");
        if (s == null)
        {
            int cut = core.IndexOfAny(SplitChars);
            if (cut > 0)
            {
                string region = core.Substring(0, cut);
                s = SoundBank.LoadStream(dir + "amb_" + region + ".ogg") ?? SoundBank.LoadStream(dir + region + ".ogg");
                if (s != null) SoundBank.NoteOnce("ambience-fallback", id, $"ambience '{id}' not found, using '{region}'");
            }
        }
        if (s == null)
        {
            SoundBank.MissingOnce("ambience", id);
            return null;
        }

        var player = new AudioStreamPlayer { Bus = AmbBus, VolumeDb = SilentDb, Stream = SoundBank.Looped(s) };
        AddChild(player);
        // open somewhere inside the bed so an area does not always greet the hero with the same gust of wind
        double len = s.GetLength();
        player.Play(len > 6.0 ? (float)(_rng.NextDouble() * (len - 3.0)) : 0f);
        Log.Info("audio", $"ambience '{id}' started");
        return new Bed { Id = id, Player = player };
    }

    // ───────────────────────── beds (shared by music and ambience) ─────────────────────────

    private void SwitchBed(ref Bed current, List<Bed> outgoing, string id, float fade, bool music)
    {
        // a bed that is still fading out is simply brought back
        Bed next = null;
        if (id != null)
        {
            for (int i = 0; i < outgoing.Count; i++)
                if (outgoing[i].Id == id) { next = outgoing[i]; outgoing.RemoveAt(i); break; }
        }
        if (current != null)
        {
            current.Target = 0f;
            current.Secs = fade;
            outgoing.Add(current);
            current = null;
        }
        if (id == null) return;
        next ??= music ? BuildMusic(id) : BuildAmbience(id);
        if (next == null) return;
        next.Target = 1f;
        next.Secs = fade;
        current = next;
    }

    private void TickBeds(ref Bed current, List<Bed> outgoing, ref string currentId, float dt, string what)
    {
        if (current != null)
        {
            FadeBed(current, dt);
            if (!current.Loops)
            {
                current.Clock += dt;
                if (current.Clock > 0.5f && !current.Player.Playing)
                {
                    Log.Info("audio", $"{what} '{current.Id}' finished");
                    FreeBed(current);
                    current = null;
                    currentId = null;
                }
            }
        }
        for (int i = outgoing.Count - 1; i >= 0; i--)
        {
            if (!FadeBed(outgoing[i], dt)) continue;
            FreeBed(outgoing[i]);
            outgoing.RemoveAt(i);
        }
    }

    /// <summary>Advances a bed's equal-power fade. True once it has faded out completely.</summary>
    private static bool FadeBed(Bed b, float dt)
    {
        b.Fade = Mathf.MoveToward(b.Fade, b.Target, dt / Mathf.Max(b.Secs, 0.02f));
        float g = Mathf.Sin(b.Fade * Mathf.Pi * 0.5f);
        float db = g <= 0.0001f ? SilentDb : Mathf.LinearToDb(g);
        if (Mathf.Abs(db - b.Db) > 0.05f)
        {
            b.Db = db;
            b.Player.VolumeDb = db;
        }
        return b.Target <= 0f && b.Fade <= 0f;
    }

    private static void FreeBed(Bed b)
    {
        if (b.Player == null || !IsInstanceValid(b.Player)) return;
        b.Player.Stop();
        b.Player.QueueFree();
        b.Player = null;
    }

    // ───────────────────────── voice ─────────────────────────

    /// <summary>
    /// Plays res://assets/audio/vo/&lt;voiceId&gt;.ogg (or .wav) on the Voice bus, lowering music and ambience while it
    /// speaks, and calls <paramref name="onDone"/> when the line ends. When there is no recording the callback still
    /// comes, after a short stand-in delay, so dialogue driven by it keeps moving (the caller shows the subtitle).
    /// A new line, or <see cref="StopVoice"/>, cuts the current one off without calling its callback — except that a
    /// line nobody waits on (no callback: a bark) never cuts off a line somebody does wait on (a dialogue line); the
    /// bark is skipped instead, so a conversation cannot lose its callback to a stray remark.
    /// </summary>
    public void PlayVoice(string voiceId, Action onDone = null)
    {
        if (_voiceActive && _voiceCb != null && onDone == null) return;
        EndVoice();
        _voiceId = voiceId;
        _voiceCb = onDone;
        _voiceClock = 0f;
        _voiceTimed = true;
        _voiceLeft = SoundBank.DefaultVoiceSeconds;
        _voiceActive = true;
        try
        {
            AudioStream s = null;
            if (!string.IsNullOrEmpty(voiceId))
            {
                const string dir = SoundBank.AudioRoot + "vo/";
                s = SoundBank.LoadStream(dir + voiceId + ".ogg") ?? SoundBank.LoadStream(dir + voiceId + ".wav");
                // recordings arrive late in production: a missing line is noted once, not raised as a warning
                if (s == null) SoundBank.NoteOnce("voice", voiceId, $"voice '{voiceId}' has no recording, using a timed stand-in");
            }
            if (s != null && _built && _inTree)
            {
                _voice.Stream = SoundBank.OneShot(s);
                _voice.VolumeDb = 0f;
                _voice.PitchScale = 1f;
                _voice.Play();
                _voiceTimed = false;
            }
            else _voiceLeft = SoundBank.VoiceFallbackSeconds(voiceId);
        }
        catch (Exception e)
        {
            _voiceTimed = true;
            Log.Error("audio", $"voice '{voiceId}' failed, continuing without it", e);
        }
    }

    /// <summary>Cuts the current voice line. Its callback is not called.</summary>
    public void StopVoice() => EndVoice();

    private void EndVoice()
    {
        if (!_voiceActive) return;
        try
        {
            if (!_voiceTimed && _voice != null && _voice.Playing) _voice.Stop();
        }
        catch (Exception e) { Log.Error("audio", "stopping the voice line failed", e); }
        _voiceActive = false;
        _voiceTimed = false;
        _voiceCb = null;
        _voiceId = null;
    }

    private void TickVoice(float dt)
    {
        if (!_voiceActive) return;
        _voiceClock += dt;
        bool done = _voiceTimed ? _voiceClock >= _voiceLeft : _voiceClock > 0.15f && !_voice.Playing;
        if (!done) return;
        var cb = _voiceCb;
        _voiceActive = false;
        _voiceTimed = false;
        _voiceCb = null;
        _voiceId = null;
        if (cb != null) Log.Guard("audio", cb);      // may start the next line
    }

    private void TickDuck(float dt)
    {
        float target = _voiceActive && !_voiceTimed ? 1f : 0f;
        if (_duck == target) return;
        _duck = Mathf.MoveToward(_duck, target, dt / (target > _duck ? DuckAttack : DuckRelease));
        ApplyBusVolumes();
    }
}

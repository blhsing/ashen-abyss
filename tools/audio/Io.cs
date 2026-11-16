// File IO: 16-bit PCM WAV (with optional 'smpl' loop chunk), OGG Vorbis encode (OggVorbisEncoder) and decode (NVorbis, for verification).
using OggVorbisEncoder;

namespace AudioTool;

public static class Io
{
    /// <summary>Writes 16-bit PCM WAV with TPDF dither (deterministic). loop=true adds a 'smpl' chunk looping the whole file.</summary>
    public static void WriteWav(string path, float[] l, float[] r, bool loop, ulong ditherSeed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        int ch = r == null ? 1 : 2, n = l.Length; var rng = new Rng(ditherSeed);
        int dataBytes = n * ch * 2, smplBytes = loop ? 8 + 36 + 24 : 0;
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        w.Write("RIFF"u8); w.Write(4 + 24 + 8 + dataBytes + smplBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(A.SR); w.Write(A.SR * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes);
        var bytes = new byte[dataBytes]; int p = 0;
        for (int i = 0; i < n; i++)
            for (int c = 0; c < ch; c++)
            {
                double v = (c == 0 ? l[i] : r[i]) * 32767.0 + (rng.U() - rng.U());   // TPDF, 1 LSB peak-to-peak each side
                int q = (int)Math.Round(v); if (q > 32767) q = 32767; if (q < -32768) q = -32768;
                bytes[p++] = (byte)q; bytes[p++] = (byte)(q >> 8);
            }
        w.Write(bytes);
        if (loop)
        {
            w.Write("smpl"u8); w.Write(36 + 24);
            w.Write(0); w.Write(0); w.Write(1000000000 / A.SR); w.Write(60); w.Write(0); w.Write(0); w.Write(0); w.Write(1); w.Write(0);
            w.Write(0); w.Write(0); w.Write(0); w.Write(n - 1); w.Write(0); w.Write(0);   // cue id, type=forward, start, end, fraction, play count (0 = infinite)
        }
    }

    public static (float[] l, float[] r, bool hasLoop) ReadWav(string path)
    {
        var b = File.ReadAllBytes(path); int pos = 12; int ch = 1, bits = 16; float[] l = null, r = null; bool loop = false;
        while (pos + 8 <= b.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(b, pos, 4); int sz = BitConverter.ToInt32(b, pos + 4); int body = pos + 8;
            if (id == "fmt ") { ch = BitConverter.ToInt16(b, body + 2); bits = BitConverter.ToInt16(b, body + 14); }
            else if (id == "data")
            {
                int n = sz / (ch * (bits / 8)); l = new float[n]; if (ch == 2) r = new float[n];
                for (int i = 0; i < n; i++) { l[i] = BitConverter.ToInt16(b, body + i * ch * 2) / 32768f; if (ch == 2) r[i] = BitConverter.ToInt16(b, body + i * 4 + 2) / 32768f; }
            }
            else if (id == "smpl") loop = BitConverter.ToInt32(b, body + 28) > 0;
            pos = body + sz + (sz & 1);
        }
        return (l, r, loop);
    }

    /// <summary>Encodes stereo float to OGG Vorbis VBR so that the stream decodes to EXACTLY s.Len samples.
    /// The managed encoder ends its stream one long-block half (1024 samples) early, which would cut the end of every loop and
    /// put a click on the seam. So the input is padded past the end (for loops with the start of the loop, i.e. the real
    /// continuation, so the last block is coded against what actually follows it) and the pad is adjusted until the final Ogg
    /// page's granule position equals the wanted length. Decoders trim their output to that granule position.</summary>
    public static void WriteOgg(string path, St s, float quality, string title, bool loop)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        int n = s.Len, extra = 1024; long gran = -1;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var src = new St(n + extra); Array.Copy(s.L, src.L, n); Array.Copy(s.R, src.R, n);
            if (loop) for (int i = 0; i < extra; i++) { src.L[n + i] = s.L[i % n]; src.R[n + i] = s.R[i % n]; }
            using var ms = new MemoryStream();
            Encode(ms, src, quality, title);
            var bytes = ms.ToArray(); gran = LastGranule(bytes);
            if (gran == n) { File.WriteAllBytes(path, bytes); return; }
            extra = Math.Max(0, extra + (int)(n - gran));
        }
        throw new InvalidOperationException($"{title}: could not produce an OGG stream of exactly {n} samples (last granule position {gran})");
    }

    /// <summary>Granule position (= total decodable samples) of the last Ogg page in a stream.</summary>
    public static long LastGranule(byte[] b)
    {
        int i = 0, last = -1;
        while (i + 27 <= b.Length)
        {
            if (b[i] != 'O' || b[i + 1] != 'g' || b[i + 2] != 'g' || b[i + 3] != 'S') { i++; continue; }
            last = i; int segs = b[i + 26], len = 0; if (i + 27 + segs > b.Length) break;
            for (int k = 0; k < segs; k++) len += b[i + 27 + k];
            i += 27 + segs + len;
        }
        return last < 0 ? -1 : BitConverter.ToInt64(b, last + 6);
    }

    static void Encode(Stream output, St s, float quality, string title)
    {
        var info = VorbisInfo.InitVariableBitRate(2, A.SR, quality);
        var ogg = new OggStream((int)(Rng.Hash(title) & 0x7FFFFFFF));
        var comments = new Comments();
        comments.AddTag("TITLE", title); comments.AddTag("ALBUM", "Ashen Abyss OST"); comments.AddTag("ENCODER", "AshenAbyss AudioTool");
        ogg.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        ogg.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        ogg.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        Flush(ogg, output, true);
        var ps = ProcessingState.Create(info);
        var data = new[] { s.L, s.R }; const int Chunk = 4096; int n = s.Len;
        void Drain() { while (!ogg.Finished && ps.PacketOut(out OggPacket packet)) { ogg.PacketIn(packet); Flush(ogg, output, false); } }
        for (int at = 0; at < n; at += Chunk) { ps.WriteData(data, Math.Min(Chunk, n - at), at); Drain(); }
        ps.WriteEndOfStream(); Drain();
        Flush(ogg, output, true);
    }
    static void Flush(OggStream ogg, Stream output, bool force)
    {
        while (ogg.PageOut(out OggPage page, force)) { output.Write(page.Header, 0, page.Header.Length); output.Write(page.Body, 0, page.Body.Length); }
    }

    /// <summary>Samples the decoder produced for the last file read on this thread, before trimming to the end granule.</summary>
    [ThreadStatic] public static int LastRawDecoded;
    public static St ReadOgg(string path)
    {
        using var vr = new NVorbis.VorbisReader(path);
        int ch = vr.Channels; long total = vr.TotalSamples; var l = new List<float>((int)Math.Max(0, total)); var r = new List<float>((int)Math.Max(0, total));
        var buf = new float[ch * 8192]; int got;
        while ((got = vr.ReadSamples(buf, 0, buf.Length)) > 0)
            for (int i = 0; i < got; i += ch) { l.Add(buf[i]); r.Add(ch > 1 ? buf[i + 1] : buf[i]); }
        // The final Ogg page's granule position is the true length of the stream: libvorbis (and Godot, which decodes with it)
        // drops whatever the last packet holds beyond it. NVorbis does not always do so, so apply the same rule here.
        long gran = LastGranule(File.ReadAllBytes(path)); LastRawDecoded = l.Count;
        if (gran > 0 && l.Count > gran) { l.RemoveRange((int)gran, l.Count - (int)gran); r.RemoveRange((int)gran, r.Count - (int)gran); }
        return new St(l.ToArray(), r.ToArray());
    }
}

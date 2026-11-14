using Encoding = System.Text.Encoding;
using OggVorbisEncoder;

namespace MiniAudioExResearch;

internal static class Fixtures
{
    internal static void Generate(string directory)
    {
        Directory.CreateDirectory(directory);
        WriteWave(Path.Combine(directory, "constant.wav"), 48000, 1, Enumerable.Repeat(.25f, 48000).ToArray());
        WriteWave(Path.Combine(directory, "impulse.wav"), 48000, 1, [0, 0, .5f, 0, 0, 0, 0, 0]);
        float[] sfx = new float[12000];
        for (int i = 0; i < sfx.Length; i++)
            sfx[i] = (float)(.5 * Math.Sin(2 * Math.PI * 880 * i / 48000) * Math.Sin(Math.PI * i / sfx.Length));
        WriteWave(Path.Combine(directory, "эффект.wav"), 48000, 1, sfx);
        WriteOgg(Path.Combine(directory, "музыка-A.ogg"), 44100, 12, 0);
        WriteOgg(Path.Combine(directory, "музыка-B.ogg"), 44100, 12, 1);
        byte[] validOgg = File.ReadAllBytes(Path.Combine(directory, "музыка-A.ogg"));
        File.WriteAllBytes(Path.Combine(directory, "valid.ogg"), validOgg);
        File.WriteAllBytes(Path.Combine(directory, "truncated.ogg"), validOgg.AsSpan(0, 128).ToArray());
        validOgg[22] ^= 255; // Invalid CRC on the initial Ogg page.
        File.WriteAllBytes(Path.Combine(directory, "bad-crc.ogg"), validOgg);

        // Ten minutes / 115 MB on disk / 230 MB decoded float PCM; never construct the full PCM in memory.
        using (var output = File.Create(Path.Combine(directory, "long-music.wav")))
        using (var writer = new BinaryWriter(output))
        {
            const int rate = 48000, seconds = 600;
            WriteHeader(writer, rate, 2, rate * seconds * 2);
            byte[] halfSecond = new byte[rate / 2 * 2 * 2];
            for (int i = 0; i < rate / 2; i++)
            {
                short sample = (short)(6000 * Math.Sin(2 * Math.PI * 220 * i / rate));
                BitConverter.TryWriteBytes(halfSecond.AsSpan(i * 4), sample);
                BitConverter.TryWriteBytes(halfSecond.AsSpan(i * 4 + 2), sample);
            }
            for (int i = 0; i < seconds * 2; i++) writer.Write(halfSecond);
        }
        File.WriteAllBytes(Path.Combine(directory, "corrupt.wav"), [1, 2, 3, 4, 5, 6, 7, 8]);
        File.WriteAllBytes(Path.Combine(directory, "corrupt.ogg"), [(byte)'O', (byte)'g', (byte)'g', (byte)'S', 0, 0, 0, 0]);
    }

    internal static void WriteWave(string path, int sampleRate, int channels, float[] pcm)
    {
        using var writer = new BinaryWriter(File.Create(path));
        WriteHeader(writer, sampleRate, channels, pcm.Length);
        foreach (float sample in pcm) writer.Write((short)Math.Clamp((int)Math.Round(sample * 32768), -32768, 32767));
    }

    private static void WriteHeader(BinaryWriter output, int rate, int channels, int samples)
    {
        output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(checked(36 + samples * 2));
        output.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16); output.Write((short)1);
        output.Write((short)channels); output.Write(rate); output.Write(rate * channels * 2);
        output.Write((short)(channels * 2)); output.Write((short)16);
        output.Write(Encoding.ASCII.GetBytes("data")); output.Write(samples * 2);
    }

    private static void WriteOgg(string path, int rate, int seconds, int variant)
    {
        using var output = File.Create(path);
        var info = VorbisInfo.InitVariableBitRate(2, rate, .35f);
        var stream = new OggStream(4711 + variant);
        var comments = new Comments();
        comments.AddTag("TITLE", "TrueMoon generated research fixture");
        stream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        stream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        stream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        Flush(stream, output, true);
        var state = ProcessingState.Create(info);
        float[][] pcm = [new float[1024], new float[1024]];
        double[] notes = variant == 0 ? [440, 523.251, 659.255, 783.991] : [349.228, 440, 523.251, 698.456];
        for (int start = 0; start < rate * seconds; start += 1024)
        {
            int length = Math.Min(1024, rate * seconds - start);
            for (int i = 0; i < length; i++)
            {
                double time = (start + i) / (double)rate;
                double local = time % .5;
                double envelope = Math.Min(local / .015, 1) * Math.Exp(-local * 3.5);
                double frequency = notes[(int)(time * 2) % notes.Length];
                float sample = (float)(envelope * (.27 * Math.Sin(2 * Math.PI * frequency * time) +
                                                  .07 * Math.Sin(2 * Math.PI * frequency * .5 * time)));
                pcm[0][i] = sample; pcm[1][i] = sample * .85f;
            }
            state.WriteData(pcm, length, 0);
            Drain();
        }
        state.WriteEndOfStream(); Drain(); Flush(stream, output, true);
        return;

        void Drain()
        {
            while (!stream.Finished && state.PacketOut(out OggPacket packet))
            { stream.PacketIn(packet); Flush(stream, output, false); }
        }
    }

    private static void Flush(OggStream stream, Stream output, bool force)
    {
        while (stream.PageOut(out OggPage page, force))
        { output.Write(page.Header); output.Write(page.Body); }
    }
}

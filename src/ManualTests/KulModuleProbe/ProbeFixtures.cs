using System.Text;

namespace TrueMoon.KulModuleProbe;

internal static class ProbeFixtures
{
    internal static void Generate(string directory)
    {
        Directory.CreateDirectory(directory);
        Wave(Path.Combine(directory, "constant.wav"), 48000, false);
        Wave(Path.Combine(directory, "эффект.wav"), 12000, true);
        string music = Path.Combine(directory, "музыка.ogg");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets/music.ogg"), music, true);
        var ogg = File.ReadAllBytes(music);
        File.WriteAllBytes(Path.Combine(directory, "truncated.ogg"), ogg[..128]);
        ogg[22] ^= 255; File.WriteAllBytes(Path.Combine(directory, "bad-crc.ogg"), ogg);
        File.WriteAllBytes(Path.Combine(directory, "corrupt.ogg"), [(byte)'O', (byte)'g', (byte)'g', (byte)'S', 0, 0, 0, 0]);
        // A streamed ten-minute stereo fixture, created without a whole-file PCM buffer.
        string longPath = Path.Combine(directory, "long.wav");
        if (!File.Exists(longPath))
        {
            using var writer = new BinaryWriter(File.Create(longPath));
            const int samples = 48000 * 600 * 2;
            Header(writer, samples, 2);
            byte[] block = new byte[48000 * 4];
            for (int i = 0; i < block.Length; i += 2) BitConverter.TryWriteBytes(block.AsSpan(i), (short)4096);
            for (int i = 0; i < 600; i++) writer.Write(block);
        }
    }
    private static void Wave(string path, int samples, bool effect)
    {
        using var writer = new BinaryWriter(File.Create(path)); Header(writer, samples, 1);
        for (int i = 0; i < samples; i++)
            writer.Write(effect ? (short)(10000 * Math.Sin(2 * Math.PI * 880 * i / 48000) * Math.Sin(Math.PI * i / samples)) : (short)8192);
    }
    private static void Header(BinaryWriter writer, int samples, int channels)
    {
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels);
        writer.Write(48000); writer.Write(48000 * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
    }
}

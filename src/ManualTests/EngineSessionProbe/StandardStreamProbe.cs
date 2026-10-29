using TrueMoon.Mithril.Units;

namespace EngineSessionProbe;

internal static class StandardStreamProbe
{
    internal static async Task<int> RunChildAsync(string mode, string directory)
    {
        Console.WriteLine("stdout-ready");
        Console.Error.WriteLine("stderr-ready");
        if (mode == "exit")
            while (!File.Exists(Path.Combine(directory, "exit"))) await Task.Delay(10);
        else if (mode == "kill") await Task.Delay(Timeout.InfiniteTimeSpan);
        else if (mode == "graceful")
            while (Console.ReadLine() is { } line && line != UnitCodes.Exit) { }
        else return 2;
        return 0;
    }
}

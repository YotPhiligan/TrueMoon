namespace TrueMoon.Nom.AssetTool;

/// <summary>Captured command arguments; parsing completes before App or store creation.</summary>
public sealed record ToolArguments(string Command, string Root, Guid ProjectId, Guid StoreId, string Receipt, string? BackupRoot, string Report)
{
    public static ToolArguments Parse(string[] args)
    {
        DemoReceipt.Require(args.Length >= 2 && args[^2] == "--report", "A trailing --report <absolute-json-path> is required.");
        var report = Absolute(args[^1]);
        DemoReceipt.Require(!File.Exists(report) && !Directory.Exists(report) && Directory.Exists(Path.GetDirectoryName(report)), "Report must be absent with an existing parent.");
        var command = args[0]; var count = command is "export" or "restore" ? 8 : 7;
        DemoReceipt.Require(command is "create" or "verify" or "export" or "restore" or "collect" or "verify-current", "Unknown command.");
        DemoReceipt.Require(args.Length == count, "Unexpected command argument count.");
        var root = Absolute(args[command == "restore" ? 2 : 1]);
        var project = Guid.Parse(args[command == "restore" ? 3 : 2]); var store = Guid.Parse(args[command == "restore" ? 4 : 3]);
        var receipt = Absolute(args[command == "restore" ? 5 : 4]);
        var backup = command == "export" ? Absolute(args[5]) : command == "restore" ? Absolute(args[1]) : null;
        DemoReceipt.Require(project != Guid.Empty && store != Guid.Empty && root != receipt && root != report && receipt != report, "Explicit distinct root/receipt/report and nonempty identities required.");
        if (command == "create") DemoReceipt.Require(!File.Exists(root) && !Directory.Exists(root) && !File.Exists(receipt) && !Directory.Exists(receipt) &&
            Directory.Exists(Path.GetDirectoryName(receipt)), "Create needs an absent root and receipt with an existing receipt parent.");
        else DemoReceipt.Require(File.Exists(receipt), "A receipt from create is required.");
        return new(command, root, project, store, receipt, backup, report);
    }
    public static string Absolute(string path)
    {
        DemoReceipt.Require(Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal), "Fully qualified local paths are required.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}

using TrueMoon.Editor;

try
{
    if (args.Contains("--help")) { Console.WriteLine("TrueMoon.Editor [--project game.csproj] [--assembly game.dll] [--entry game.type] [--light] [--validation] [--smoke fresh-directory]"); return 0; }
    if (args.Contains("--runtime")) await RuntimeEntryPoint.RunAsync();
    else await EditorApplication.RunAsync(args);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

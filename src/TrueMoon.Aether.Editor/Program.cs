using TrueMoon.Aether.Editor;

try
{
    if (args.Contains("--help")) { Console.WriteLine("TrueMoon.Aether.Editor [--project project.aetherproj] [--check-project project.aetherproj] [--light] [--validation]\nScene preflight: --project project.aetherproj --check-scene scene.aetherscene\nLegacy: --project game.csproj [--assembly game.dll] [--entry game.type] [--smoke fresh-directory]"); return 0; }
    if (args.Contains("--runtime")) await RuntimeEntryPoint.RunAsync();
    else
    {
        var options = EditorLaunchOptions.Parse(args);
        if (options.CheckScenePath != null) await ProjectEntryPoint.CheckSceneAsync(options.ProjectPath!, options.CheckScenePath);
        else if (options.CheckProjectPath != null) await ProjectEntryPoint.CheckAsync(options.CheckProjectPath);
        else await EditorApplication.RunAsync(options);
    }
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

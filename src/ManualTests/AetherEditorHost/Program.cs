using TrueMoon.ManualTests.AetherEditorHost;

try
{
    if (args.Contains("--help"))
    {
        Console.WriteLine("""
            AetherEditorHost [--sandbox[=false] | --project project.aetherproj] [--light] [--validation]
            Interactive default: a fresh sandbox with an empty scene. Use --sandbox=false to open without a project.
            Project preflight: --check-project project.aetherproj
            Scene preflight: --project project.aetherproj --check-scene scene.aetherscene
            Legacy: --project game.csproj [--assembly game.dll] [--entry game.type] [--smoke fresh-directory]
            """);
        return 0;
    }
    if (args.Contains("--runtime")) await RuntimeEntryPoint.RunAsync();
    else if (args.FirstOrDefault() == "--model-build-probe") await EditorModelBuildSmoke.RunAsync(args[1..]);
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

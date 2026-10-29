using ProcessOutputProbe;

try
{
    if (args is ["--starvation", var directory])
        return FileCaptureProbe.RunStarvation(Path.GetFullPath(directory));
    if (args is ["--files", var filesDirectory, var count])
        return await FileCaptureProbe.RunAsync(Path.GetFullPath(filesDirectory), int.Parse(count));
    if (args is ["--app-files", var appDirectory, var appCount])
        return await FileCaptureProbe.RunAsync(Path.GetFullPath(appDirectory), int.Parse(appCount), diagnostics: true);
    Console.Error.WriteLine("ProcessOutputProbe <--starvation directory|--files directory iterations|--app-files directory iterations>");
    return 2;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

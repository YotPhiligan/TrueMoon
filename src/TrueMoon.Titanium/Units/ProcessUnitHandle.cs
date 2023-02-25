using System.Collections.ObjectModel;
using System.Diagnostics;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;

namespace TrueMoon.Titanium.Units;

public abstract class ProcessUnitHandle : UnitHandleBase, IDisposable, IAsyncDisposable
{
    protected Process? Process;
    
    private bool _restartedOnce;

    protected ProcessUnitHandle(IUnitConfiguration configuration, IEventsSource source) : base(configuration, source)
    {

    }

    public override async Task<bool?> StartAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetProcessStartInfoAsync(cancellationToken);
        
        ProcessLaunchArguments(info.ArgumentList);
        
        Process ??= new Process();
        Process.StartInfo = info;
        Process.EnableRaisingEvents = true;
        Process.Exited += ProcessOnExited;

        return Process.Start();
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        EventsSource.Write(()=> $"{Configuration.Name} exited", "UnitProcessOnExited");
        
        OnProcessExited(Process?.ExitCode, Process?.ExitTime);
        if (ShouldRestart(Process?.ExitCode) is true)
        {
            Restart();
        }
        
        OnExitAction?.Invoke(this);
    }

    protected virtual void OnProcessExited(int? exitCode, DateTime? exitTime)
    {
        
    }
    
    protected virtual bool? ShouldRestart(int? exitCode)
    {
        return Configuration switch
        {
            {RestartPolicy:UnitRestartPolicy.Always} => true,
            {RestartPolicy:UnitRestartPolicy.Once} => !_restartedOnce,
            {RestartPolicy:UnitRestartPolicy.OnCrash} => exitCode != 0,
            {RestartPolicy:UnitRestartPolicy.Never} => false,
            _ => null
        };
    }

    protected virtual void Restart()
    {
        try
        {
            Process?.Start();
            _restartedOnce = true;
        }
        catch (Exception e)
        {
            throw;
        }
    }

    protected virtual async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo
        {
            FileName = GetProcessFilePath(),
            UseShellExecute = true
        };

        return info;
    }

    protected virtual string GetProcessFilePath() => Environment.ProcessPath ?? throw new InvalidOperationException("Path is not available");

    protected virtual void ProcessLaunchArguments(Collection<string> collection)
    {
        collection.Add($"{UnitsConfigurationExtensions.UnitIdArg}={Configuration.Index}");
        collection.Add($"{UnitsConfigurationExtensions.UnitParentIdArg}={Environment.ProcessId}");
    }

    public virtual void Dispose()
    {
        Process?.Dispose();
    }

    public virtual ValueTask DisposeAsync()
    {
        Process?.Dispose();
        return ValueTask.CompletedTask;
    }
}
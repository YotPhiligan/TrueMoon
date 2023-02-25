using System.Diagnostics;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;

namespace TrueMoon.Titanium;

public class UnitParentProcessEventsHandler : IStartable, IDisposable
{
    private readonly IAppLifetime _appLifetime;
    private readonly IEventsSource<UnitParentProcessEventsHandler> _eventsSource;
    private readonly int? _i;
    private Process _process;
    private Timer _timer;

    public UnitParentProcessEventsHandler(IConfiguration configuration, 
        IAppLifetime appLifetime, 
        IEventsSource<UnitParentProcessEventsHandler> eventsSource)
    {
        _appLifetime = appLifetime;
        _eventsSource = eventsSource;
        _i = configuration.GetProcessingUnitParentId();
    }

    public void StartListen(int parentProcessId)
    {
        var id = parentProcessId;
        void Close()
        {
            _eventsSource.Write(() => "parent process exit");
            _appLifetime.Cancel();
        }

        try
        {
            _process = Process.GetProcessById(id);
            _process.EnableRaisingEvents = true;
            _process.Exited += (sender, args) => Close();
            
            _timer = new Timer(state =>
            {
                try
                {
                    _process.Refresh();
                }
                catch (Exception e)
                {
                    Close();
                }
            }, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        }
        catch (Exception e)
        {
            _appLifetime.Cancel();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartListen(_i ?? throw new InvalidOperationException("Invalid parent process id"));
        return Task.CompletedTask;
    }

    private bool _isDisposed;
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _timer?.Dispose();
        _process?.Dispose();
    }
}
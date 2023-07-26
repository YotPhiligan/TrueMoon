using System.Diagnostics;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Titanium.Units;

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

    public void StartListen(int parentProcessId, CancellationToken cancellationToken = default)
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

            _ = Task.Factory.StartNew(()=>ListenCodes(cancellationToken),cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch (Exception e)
        {
            _appLifetime.Cancel();
        }
    }

    private async Task ListenCodes(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var code = await Console.In.ReadLineAsync(cancellationToken);

                switch (code)
                {
                    case UnitCodes.Exit:
                        _eventsSource.Write(() => "exit requested");
                        _appLifetime.Cancel();
                        return;
                }
            }
        }
        catch (OperationCanceledException) {}
        catch (Exception e)
        {
            _eventsSource.Exception(e);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartListen(_i ?? throw new InvalidOperationException("Invalid parent process id"), cancellationToken);
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
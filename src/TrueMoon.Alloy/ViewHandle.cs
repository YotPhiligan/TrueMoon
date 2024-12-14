using Silk.NET.Input;
using Silk.NET.Windowing;
using TrueMoon.Diagnostics;
using TrueMoon.Threading;

namespace TrueMoon.Alloy;

public class ViewHandle : IViewHandle
{
    private readonly IEventsSource _eventsSource;
    private readonly IGraphicsPlatform _graphicsPlatform;
    private readonly IViewPresenter _viewPresenter;
    private readonly IVisualTreeBuilder _visualTreeBuilder;
    private readonly IAppLifetime _appLifetime;
    private IWindow? _nativeWindow;
    private IInputContext _input;
    private TmTaskScheduler _scheduler;

    public ViewHandle(IEventsSource eventsSource, 
        IGraphicsPlatform graphicsPlatform,
        IViewPresenter viewPresenter,
        IVisualTreeBuilder visualTreeBuilder,
        IAppLifetime appLifetime)
    {
        _eventsSource = eventsSource;
        _graphicsPlatform = graphicsPlatform;
        _viewPresenter = viewPresenter;
        _visualTreeBuilder = visualTreeBuilder;
        _appLifetime = appLifetime;
    }
    
    private void RunCore()
    {
        VisualTree = _visualTreeBuilder.Build(View);
        
        _graphicsPlatform.Initialize();
        
        _nativeWindow = _graphicsPlatform.GetNativeWindow();
        
        _nativeWindow.Load += () =>
        {
            _input = _nativeWindow.CreateInput();
            _viewPresenter.Initialize(_graphicsPlatform);
            
            _nativeWindow.Center();
        };

        _nativeWindow.Render += d =>
        {
            _viewPresenter.Present(d, VisualTree);
            
            //_nativeWindow.SwapBuffers();
        };
        
        _nativeWindow.Closing += () =>
        {
            _input.Dispose();
            _viewPresenter.Release();
        };

        _nativeWindow.Resize += v =>
        {
            _nativeWindow.DoRender();
            _viewPresenter.Resize(v.X, v.Y);
        };

        _nativeWindow.Move += _ =>
        {
            _nativeWindow.DoUpdate();
            _nativeWindow.DoRender();
        };

        _nativeWindow.StateChanged += state =>
        {
            
        };

        _nativeWindow.FocusChanged += b =>
        {
            _nativeWindow.FramesPerSecond = b ? 60 : 30;
        };
        
        _nativeWindow.Run();
        
        _eventsSource.Write(() => "Closed");
        
        Closed?.Invoke();
    }
    
    public void Run()
    {
        if (_nativeWindow is { IsInitialized: true })
        {
            _nativeWindow.IsVisible = true;
            return;
        }

        _scheduler = new TmTaskScheduler("window",1);
        Task.Factory.StartNew(RunCore, _appLifetime.AppCancellationToken, TaskCreationOptions.LongRunning, _scheduler);
    }

    public void Close()
    {
        _nativeWindow?.Close();
    }
    
    public void Dispose()
    {
        _graphicsPlatform?.Dispose();
        _scheduler?.Dispose();
    }

    public event Action Closed;

    public void Attach(TrueMoon.Aluminum.IView? view)
    {
        View = view;
    }
    
    public TrueMoon.Aluminum.IView? View { get; private set; }
    public IVisualTree VisualTree { get; private set; }
}
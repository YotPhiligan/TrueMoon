using TrueMoon.Argentis.Styling;
using System.Text.Json;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using System.Security.Cryptography;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Aether.Kton;

namespace TrueMoon.ManualTests.KtonVisual;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,IncludeFields=true};
    private static async Task<int> Main(string[] args)
    {
        if(args.Length!=2||args[0] is not("cpu" or "verify" or "play" or "cpu-msdi" or "verify-msdi" or "play-msdi"))
        {Console.Error.WriteLine("KtonVisualProbe cpu|verify|play[-msdi] <new-evidence-directory>");return 2;}
        var microsoft=args[0].EndsWith("-msdi",StringComparison.Ordinal);var mode=args[0].Split('-')[0];
        var report=Path.GetFullPath(args[1]);
        if(Directory.Exists(report))throw new IOException("Use a new evidence directory.");
        Directory.CreateDirectory(report);
        try
        {
            if(mode=="cpu")return await Verification.Cpu(report,microsoft);
            return await VisualHost.Run(report,mode=="verify",microsoft);
        }
        catch(Exception error){await File.WriteAllTextAsync(Path.Combine(report,"error.txt"),error.ToString());Console.Error.WriteLine(error);return 1;}
    }
    internal static IApp Build(bool microsoft,Action<IAppConfigurationContext> configure)=>
        App.Builder(context=>{if(microsoft)context.UseDI();}).Setup(configure).Build();
    internal static object[] Assemblies()=>AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly=>!assembly.IsDynamic&&assembly.GetName().Name is {} name&&(name.StartsWith("TrueMoon",StringComparison.Ordinal)||name is "BepuPhysics" or "BepuUtilities" or "SkiaSharp"))
        .Select(assembly=>(object)new{Name=assembly.GetName().Name,Path=assembly.Location,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))}).ToArray();
    internal static void Configure(IAppConfigurationContext context)
    {
        context.UseBepuPhysics(options=>options.DefaultWorld=new(){Gravity=new(0,-9.81f,0)});
        context.UseAetherEcs().UseAetherPrimitiveEcs();
        context.UseAetherSceneLoading(_=>PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
        context.UseAetherKton();context.UseAetherSceneSubsystems(builder=>builder.AddKtonPhysics());
        context.Services(services=>
        {
            services.Singleton<IFactory<PrimitiveGameSession>>(resolver=>
            {
                var loader=resolver.Resolve<SceneWorldLoader>()!;var simulations=resolver.Resolve<IFactory<PrimitiveSimulation,PrimitiveSimulationRequest>>()!;
                return new Factory<PrimitiveGameSession>(()=>new(loader,simulations));
            });
            services.Singleton<IFactory<VisualSession>>(resolver=>
            {var games=resolver.Resolve<IFactory<PrimitiveGameSession>>()!;return new Factory<VisualSession>(()=>new(games));});
            services.Singleton<IFactory<FrameSource,VisualSession>>(_=>new Factory<FrameSource,VisualSession>(session=>new(session)));
            services.Singleton<Controls>(_=>new());services.Singleton<VisualData>(_=>new());
            services.Singleton<IViewStyle<VisualView>>(_=>new VisualStyle());
            services.Singleton<IFactory<VisualView>>(resolver=>
            {var data=resolver.Resolve<VisualData>();var input=resolver.Resolve<Controls>();var style=resolver.Resolve<IViewStyle<VisualView>>();return new Factory<VisualView>(()=>new(data,input,style));});
        });
    }
    internal static void ConfigureWindow(IAppConfigurationContext context)
    {
        var fonts=SkiaFontCatalog.Load("Noto Sans",new Dictionary<string,string>{{"Noto Sans",Path.Combine(AppContext.BaseDirectory,"Assets/Fonts/NotoSans-Regular.ttf")}});
        context.UsePresentationFactory<VisualView>(options=>
        {
            options.ConfigurePresenters(VisualPresenters.Configure).UseSkiaOpenGL(fonts);options.UseSilkWindow(window=>{window.Width=1000;window.Height=690;window.Title="Kton visual acceptance";});
        });
        context.Services(services=>
        {
            services.Singleton<GpuResourceHost>(_=>new());
            services.Singleton<IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest>>(resolver=>
                new Factory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest>(resolver.Resolve<GpuResourceHost>().CreateLocalSurface));
            services.Singleton<IFactory<VisualHost,VisualHostArguments>>(resolver=>
            {
                var window=resolver.Resolve<HostedUiWindow>()!;var gpu=resolver.Resolve<GpuResourceHost>()!;
                var surfaces=resolver.Resolve<IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest>>()!;
                var data=resolver.Resolve<VisualData>()!;var controls=resolver.Resolve<Controls>()!;
                return new Factory<VisualHost,VisualHostArguments>(arguments=>new(window,arguments.Session,arguments.Source,gpu,surfaces,data,controls,arguments.Directory,arguments.Verify));
            });
        });
    }
}

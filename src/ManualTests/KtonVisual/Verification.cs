using TrueMoon.Argentis.Input;
using System.Numerics;
using System.Text.Json;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Transforms;
using TrueMoon.Dependencies;
using TrueMoon.Kton.Bepu.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using SkiaSharp;

namespace TrueMoon.ManualTests.KtonVisual;

internal static class Verification
{
    internal static void Check(bool condition,string message)
    {if(!condition)throw new InvalidDataException(message);}
    internal static async Task<int> Cpu(string directory,bool microsoft)
    {
        var checks=new List<string>();
        await using var app=Program.Build(microsoft,Program.Configure);
        var physics=app.Services.Resolve<BepuPhysicsHost>()!;
        Check(physics.Resources.PublishedWorlds==0,"Build opened a physics world.");
        await app.StartAsync();
        RoutedUi(app,directory,checks);
        using var session=app.Services.Resolve<IFactory<VisualSession>>()!.Create()!;
        session.Reset();checks.Add("App factory and private session activation");
        for(var i=0;i<90;i++)session.Advance(Vector2.Zero);
        Check(session.CharacterState.IsSupported&&Math.Abs(session.Position(Level.Character).Y-1)<.05f,"Character did not settle on the floor.");
        checks.Add("Capsule settles on flat support");
        session.SetPlaying(true);var slope=false;var platformBefore=session.Position(Level.Platform);
        for(var i=0;i<205;i++)
        {
            platformBefore=session.Position(Level.Platform);
            session.Advance(new(2,0));
            slope|=session.CharacterState.IsSupported&&Math.Abs(session.CharacterState.SupportNormal.X)>.1f;
        }
        Check(slope&&session.Position(Level.Character).Y>1.5f,$"Controller did not climb the slope: {session.Position(Level.Character)}, {session.CharacterState}, slope={slope}.");
        checks.Add("Slope support and ascent");
        var tick=session.Tick;var previous=session.Sample(Level.Platform,0);var current=session.Sample(Level.Platform,1);
        var middle=session.Sample(Level.Platform,.5f);
        Check(Vector3.Distance(previous.Position,platformBefore)<1e-5f&&Vector3.Distance(current.Position,session.Position(Level.Platform))<1e-5f,"Interpolation endpoints are not adjacent completed physics ticks.");
        Check(Vector3.Distance(middle.Position,(previous.Position+current.Position)*.5f)<1e-5f,"Interpolation midpoint mismatch.");
        var frame=session.Render(1);var middleFrame=session.Render(.5f);
        for(var i=0;i<Level.Actors.Length;i++)
        {
            if(i==Level.Character)continue;
            var index=i<Level.Character?i:i+15;
            Check(Vector3.Distance(frame.Primitives[index].World.Translation,session.Position(i))<1e-5f,"Rendered body centre differs from physics.");
        }
        var capsuleCentre=Vector3.Zero;for(var i=2;i<18;i++)capsuleCentre+=frame.Primitives[i].World.Translation/16;
        Check(Vector3.Distance(capsuleCentre,session.Position(Level.Character))<1e-5f,"Capsule proxy centre differs from physics.");
        Check(Vector3.Distance(middleFrame.Primitives[Level.Platform+15].World.Translation,middle.Position)<1e-5f,"Rendered platform ignored interpolation.");
        checks.Add("Rendered body/proxy centres equal physics and fractional platform pose");
        var immutable=WorldHash(session);session.Render(.25f).Validate();session.Render(.75f).Validate();
        Check(tick==session.Tick&&WorldHash(session)==immutable,"Rendering changed physics World.");
        checks.Add("Fractional rendering preserves fixed World and tick");
        session.SetPlaying(false);
        Check(session.Sample(Level.Platform,0)==session.Sample(Level.Platform,1),"Pause must display the completed pose.");
        session.Advance(Vector2.Zero);Check(session.Tick==tick+1,"Paused Step must advance one tick.");
        checks.Add("Pause snaps and Step advances exactly once");
        session.Teleport(new(-6,1.05f,3));
        Check(session.Sample(Level.Character,0)==session.Sample(Level.Character,1),"Teleport retained an interpolation trail.");
        for(var i=0;i<30;i++)session.Advance(Vector2.Zero);
        session.Advance(Vector2.Zero,true);for(var i=0;i<8;i++)session.Advance(Vector2.Zero);
        Check(!session.CharacterState.IsSupported&&session.Position(Level.Character).Y>1.3f,"Jump did not leave support.");
        for(var i=0;i<90;i++)session.Advance(Vector2.Zero);
        Check(session.CharacterState.IsSupported,"Jump did not land.");checks.Add("Jump, airborne and landing");
        session.Teleport(new(-6,1.05f,5));for(var i=0;i<30;i++)session.Advance(Vector2.Zero);
        var peak=1f;session.Advance(Vector2.Zero,true);
        for(var i=0;i<60;i++){session.Advance(Vector2.Zero);peak=MathF.Max(peak,session.Position(Level.Character).Y);}
        Check(peak>1.25f&&peak<1.7f&&session.CharacterState.IsSupported,"Low ceiling did not limit the jump and permit landing.");
        checks.Add("Low ceiling blocks ascent without losing floor support after landing");
        session.Teleport(session.Position(Level.Platform)+new Vector3(0,1.25f,0));
        for(var i=0;i<120;i++)session.Advance(Vector2.Zero);
        Check(session.CharacterState.IsSupported&&session.CharacterState.SupportEntity==session.Entity(Level.Platform),"Moving platform support lost.");
        Check(Math.Abs(session.Position(Level.Character).Z-session.Position(Level.Platform).Z)<.2f,"Platform did not carry the controller.");
        Check(session.LastContacts>0&&session.PriorTickContacts>0,"No contact entities observed.");
        checks.Add("Platform carry, body/entity mapping and next-tick contacts");
        var old=session.World;session.Reset();Check(old.IsDisposed&&session.Tick==0&&physics.Resources.PublishedWorlds==1,"Reset retained the retired World.");
        checks.Add("Reset starts a fresh World and interpolation history");
        session.Stop();session.Stop();Check(physics.Resources.PublishedWorlds==0&&physics.Resources.ActiveResourceOwners==0,"Stop retained physics ownership.");
        checks.Add("Idempotent Stop releases the physics World");
        await app.StopAsync();
        await File.WriteAllTextAsync(Path.Combine(directory,"verification.json"),JsonSerializer.Serialize(new{Completed=true,Stage="PHYS8.4",Mode="CPU",Provider=microsoft?"Microsoft":"Cobalt",Checks=checks,Count=checks.Count,NativeWindow=false,Gpu=false,Resources=physics.Resources,Assemblies=Program.Assemblies()},Program.Json));
        return 0;
    }
    private static void RoutedUi(IApp app,string directory,List<string> checks)
    {
        var root=app.Services.Resolve<IFactory<VisualView>>()!.Create()!;var controls=app.Services.Resolve<Controls>()!;
        var fonts=SkiaFontCatalog.Load("Noto Sans",new Dictionary<string,string>{{"Noto Sans",Path.Combine(AppContext.BaseDirectory,"Assets/Fonts/NotoSans-Regular.ttf")}});
        using var ui=UiSession.Create(root,new SkiaRasterRenderBackend(fonts),new RasterUiTarget(),new UiViewport(1000,690,1), null, new UiDispatchOptions(), configurePresenters: VisualPresenters.Configure);
        ui.Update();var bounds=root.Canvas.Bounds;
        ui.HandleInput(new UiInput(InputKind.PointerDown,bounds.X+10,bounds.Y+10));
        ui.HandleInput(new UiInput(InputKind.PointerUp,bounds.X+10,bounds.Y+10));
        ui.HandleInput(new UiInput(InputKind.KeyDown,Key:UiKey.D));
        Check(root.Canvas.IsFocused&&controls.Take().Move.X==2.5f,"Routed WASD did not reach the focused canvas.");
        ui.HandleInput(new UiInput(InputKind.KeyDown,Key:UiKey.Enter));ui.HandleInput(new UiInput(InputKind.KeyDown,Key:UiKey.Enter));
        var command=controls.Take();Check(command.Commands.Length==1&&command.Commands[0]==VisualCommand.Toggle,"Key repeat toggled twice.");
        controls.ResetMotion();ui.HandleInput(new UiInput(InputKind.KeyDown,Key:UiKey.Enter));
        Check(controls.Take().Commands.Length==0,"Mode change forgot the held command key.");
        ui.HandleInput(new UiInput(InputKind.KeyUp,Key:UiKey.Enter));
        ui.HandleInput(new UiInput(InputKind.KeyDown,Key:UiKey.Space));
        Check(controls.Take().Jump&&!controls.Take().Jump,"Jump edge was not consumed once.");
        ui.HandleInput(new UiInput(InputKind.FocusLost));
        Check(controls.Take().Move==Vector2.Zero&&!root.Canvas.IsFocused,"Focus loss retained movement.");
        ui.Update();using var image=ui.SnapshotRasterImage();using var png=image.Encode(SKEncodedImageFormat.Png,100);
        using var file=File.Create(Path.Combine(directory,"controls-raster.png"));png.SaveTo(file);
        checks.Add("Real UiSession focus, routed WASD, repeat coalescing, jump edge and focus-loss release");
    }
    internal static string WorldHash(VisualSession session)=>string.Join(";",Enumerable.Range(0,Level.Actors.Length).Select(i=>
    {
        var pose=session.World.GetComponent<TrueMoon.Aether.Ecs.Transforms.LocalTransform>(session.Entity(i));
        return $"{i}:{pose.Position}:{pose.Rotation}";
    }));
}

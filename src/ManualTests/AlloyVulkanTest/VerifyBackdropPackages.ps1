[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ValidationDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$RunDirectory
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$run=if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-BackdropPackages-'+[guid]::NewGuid().ToString('N'))}
if($run-eq$workspace-or$run.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Consumer must be outside checkout.'}
if((Test-Path $run)-or(Test-Path $evidence)){throw 'Use fresh run/evidence directories.'}
$validation=(Resolve-Path -LiteralPath $ValidationDirectory).Path
if(!(Test-Path "$validation/VkLayer_khronos_validation.json")){throw 'Validation manifest missing.'}
New-Item -ItemType Directory -Path $run,$evidence,"$run/feed","$run/cache","$run/consumer"|Out-Null
$version='0.2.2.7-backdrop.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$commands=[Collections.Generic.List[object]]::new()
function Invoke-Dotnet([string]$name,[string[]]$arguments){
    $info=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet.exe).Source)
    $info.WorkingDirectory=$run;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $arguments){[void]$info.ArgumentList.Add($argument)}
    $info.Environment['VK_LAYER_PATH']=$validation
    if($name-notlike'pack-*'){$info.Environment['NUGET_PACKAGES']="$run/cache"}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$info
    try{
        [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(240000)){throw "Timeout $name"}
        [IO.File]::WriteAllText("$evidence/$name.log",$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult())
        $commands.Add([pscustomobject]@{Name=$name;Arguments=$arguments;ExitCode=$process.ExitCode})
        if($process.ExitCode-ne0){throw "Failed $name"}
    }finally{if(!$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
foreach($id in 'TrueMoon.Contracts','TrueMoon.Argentis','TrueMoon.Alloy','TrueMoon.Alloy.Rendering.Skia','TrueMoon.Alloy.Platform.Silk'){
    Invoke-Dotnet "pack-$id" @('pack',"$workspace/$id/$id.csproj",'-c','Release','-o',"$run/feed","-p:Version=$version",'--artifacts-path',"$evidence/pack-artifacts","-bl:$evidence/pack-$id-{}.binlog",'--verbosity','quiet')
}
$escapedFeed=[Security.SecurityElement]::Escape("$run/feed")
[IO.File]::WriteAllText("$run/NuGet.Config","<configuration><packageSources><clear/><add key='fresh' value='$escapedFeed'/><add key='nuget.org' value='https://api.nuget.org/v3/index.json'/></packageSources><packageSourceMapping><packageSource key='fresh'><package pattern='TrueMoon.*'/></packageSource><packageSource key='nuget.org'><package pattern='*'/></packageSource></packageSourceMapping></configuration>")
[IO.File]::WriteAllText("$run/consumer/Consumer.csproj",@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><RootNamespace>AlloyVulkanTest</RootNamespace></PropertyGroup><ItemGroup><PackageReference Include="TrueMoon.Alloy.Rendering.Skia" Version="$version"/><PackageReference Include="TrueMoon.Alloy.Platform.Silk" Version="$version"/></ItemGroup></Project>
"@)
Copy-Item -LiteralPath "$PSScriptRoot/VulkanHostReadback.cs" -Destination "$run/consumer/VulkanHostReadback.cs"
[IO.File]::WriteAllText("$run/consumer/Program.cs",@'
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon.Argentis;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Platform.Silk;
namespace AlloyVulkanTest;
internal static unsafe class Program
{
    private static void Main(string[] args)
    {
        SkiaNativeLibrary.Initialize();var cases=new List<object>();
        using var device=new VulkanDevice(validationMessage:Console.Error.WriteLine);
        var descriptor=new VulkanHostContext(device.Instance.Handle,device.PhysicalDevice.Handle,device.Device.Handle,device.Queue.Handle,
            device.QueueFamily,Vk.Version11,device.GetProcedureAddress,device.WaitIdle,device.InstanceExtensions,device.DeviceExtensions);
        foreach(var light in new[]{false,true})foreach(var scale in new[]{1f,1.5f,2f})
        {
            var root=new SplitBackground();var menu=new MenuPopup([new("apply","Применить",new DelegateUiCommand(()=>{})),new("cancel","Отменить",new DelegateUiCommand(()=>{}))],()=>{})
            {Width=220,Height=80,Margin=new(140,60,0,0),HorizontalAlignment=LayoutAlignment.Start,VerticalAlignment=LayoutAlignment.Start};root.Add(menu);
            var text=new TextBox{Value="rejected draft",Width=140,Height=32,HasError=true,ErrorMessage="Validator rejected the draft",Margin=new(20,200,0,0),HorizontalAlignment=LayoutAlignment.Start,VerticalAlignment=LayoutAlignment.Start};
            var number=new NumberBox("X"){Width=140,Height=32,HasError=true,ErrorMessage="Enter a finite number",Margin=new(180,200,0,0),HorizontalAlignment=LayoutAlignment.Start,VerticalAlignment=LayoutAlignment.Start};number.Editor.Value="-";
            var rich=new RichTextEditor{Text="Rejected text",Width=140,Height=70,MinWidth=0,MinHeight=0,HasError=true,ErrorMessage="Invalid text",Margin=new(340,200,0,0),HorizontalAlignment=LayoutAlignment.Start,VerticalAlignment=LayoutAlignment.Start};
            root.Add(text);root.Add(number);root.Add(rich);var overlays=new OverlayHost(root);
            using var ui=UiSession.Create(overlays,new SkiaVulkanRenderBackend(),new VulkanUiTarget(descriptor),new((int)(500*scale),(int)(320*scale),scale));
            var theme=light?Theme.Light:Theme.Dark;ui.SetTheme(theme);ui.Update();
            if(menu.Backdrop!=theme.MenuBackdrop||ui.Update())throw new Exception("Default backdrop/idle failed");
            var lease=ui.AcquireVulkanTexture();var layout=(ImageLayout)lease.Info.Layout;byte[] pixels;
            try{pixels=VulkanHostReadback.Read(device,lease.Info,state=>layout=state);}finally{lease.Return(layout);}
            foreach(var field in new Element[]{text,number.Editor,rich})
            {
                var badge=field.ValidationIconBounds;
                if(badge!=new Rect(field.Bounds.X+field.Bounds.Width-field.Bounds.Height,field.Bounds.Y,field.Bounds.Height,field.Bounds.Height))throw new Exception("Validation badge is not square");
                var offset=((int)((badge.Y+3)*scale)*(int)(500*scale)+(int)((badge.X+2)*scale))*4;
                if(Math.Abs(pixels[offset]-theme.Error.R)>2||Math.Abs(pixels[offset+1]-theme.Error.G)>2||Math.Abs(pixels[offset+2]-theme.Error.B)>2)throw new Exception("Validation fill missing");
            }
            if(number.Editor.ErrorMessage!=number.ErrorMessage||number.Editor.Value!="-"||theme.OnError==theme.Error)throw new Exception("Validation inheritance/contrast failed");
            var material=menu.Backdrop!;var y=(int)((menu.Bounds.Y+menu.Bounds.Height-2)*scale);var mid=menu.Bounds.X+menu.Bounds.Width/2;
            var left=pixels[(y*(int)(500*scale)+(int)((mid-3)*scale))*4];var right=pixels[(y*(int)(500*scale)+(int)((mid+3)*scale))*4];
            var flatLeft=material.Tint.R*material.Tint.A/255f;var flatRight=flatLeft+255-material.Tint.A;
            if(!(left>flatLeft+8&&right<flatRight-8&&left<right))throw new Exception($"Actual blur missing {left}/{right} vs {flatLeft}/{flatRight}");
            ui.Focus(text);ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.A,Control:true));ui.Update();
            var hit=text.ValidationIconBounds;ui.HandleInput(new(InputKind.PointerMove,hit.X+2,hit.Y+3));overlays.AdvanceFrame(TimeSpan.FromMilliseconds(500),ui);ui.Update();
            if(overlays.Popup is not Border{Child:ScrollViewer{Child:Text hint}}||hint.Value!=text.ErrorMessage||!text.IsFocused||text.SelectionLength!=text.Value.Length||ui.Update())throw new Exception("Validation tooltip/state/idle failed");
            using(var bitmap=new SKBitmap(new SKImageInfo((int)(500*scale),(int)(320*scale),SKColorType.Rgba8888,SKAlphaType.Premul)))
            {Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(args[0]+$"-{light}-{scale}.png");encoded.SaveTo(file);}
            ui.Dispose();if(!root.IsDisposed||!menu.IsDisposed)throw new Exception("Tree ownership leak");
            cases.Add(new{Light=light,Scale=scale,LeftRed=left,RightRed=right,UnblurredLeft=flatLeft,UnblurredRight=flatRight,ValidationBadges=true,ValidationTooltip=true,Disposed=root.IsDisposed});
        }
        device.WaitIdle();device.Dispose();
        if(device.ValidationErrorCount!=0||device.ValidationWarningCount!=0)throw new Exception("Validation failed including teardown");
        File.WriteAllText(args[0]+".json",JsonSerializer.Serialize(new{Passed=true,Cases=cases,Errors=device.ValidationErrorCount,Warnings=device.ValidationWarningCount,
            Native=SkiaNativeLibrary.LoadedPath,NativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SkiaNativeLibrary.LoadedPath!))),Assembly=typeof(SkiaVulkanRenderBackend).Assembly.Location},new JsonSerializerOptions{WriteIndented=true}));
    }
    private sealed class SplitBackground:Panel
    {protected override void DrawCore(IDrawingContext c){c.Fill(new(Bounds.X,Bounds.Y,Bounds.Width/2,Bounds.Height),new(0,0,0));c.Fill(new(Bounds.X+Bounds.Width/2,Bounds.Y,Bounds.Width/2,Bounds.Height),new(255,255,255));}}
}
'@)
Invoke-Dotnet 'restore' @('restore',"$run/consumer/Consumer.csproj",'--configfile',"$run/NuGet.Config",'--no-http-cache',"-bl:$evidence/restore-{}.binlog",'--verbosity','quiet')
Invoke-Dotnet 'build-debug' @('build',"$run/consumer/Consumer.csproj",'-c','Debug','--no-restore',"-bl:$evidence/build-debug-{}.binlog",'--verbosity','quiet')
Invoke-Dotnet 'publish-release' @('publish',"$run/consumer/Consumer.csproj",'-c','Release','--no-restore','-o',"$run/published","-bl:$evidence/publish-release-{}.binlog",'--verbosity','quiet')
Invoke-Dotnet 'native-debug' @("$run/consumer/bin/Debug/net10.0/Consumer.dll","$evidence/native-debug")
Invoke-Dotnet 'native-release' @("$run/published/Consumer.dll","$evidence/native-release")
$packages=@(Get-ChildItem "$run/feed" -Filter '*.nupkg'|ForEach-Object {[pscustomobject]@{Path=$_.FullName;Sha256=(Get-FileHash $_.FullName).Hash}})
[pscustomobject]@{Passed=$true;RunDirectory=$run;Version=$version;Packages=$packages;ProjectReferences=0;SourceFallback=$false;Commands=$commands}|ConvertTo-Json -Depth 7|Set-Content "$evidence/verification.json" -Encoding utf8
Write-Output "Fresh5package Vulkan backdrop consumer passed: $run"

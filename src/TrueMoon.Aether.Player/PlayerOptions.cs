using TrueMoon.Configuration;

namespace TrueMoon.Aether.Player;

/// <summary>Startup-only Player window data. Artifact/scene identity is supplied separately as PlayerStart.</summary>
public sealed class PlayerOptions
{
    /// <summary>Initial logical width,16..1920.</summary>
    public int Width{get;set;}=960;
    /// <summary>Initial logical height,16..1080.</summary>
    public int Height{get;set;}=540;
    /// <summary>Bounded native title.</summary>
    public string Title{get;set;}="Aether Player";
    /// <summary>Requires an installed Vulkan core/synchronization validation layer.</summary>
    public bool Validation{get;set;}
    internal PlayerOptions Snapshot()
    {
        if(Width is <16 or >1920||Height is <16 or >1080||string.IsNullOrWhiteSpace(Title)||Title.Length>256)throw new ArgumentOutOfRangeException(nameof(PlayerOptions));
        return new(){Width=Width,Height=Height,Title=Title,Validation=Validation};
    }
    /// <summary>Standard immutable window schema; explicitly add this exact instance and sources in the composition root.</summary>
    public static IConfigurationSchema<PlayerOptions> Schema{get;}=ConfigurationSchemaDefinitions.Create("Aether:Player",()=>new PlayerOptions(),value=>value.Snapshot(),value=>{_ = value.Snapshot();},fields=>
    {
        fields.Field("Width",value=>value.Width,(value,next)=>value.Width=next);
        fields.Field("Height",value=>value.Height,(value,next)=>value.Height=next);
        fields.Field("Title",value=>value.Title,(value,next)=>value.Title=next);
        fields.Field("Validation",value=>value.Validation,(value,next)=>value.Validation=next);
    });
}

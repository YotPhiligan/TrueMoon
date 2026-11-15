using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Configuration;
using TrueMoon.Kul;

namespace TrueMoon.Aether.Kul.Runtime;

/// <summary>Explicit optional Kul scene composition.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Registers lazy caller-owned scene factories after UseAudio. Build/Start acquire no scene resources.</summary>
    /// <param name="context">App composition root.</param><param name="content">Explicit immutable capture identity.</param>
    /// <param name="configure">Copied admission policy.</param><returns>Same context.</returns>
    public static IAppConfigurationContext UseAetherKul(this IAppConfigurationContext context, AudioContentReference content, Action<AudioBridgeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(content);
        var options = new AudioBridgeOptions(); configure?.Invoke(options); var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (!services.Exist<IFactory<AudioSession>>()) throw new InvalidOperationException("Register UseAudio before UseAetherKul.");
            if (services.Exist<IFactory<AudioSceneSubsystem, SceneSubsystemArguments>>()) throw new InvalidOperationException("Aether Kul is already registered.");
            if (!services.Exist<IFactory<IAudioContentLease, AudioContentLoadArguments>>())
                services.Singleton<IFactory<IAudioContentLease, AudioContentLoadArguments>>(_ => new Factory<IAudioContentLease, AudioContentLoadArguments>(AudioContentLease.Open));
            services.Singleton<IFactory<AudioBridgeSystem, AudioSceneSubsystem>>(_ => new Factory<AudioBridgeSystem, AudioSceneSubsystem>(owner => new(owner)));
            services.Singleton<IFactory<AudioSceneSubsystem, SceneSubsystemArguments>>(resolver => new AudioSceneSubsystemFactory(
                resolver.Resolve<IFactory<AudioSession>>()!, resolver.Resolve<IFactory<IAudioContentLease, AudioContentLoadArguments>>()!,
                resolver.Resolve<IFactory<AudioBridgeSystem, AudioSceneSubsystem>>()!, content, snapshot));
        });
        return context;
    }
    /// <summary>Binds the exact common schema while capture identity remains explicit runtime input.</summary>
    /// <param name="context">App with configuration stage.</param><param name="content">Immutable capture.</param>
    /// <param name="configure">Explicit startup overrides.</param><returns>Same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherKul(this IAppConfigurationContext context, AudioContentReference content, Action<AudioBridgeOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AudioBridgeConfigurationSchema.Default,
            (app, value) => app.UseAetherKul(content, options =>
            { options.MaximumSources = value.MaximumSources; options.MaximumPreparedVoices = value.MaximumPreparedVoices; options.MaximumAssets = value.MaximumAssets; options.MaximumEncodedBytes = value.MaximumEncodedBytes; }), configure);
    /// <summary>Selects Kul in the single shared scene-subsystem composition.</summary><param name="builder">Shared builder.</param><returns>Same builder.</returns>
    public static SceneSubsystemsBuilder AddKulAudio(this SceneSubsystemsBuilder builder)
    { ArgumentNullException.ThrowIfNull(builder); return builder.Add<AudioSceneSubsystem>("kul.audio"); }
}

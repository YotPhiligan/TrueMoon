using TrueMoon.Configuration;
using TrueMoon.Kul.Hosting;

namespace TrueMoon.Kul;

/// <summary>App composition for the Windows MiniAudioEx audio module.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Validates and snapshots options; registers one App owner and borrowed service/factory/lifecycle adapters.</summary>
    /// <param name="context">Application composition context.</param><param name="configure">Optional startup settings.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Register before audio consumers. Build opens no device; App startup creates the native graph.
    /// Sessions are caller-owned factory products, also tracked for App shutdown. Duplicate registration is rejected.
    /// Stop drains the native device before releasing voices, decode workers and context. It is non-cancellable.</remarks>
    public static IAppConfigurationContext UseAudio(this IAppConfigurationContext context, Action<AudioOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new AudioOptions();
        configure?.Invoke(options);
        var settings = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<AudioHost>() || services.Exist<IAudioService>() || services.Exist<IFactory<AudioSession>>() || services.Exist<AudioLifecycle>())
                throw new InvalidOperationException("Audio is already registered in this App.");
            services.Singleton<AudioHost>(_ => new AudioHost(settings));
            services.Singleton<IAudioService>(resolver => new AudioService(resolver.Resolve<AudioHost>()));
            services.Singleton<IFactory<AudioSession>>(resolver => new Factory<AudioSession>(resolver.Resolve<AudioHost>().CreateSession));
            services.Singleton<AudioLifecycle>(resolver => new AudioLifecycle(resolver.Resolve<AudioHost>()));
            services.Singleton<IStartable>(resolver => resolver.Resolve<AudioLifecycle>());
            services.Singleton<IStoppable>(resolver => resolver.Resolve<AudioLifecycle>());
        });
        return context;
    }

    /// <summary>Binds the exact registered Audio schema, applies explicit overrides and connects the same App lifecycle.</summary>
    /// <param name="context">App context supporting the common configuration stage.</param>
    /// <param name="configure">Pure explicit option overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAudio(this IAppConfigurationContext context, Action<AudioOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AudioConfigurationSchema.Default, (app, value) => app.RegisterSnapshot(value), configure);

    private static void RegisterSnapshot(this IAppConfigurationContext context, AudioOptions value)
        => context.UseAudio(options =>
        {
            options.Mode = value.Mode; options.SampleRate = value.SampleRate; options.PeriodFrames = value.PeriodFrames;
            options.Volume = value.Volume; options.MuteOutput = value.MuteOutput; options.MaximumSessions = value.MaximumSessions;
            options.MaximumClips = value.MaximumClips; options.MaximumVoices = value.MaximumVoices;
            options.MaximumMusicStreams = value.MaximumMusicStreams; options.MaximumSoundFileBytes = value.MaximumSoundFileBytes;
            options.MaximumResidentSoundBytes = value.MaximumResidentSoundBytes;
        });
}

namespace TrueMoon;

public static class AppBuilderExtensions
{
    public static Task RunAsync(this IAppBuilder builder, Action<IAppConfigurationContext> action, CancellationToken cancellationToken = default) 
        => App.RunAsync(builder, action, cancellationToken);
}
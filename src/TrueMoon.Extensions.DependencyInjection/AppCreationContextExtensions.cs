namespace TrueMoon.Extensions.DependencyInjection;

public static class AppCreationContextExtensions
{
    public static IAppCreationContext UseDI(this IAppCreationContext context)
    {
        return context.UseProvider<DependencyInjectionProvider>();
    }
}
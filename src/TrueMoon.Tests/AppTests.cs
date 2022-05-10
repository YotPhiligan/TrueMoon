using System.Threading.Tasks;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public class AppTests
{
    [Fact]
    public async Task AppCreate()
    {
        await using var app = App.Create(context => context
            .UseDependencyInjection<DependencyInjectionProvider>()
            .AddCommonDependencies(registrationContext => registrationContext.Add<object>())
            .AddProcessingEnclave(configurationContext => configurationContext
                .AddDependencies(registrationContext => registrationContext.Add<object, string>())
            )
        );

        await app.StartAsync();
    }
}
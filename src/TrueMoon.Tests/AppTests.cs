using System.Threading.Tasks;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Tests;

public class AppTests
{
    [Fact]
    public async Task AppCreate()
    {
        await using var app = App.Create(context => context
            .AddService(v=>v.)
            .AddService()
        );

        await app.StartAsync();
    }
}
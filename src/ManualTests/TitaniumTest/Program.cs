using TitaniumTest.Models;
using TitaniumTest.Services;
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Thorium;
using TrueMoon.Titanium;

await App.RunAsync(context => context
    .UseDiagnostics(configuration => configuration
        .OnEvent(@event => Console.WriteLine($"{@event}"))
        .Filters("TrueMoon")
    )
    .UseDI()
    .UseSignals()
    .AddUnit(ctx => ctx
        .AddDependencies(registrationContext => registrationContext
            .Add<Sender>(dependencyRegistrationContext => dependencyRegistrationContext
                .WithAppLifetime())
        )
    )
    .AddUnit(configuration => configuration
        .OnSignal<TestMessage,TestMessageResponse>(m =>
        {
            Console.WriteLine(m.Data);
            //Thread.Sleep(Random.Shared.Next(16, 240));
            return new TestMessageResponse { Data = $"{m.Data}_1" };
        })
        .OnSignal<TestMessage2,TestMessageResponse2>(m =>
        {
            Console.WriteLine(m.Data);
            //Thread.Sleep(Random.Shared.Next(16, 240));
            return new TestMessageResponse2 { Data = $"{m.Data}_2" };
        })
        .OnSignal<Test1Message>(m =>
        {
            var dif = DateTime.Now.TimeOfDay.TotalMilliseconds - m.Time.TotalMilliseconds;
            Console.WriteLine($"{DateTime.Now.TimeOfDay} - received: {m.Data}   ;    {dif} ms.");
        })
        .OnSignal<SignalTest>(_ =>
        {
            Console.WriteLine($"{DateTime.Now.TimeOfDay} - test signal");
        })
        .OnSignal<TestMessageLarge1,TestMessageLargeResponse1>(m =>
        {
            Console.WriteLine(m.Index);
            //Thread.Sleep(Random.Shared.Next(16, 240));
            return new TestMessageLargeResponse1 { Index = m.Index, Result = true, Data = m.Data.Reverse().ToArray()  };
        })
        .OnSignal<TestMessage3,TestMessageLargeResponse3>(m =>
        {
            Console.WriteLine($"small request -> Large Response: {m.Index}");
            //Thread.Sleep(Random.Shared.Next(16, 240));
            var bytes = new byte[102 * 1024];
            bytes[0] = 255;
            bytes[1] = 254;
            bytes[2] = 253;
            bytes[3] = 252;
            return new TestMessageLargeResponse3 { Index = m.Index, Result = true, Data = bytes  };
        })
    )
);
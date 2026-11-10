using TrueMoon;
using TrueMoon.Cobalt;
using TrueMoon.Services;

namespace TrueMoon.ManualTests.CobaltGeneratorConsumer.Presentation.Shell.WithLongNamespaces.ForGeneratedResolvers.AndGenericFactoryRegistrations;

internal static class Program
{
    private static void Main()
    {
        var resolver = new CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
            .Singleton<IFactory<Outer.TitleBar<int>, Outer.Arguments>, FactoryWithTheSameLongReadablePrefixForBothImplementationsOne>()
            .Singleton<FactoryWithTheSameLongReadablePrefixForBothImplementationsTwo>()
            .Transient<Outer.Сервис>().Singleton<Outer.服务>()
            .Singleton(typeof(Outer.IBox<>), typeof(Outer.Box<>))
            .Composite<Outer.Owned, Outer.IOne, Outer.ITwo>()]);
        using var owner = (IDisposable)resolver;
        var factory = (FactoryWithTheSameLongReadablePrefixForBothImplementationsOne)resolver.Resolve<IFactory<Outer.TitleBar<int>, Outer.Arguments>>();
        var secondFactory = resolver.Resolve<FactoryWithTheSameLongReadablePrefixForBothImplementationsTwo>();
        var owned = resolver.Resolve<Outer.Owned>();
        Check(ReferenceEquals(factory, resolver.Resolve<IFactory<Outer.TitleBar<int>, Outer.Arguments>>()));
        Check(factory.Create(new Outer.Arguments()) is not null);
        Check(ReferenceEquals(secondFactory, resolver.Resolve<FactoryWithTheSameLongReadablePrefixForBothImplementationsTwo>()));
        Check(ReferenceEquals(owned, resolver.Resolve<Outer.IOne>()));
        Check(ReferenceEquals(owned, resolver.Resolve<Outer.ITwo>()));
        Check(!ReferenceEquals(resolver.Resolve<Outer.Сервис>(), resolver.Resolve<Outer.Сервис>()));
        Check(ReferenceEquals(resolver.Resolve<Outer.服务>(), resolver.Resolve<Outer.服务>()));
        Check(ReferenceEquals(resolver.Resolve<Outer.IBox<int>>(), resolver.Resolve<Outer.IBox<int>>()));
        Check(resolver.Resolve<Outer.IBox<string>>() is Outer.Box<string>);
        Check(factory.Disposals == 0 && secondFactory.Disposals == 0 && owned.Disposals == 0);
        owner.Dispose();
        Check(factory.Disposals == 1 && secondFactory.Disposals == 1 && owned.Disposals == 1);
        Console.WriteLine("Cobalt generator consumer: 11 checks passed");
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Generated graph contract failed.");
    }
}

internal class Outer
{
    public class TitleBar<T>;
    public class Arguments;
    public class Сервис;
    public class 服务;
    public interface IBox<T>;
    public class Box<T> : IBox<T>;
    public interface IOne;
    public interface ITwo;
    public class Owned : IOne, ITwo, IDisposable
    {
        public int Disposals;
        public void Dispose() => Disposals++;
    }
}

internal class FactoryWithTheSameLongReadablePrefixForBothImplementationsOne : IFactory<Outer.TitleBar<int>, Outer.Arguments>, IDisposable
{
    public int Disposals;
    public Outer.TitleBar<int> Create(Outer.Arguments arguments) => new();
    public void Dispose() => Disposals++;
}

internal class FactoryWithTheSameLongReadablePrefixForBothImplementationsTwo : IDisposable
{
    public int Disposals;
    public void Dispose() => Disposals++;
}

// using TrueMoon.Diagnostics;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileInvocationClientFactory : IInvocationClientFactory
// {
//     private readonly IEventsSourceFactory _eventsSourceFactory;
//     private readonly ThoriumConfiguration _configuration;
//
//     public MemoryMappedFileInvocationClientFactory(IEventsSourceFactory eventsSourceFactory, ThoriumConfiguration configuration)
//     {
//         _eventsSourceFactory = eventsSourceFactory;
//         _configuration = configuration;
//     }
//     
//     public IInvocationClient<T> Create<T>() => new MemoryMappedFileInvocationClient<T>(_eventsSourceFactory.Create<MemoryMappedFileInvocationClient<T>>(), _configuration);
// }
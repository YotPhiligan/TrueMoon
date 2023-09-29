// using TrueMoon.Diagnostics;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileInvocationServerFactory : IInvocationServerFactory
// {
//     private readonly IEventsSourceFactory _eventsSourceFactory;
//     private readonly IInvocationServerHandlerResolver _resolver;
//     private readonly ThoriumConfiguration _configuration;
//
//     public MemoryMappedFileInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, 
//         IInvocationServerHandlerResolver resolver,
//         ThoriumConfiguration configuration)
//     {
//         _eventsSourceFactory = eventsSourceFactory;
//         _resolver = resolver;
//         _configuration = configuration;
//     }
//     
//     public IInvocationServer<T> Create<T>()
//     {
//         var handler = _resolver.Resolve<T>();
//         return new MemoryMappedFileInvocationServer<T>(_eventsSourceFactory.Create<MemoryMappedFileInvocationServer<T>>(), handler, _configuration);
//     }
// }
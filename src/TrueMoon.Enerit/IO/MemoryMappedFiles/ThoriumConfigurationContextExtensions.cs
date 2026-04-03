// using TrueMoon.Dependencies;
//
// namespace TrueMoon.Enerit.IO.MemoryMappedFiles;
//
// public static class ThoriumConfigurationContextExtensions
// {
//     public static IThoriumConfigurationContext MemoryMappedFiles(this IThoriumConfigurationContext context)
//     {
//         context.AppCreationContext.AddDependencies(registrationContext => registrationContext
//             .RemoveAll<IInvocationClientFactory>()
//             .RemoveAll<IInvocationServerFactory>()
//             .RemoveAll<ISignalsHandleFactory>()
//             .Add<IInvocationClientFactory, MemoryMappedFileInvocationClientFactory>()
//             .Add<IInvocationServerFactory, MemoryMappedFileInvocationServerFactory>()
//             .Add<ISignalsHandleFactory, MemoryMappedFileSignalsHandleFactory>()
//         );
//         return context;
//     }
// }
using Headless.Consumer;

if (args.Length != 3 || args[0] is not ("cobalt" or "microsoft") || !ConsumerProbe.Scenarios.Contains(args[1]))
{
    Console.Error.WriteLine("Usage: HeadlessConsumer <cobalt|microsoft> <normal|cancel|start-failure|stop-failure|build-failure> <report.json>");
    return 2;
}
return await ConsumerProbe.RunAsync(args[0], args[1], args[2]);

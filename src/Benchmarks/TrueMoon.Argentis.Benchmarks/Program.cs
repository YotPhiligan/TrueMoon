using TrueMoon.Argentis.Benchmarks;

if (args is ["--verify"])
{
    WorkloadVerification.Verify();
    Console.WriteLine("VIEW_BASELINE_VERIFIED");
    return 0;
}

string? group = null;
var size = 0;
var output = "";
var samples = 100;
var warmupSamples = 20;
var warmupMilliseconds = 250;
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--measure":
            break;
        case "--group":
            group = args[++index];
            break;
        case "--size":
            size = int.Parse(args[++index]);
            break;
        case "--output":
            output = args[++index];
            break;
        case "--samples":
            samples = int.Parse(args[++index]);
            break;
        case "--warmup-samples":
            warmupSamples = int.Parse(args[++index]);
            break;
        case "--warmup-ms":
            warmupMilliseconds = int.Parse(args[++index]);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[index]}");
            return 2;
    }
}

if (group is null || !Cases.Groups.Contains(group) || !Cases.Sizes(group).Contains(size) || output.Length == 0 || samples < 1)
{
    Console.Error.WriteLine("Usage: --measure --group <" + string.Join('|', Cases.Groups) +
        "> --size <scene size> --output <json> [--samples N] [--warmup-samples N] [--warmup-ms N]");
    return 2;
}

Probe.Run(new ProbeOptions(group, size, output, samples, warmupSamples, warmupMilliseconds));
Console.WriteLine($"VIEW_PROBE_COMPLETED {group} {size}");
return 0;

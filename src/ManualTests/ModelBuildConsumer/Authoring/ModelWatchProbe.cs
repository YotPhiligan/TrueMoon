using System.Buffers.Binary;
using TrueMoon;
using TrueMoon.Aether.Tooling.Importing;

internal static class ModelWatchProbe
{
    internal static async Task<object> RunAsync(IFactory<ModelSourceWatch, ModelSourceWatchArguments> factory,
        ModelSourceWatchArguments redArguments, ModelSourceWatchArguments greenArguments, string bufferPath)
    {
        var original = File.ReadAllBytes(bufferPath);
        var edited = original.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(edited.AsSpan(16), .25f);
        var held = bufferPath + ".held";
        bool redNotified, recoveryNotified, missingNotified;
        long greenSequence;
        try
        {
            // The polling delay exceeds the assertion deadline, so these observations require actual native hints.
            await using (var red = factory.Create(redArguments with { PollInterval = TimeSpan.FromMinutes(1) })!)
            await using (var green = factory.Create(greenArguments with { PollInterval = TimeSpan.FromMinutes(1) })!)
            {
                var redInitial = await WaitAsync(red, ModelImportFreshnessStatus.Current);
                var greenInitial = await WaitAsync(green, ModelImportFreshnessStatus.Current);
                Check(redInitial.NotificationsActive && greenInitial.NotificationsActive, "Native source watches did not attach.");
                File.WriteAllBytes(bufferPath, edited);
                var redEdited = await WaitAsync(red, ModelImportFreshnessStatus.Stale, redInitial.Sequence);
                Check(redEdited.TriggeredByNotification && redEdited.Freshness.InputChanged == true, "BIN edit was not confirmed after a notification.");
                redNotified = redEdited.TriggeredByNotification;

                File.Move(bufferPath, held);
                var missing = await WaitAsync(red, ModelImportFreshnessStatus.Unavailable, redEdited.Sequence);
                Check(missing.TriggeredByNotification, "Missing dependency was not confirmed after a notification.");
                missingNotified = missing.TriggeredByNotification;
                File.WriteAllBytes(held, original);
                File.Move(held, bufferPath);
                var recovered = await WaitAsync(red, ModelImportFreshnessStatus.Current, missing.Sequence);
                Check(recovered.TriggeredByNotification && ReferenceEquals(greenInitial, green.Read()),
                    "Dependency recovery was not observed or the unrelated model was scanned.");
                recoveryNotified = recovered.TriggeredByNotification;
                greenSequence = greenInitial.Sequence;
            }

            await using var polling = factory.Create(redArguments with
            {
                UseFileSystemNotifications = false,
                PollInterval = TimeSpan.FromMilliseconds(250)
            })!;
            var pollInitial = await WaitAsync(polling, ModelImportFreshnessStatus.Current);
            File.WriteAllBytes(bufferPath, edited);
            var polled = await WaitAsync(polling, ModelImportFreshnessStatus.Stale, pollInitial.Sequence);
            Check(!polled.TriggeredByNotification && !polled.NotificationsActive,
                "Polling could not detect the edit independently of native signals.");
        }
        finally
        {
            if (File.Exists(held)) File.Move(held, bufferPath, overwrite: true);
            File.WriteAllBytes(bufferPath, original);
        }

        return new { RedNotified = redNotified, MissingNotified = missingNotified, RecoveryNotified = recoveryNotified,
            GreenSequence = greenSequence, PollingDetectedEdit = true };
    }

    internal static async Task<ModelSourceObservation> WaitAsync(ModelSourceWatch watch, ModelImportFreshnessStatus expected, long after = 0)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            if (watch.Read() is { } observation && observation.Sequence > after && observation.Freshness.Status == expected)
                return observation;
            await Task.Delay(20, timeout.Token);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

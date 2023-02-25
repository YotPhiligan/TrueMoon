using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.Tests;

public class DataStorageHandleTests
{
    [Fact]
    public void GetDetails()
    {
        var name = "tm_test";
        var thoriumConfiguration = new ThoriumConfiguration{Name = name};
        var listener = new EventsSource<StorageHandle>();
        using var handle = new StorageHandle(thoriumConfiguration, listener);

        var details = handle.GetDescriptor();

        Assert.Equal(thoriumConfiguration.SmallSignals,details.SmallSignals);
        Assert.Equal(thoriumConfiguration.SmallSignalSize,details.SmallSignalSize);
        Assert.Equal(thoriumConfiguration.LargeSignals,details.LargeSignals);
        Assert.Equal(thoriumConfiguration.LargeSignalSize,details.LargeSignalSize);
    }
}
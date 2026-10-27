using System.Globalization;
using System.Text.Json;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Hosting;

/// <summary>Validated child launch data, without creating an App, services or runtime resources.</summary>
/// <param name="Launch">Normalized launch with a private snapshot of diagnostic delivery options.</param>
/// <param name="ParentProcessId">Positive parent process ID, distinct from the current process.</param>
public sealed record RuntimeBootstrap(SessionLaunch Launch, int ParentProcessId)
{
    private const int MaximumTextLength = 16384;
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 32 };

    /// <summary>Reads and validates the current child's bootstrap and parent environment variables.</summary>
    /// <param name="allowLegacyGame">Explicitly permits a game without a selected artifact only when its ID matches the local legacy game.</param>
    /// <returns>Validated launch data and parent process ID.</returns>
    /// <exception cref="InvalidDataException">The bootstrap, parent, runtime identity or launch configuration is invalid.</exception>
    public static RuntimeBootstrap ReadEnvironment(bool allowLegacyGame = false)
        => Parse(Environment.GetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey)!,
            Environment.GetEnvironmentVariable("-p")!, allowLegacyGame);

    /// <summary>Parses bounded launch JSON and validates it against the current child runtime.</summary>
    /// <param name="text">Launch JSON, limited to 16384 characters and a maximum depth of 32.</param>
    /// <param name="parentProcessId">Invariant decimal digits identifying a positive parent other than this process.</param>
    /// <param name="allowLegacyGame">Explicitly permits a null build only for the matching local legacy game ID.</param>
    /// <returns>Validated launch data with normalized absolute report/artifact paths and copied diagnostic options.</returns>
    /// <exception cref="InvalidDataException">Input is missing, oversized, malformed or incompatible with this runtime.</exception>
    public static RuntimeBootstrap Parse(string text, string parentProcessId, bool allowLegacyGame = false)
    {
        if (text == null || text.Length > MaximumTextLength || string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Missing or oversized runtime bootstrap; the limit is 16384 characters.");
        if (!int.TryParse(parentProcessId, NumberStyles.None, CultureInfo.InvariantCulture, out var parentId) ||
            parentId <= 0 || parentId == Environment.ProcessId)
            throw new InvalidDataException("Runtime parent must be a positive invariant process ID distinct from this process.");

        try
        {
            var launch = Validate(JsonSerializer.Deserialize<SessionLaunch>(text, JsonOptions), allowLegacyGame);
            BuildIdentity.ValidateRuntime(launch);
            return new(launch, parentId);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Malformed runtime bootstrap JSON.", error);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Invalid runtime bootstrap configuration or runtime artifacts.", error);
        }
    }

    /// <summary>Validates and encodes a selected-game or preview launch for an owned child process.</summary>
    /// <param name="launch">Launch data to normalize and encode; the caller's diagnostic options are not retained or modified.</param>
    /// <param name="allowLegacyGame">Explicit same-executable probe profile; the child must also opt in when parsing.</param>
    /// <returns>Launch JSON of at most 16384 characters and maximum depth 32.</returns>
    /// <remarks>Called by the parent; verification against the child's actual runtime entry and fingerprints happens only in <see cref="Parse"/>.</remarks>
    /// <exception cref="InvalidDataException">The launch is invalid, requires implicit legacy game support or exceeds the JSON limits.</exception>
    public static string Encode(SessionLaunch launch, bool allowLegacyGame = false)
    {
        launch = Validate(launch, allowLegacyGame);
        string text;
        try { text = JsonSerializer.Serialize(launch, JsonOptions); }
        catch (JsonException error)
        {
            throw new InvalidDataException("Runtime bootstrap exceeds the JSON limits.", error);
        }
        if (text.Length > MaximumTextLength)
            throw new InvalidDataException("Runtime bootstrap exceeds 16384 characters.");
        return text;
    }

    private static SessionLaunch Validate(SessionLaunch? launch, bool allowLegacyGame)
    {
        if (launch?.Identity is not { } identity)
            throw new InvalidDataException("Runtime bootstrap requires a session identity.");
        identity.Validate();
        if (!Enum.IsDefined(launch.Role))
            throw new InvalidDataException("Unknown runtime role.");
        if (string.IsNullOrWhiteSpace(launch.Endpoint) ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new InvalidDataException("Runtime control endpoint must start with 'engine-' and contain at most 100 characters.");
        if (string.IsNullOrWhiteSpace(launch.ReportDirectory) || !Path.IsPathFullyQualified(launch.ReportDirectory))
            throw new InvalidDataException("Runtime report directory must be an absolute path.");

        if (launch.Role == RuntimeRole.EditPreview)
        {
            if (launch.Build != null || launch.RequireRestore || identity.GameBuildId != BuildIdentity.PreviewId)
                throw new InvalidDataException("Edit preview requires the built-in preview ID and forbids a game build or restore.");
        }
        else if (launch.Build is { } build)
        {
            if (build.BuildId != identity.GameBuildId)
                throw new InvalidDataException("Runtime game build does not match the session identity.");
            if (string.IsNullOrWhiteSpace(build.DirectoryPath) || !Path.IsPathFullyQualified(build.DirectoryPath) || build.DirectoryPath.Contains('\0'))
                throw new InvalidDataException("Runtime game artifact directory must be an absolute path without NUL.");
        }
        else if (!allowLegacyGame || identity.GameBuildId != BuildIdentity.GameId)
            throw new InvalidDataException("Game runtime requires a matching selected build or explicit matching legacy game support.");

        try
        {
            return launch with
            {
                ReportDirectory = Path.GetFullPath(launch.ReportDirectory),
                Build = launch.Build is { } buildReference ? buildReference with { DirectoryPath = Path.GetFullPath(buildReference.DirectoryPath) } : null,
                DiagnosticDelivery = launch.DiagnosticDelivery?.Snapshot()
            };
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException)
        {
            throw new InvalidDataException("Invalid runtime report/artifact path or diagnostic delivery options.", error);
        }
    }
}

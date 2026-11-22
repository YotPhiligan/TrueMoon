using System.Collections.Immutable;

namespace TrueMoon.Aether.Abstractions.Sessions;

/// <summary>Detached text for a non-interactive HUD. Journal entries are ordered oldest to newest.</summary>
public sealed class GameHudSnapshot
{
    /// <summary>Maximum journal rows retained in the HUD.</summary>
    public const int JournalCapacity = 5;

    /// <summary>Maximum characters per displayed line.</summary>
    public const int LineCapacity = 160;

    /// <summary>Copies and validates bounded, single-line display values.</summary>
    public GameHudSnapshot(string? prompt, string? status, IEnumerable<string> journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ValidateLine(prompt, nameof(prompt));
        ValidateLine(status, nameof(status));
        var rows = ImmutableArray.CreateBuilder<string>();
        foreach (var row in journal)
        {
            if (rows.Count == JournalCapacity)
                throw new ArgumentException("The HUD journal exceeds its row limit.", nameof(journal));
            ArgumentNullException.ThrowIfNull(row);
            ValidateLine(row, nameof(journal));
            rows.Add(row);
        }
        Prompt = prompt;
        Status = status;
        Journal = rows.ToImmutable();
    }

    /// <summary>The current action prompt, if any.</summary>
    public string? Prompt { get; }

    /// <summary>The game's status line, if any.</summary>
    public string? Status { get; }

    /// <summary>The detached tail of the game's journal.</summary>
    public ImmutableArray<string> Journal { get; }

    private static void ValidateLine(string? text, string parameter)
    {
        if (text is not null && (text.Length > LineCapacity || text.Any(char.IsControl)))
            throw new ArgumentException("A HUD line must fit the character limit and contain no control characters.", parameter);
    }
}

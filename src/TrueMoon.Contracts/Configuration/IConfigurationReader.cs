namespace TrueMoon.Configuration;

/// <summary>Borrowed access to the application's prepared configuration.</summary>
public interface IConfigurationReader
{
    /// <summary>Current immutable snapshot. Access after App disposal throws ObjectDisposedException.</summary>
    IConfigurationSnapshot Current { get; }
}

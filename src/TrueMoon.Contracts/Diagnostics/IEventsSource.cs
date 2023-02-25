using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public interface IEventsSource
{
    string Name { get; }

    Activity StartActivity(string? details = default, string? category = default,
        [CallerMemberName] string? caller = default);
    void StopActivity(Activity activity);

    void Write(Func<object>? func, string? category = default, [CallerMemberName] string? caller = default);
}

public interface IEventsSource<T> : IEventsSource {}
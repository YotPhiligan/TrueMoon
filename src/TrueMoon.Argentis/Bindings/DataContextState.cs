namespace TrueMoon.Argentis.Bindings;

internal sealed class DataContextState<TData>
{
    internal TData? Value { get; private set; }
    internal event Action? Changed;

    internal void Set(Element owner, TData? value)
    {
        owner.VerifyAccess();
        var same = typeof(TData).IsValueType
            ? EqualityComparer<TData?>.Default.Equals(Value, value)
            : ReferenceEquals(Value, value);
        if (same) return;

        Value = value;
        if (Changed is not { } handlers) return;
        List<Exception>? errors = null;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        TreeChange.ThrowErrors(errors);
    }
}

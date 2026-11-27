namespace TrueMoon.Argentis.Layout;

/// <summary>The row source whose window a <see cref="VirtualizingStack"/> realizes.</summary>
internal interface IVirtualizingItemsBinding : IItemsBinding
{
    /// <summary>Number of rows in the bound source.</summary>
    int ItemCount { get; }

    /// <summary>Makes rows <paramref name="start"/> through <paramref name="start"/> + <paramref name="length"/> - 1 the container's children.</summary>
    void PrepareWindow(int start, int length);
}

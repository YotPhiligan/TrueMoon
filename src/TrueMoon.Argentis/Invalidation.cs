namespace TrueMoon.Argentis;

/// <summary>The work invalidated by a property or tree mutation.</summary>
[Flags]
public enum Invalidation { Render = 1, Layout = 2, Tree = 4 }
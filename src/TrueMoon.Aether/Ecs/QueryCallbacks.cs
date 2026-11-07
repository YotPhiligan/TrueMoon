namespace TrueMoon.Aether.Ecs;

/// <summary>Observes a component only within a synchronous callback.</summary>
/// <typeparam name="T">Component type.</typeparam><param name="entity">Matched entity.</param><param name="value">Borrowed readonly value.</param>
public delegate void QueryReadAction<T>(EntityHandle entity, scoped in T value) where T : struct;
/// <summary>Writes a component only within a synchronous callback; previous writes survive callback failure.</summary>
/// <typeparam name="T">Component type.</typeparam><param name="entity">Matched entity.</param><param name="value">Borrowed writable value.</param>
public delegate void QueryWriteAction<T>(EntityHandle entity, scoped ref T value) where T : struct;
/// <summary>Observes two required components.</summary>
/// <typeparam name="T1">First type.</typeparam><typeparam name="T2">Second type.</typeparam>
/// <param name="entity">Matched entity.</param><param name="first">First borrowed value.</param><param name="second">Second borrowed value.</param>
public delegate void QueryReadAction<T1, T2>(EntityHandle entity, scoped in T1 first, scoped in T2 second) where T1 : struct where T2 : struct;
/// <summary>Writes the first component and observes the second.</summary>
/// <typeparam name="T1">Writable type.</typeparam><typeparam name="T2">Readonly type.</typeparam>
/// <param name="entity">Matched entity.</param><param name="first">Writable value.</param><param name="second">Readonly value.</param>
public delegate void QueryWriteAction<T1, T2>(EntityHandle entity, scoped ref T1 first, scoped in T2 second) where T1 : struct where T2 : struct;
/// <summary>Writes both required components within one callback.</summary>
/// <typeparam name="T1">First type.</typeparam><typeparam name="T2">Second type.</typeparam>
/// <param name="entity">Matched entity.</param><param name="first">First value.</param><param name="second">Second value.</param>
public delegate void QueryWriteBothAction<T1, T2>(EntityHandle entity, scoped ref T1 first, scoped ref T2 second) where T1 : struct where T2 : struct;

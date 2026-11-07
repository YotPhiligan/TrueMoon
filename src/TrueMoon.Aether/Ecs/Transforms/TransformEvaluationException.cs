namespace TrueMoon.Aether.Ecs.Transforms;

/// <summary>Stable transform/movement failure categories.</summary>
public enum TransformEvaluationError
{
    /// <summary>A node is missing one of LocalTransform, TransformParent or WorldTransform.</summary>
    IncompleteNode,
    /// <summary>A parent is stale, foreign or not a transform node.</summary>
    InvalidParent,
    /// <summary>A self edge or longer cycle was found.</summary>
    Cycle,
    /// <summary>A local pose cannot be represented.</summary>
    InvalidLocalTransform,
    /// <summary>Composition produced an invalid or singular world matrix.</summary>
    InvalidWorldTransform,
    /// <summary>Velocity or displacement cannot be represented.</summary>
    InvalidVelocity,
    /// <summary>The configured node admission limit was exceeded.</summary>
    LimitExceeded
}

/// <summary>Contextual failure detected before publishing new derived matrices or movement values.</summary>
public sealed class TransformEvaluationException : InvalidOperationException
{
    internal TransformEvaluationException(TransformEvaluationError error, EntityHandle entity, EntityHandle parent = default, Exception? inner = null)
        : base($"Transform evaluation failed: {error}, entity {entity.Index}:{entity.Generation}.", inner)
    { Error = error; Entity = entity; Parent = parent; }
    /// <summary>Gets the stable category.</summary>
    public TransformEvaluationError Error { get; }
    /// <summary>Gets the node whose data/edge failed.</summary>
    public EntityHandle Entity { get; }
    /// <summary>Gets the relevant parent, or the root marker.</summary>
    public EntityHandle Parent { get; }
}

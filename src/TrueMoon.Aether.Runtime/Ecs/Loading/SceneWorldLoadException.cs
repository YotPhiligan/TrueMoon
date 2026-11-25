using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Ecs.Loading
{
    /// <summary>Load error with stable location and optional retained cleanup ownership.</summary>
    public sealed class SceneWorldLoadException : Exception
    {
        internal SceneWorldLoadException(SceneWorldLoadError error, string message, SceneId? scene = null,
            EntityId? entity = null, ComponentId? component = null, string? property = null, Exception? inner = null, SceneWorld? cleanup = null)
            : base(message, inner) { Error = error; SceneId = scene; EntityId = entity; ComponentId = component; PropertyId = property; CleanupOwner = cleanup; }
        /// <summary>Gets the stable error code.</summary>
        public SceneWorldLoadError Error { get; }
        /// <summary>Gets scene identity when available.</summary>
        public SceneId? SceneId { get; }
        /// <summary>Gets entity identity when available.</summary>
        public EntityId? EntityId { get; }
        /// <summary>Gets occurrence identity when available.</summary>
        public ComponentId? ComponentId { get; }
        /// <summary>Gets the ordinal property key when available.</summary>
        public string? PropertyId { get; }
        /// <summary>Gets a failed load owner to Dispose again after cleanup failure.</summary>
        public SceneWorld? CleanupOwner { get; }
    }
}

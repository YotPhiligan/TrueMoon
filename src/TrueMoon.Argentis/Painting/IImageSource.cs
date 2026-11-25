namespace TrueMoon.Argentis.Painting;

/// <summary>A borrowed or owned decoded image. The creator controls its lifetime.</summary>
public interface IImageSource : IDisposable
{
    /// <summary>Image dimensions.</summary>
    Size Size { get; }
}
namespace TrueMoon.Aluminum;

public interface IContentPresenter
{
    void Present(double t, IContentPresenterContext context);
}

public interface IContentPresenter<TContent> : IContentPresenter
{
    TContent? Content { get; }
}
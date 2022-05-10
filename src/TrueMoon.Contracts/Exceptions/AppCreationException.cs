namespace TrueMoon.Exceptions;

public class AppCreationException : Exception
{
    public AppCreationException(string message) : base(message) {}
    public AppCreationException(string message, Exception e) : base(message, e) {}
}
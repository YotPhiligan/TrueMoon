namespace TrueMoon.Thorium;

public class SignalMappingItem
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public Type SignalType { get; set; }
    public Type SignalWrapperType { get; set; }
    public Type? SignalReponseWrapperType { get; set; }
    public Guid Code { get; set; }
    public bool? IsListen { get; set; }
}
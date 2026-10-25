namespace Identity.Application.Common;

public sealed class ApplicationException : Exception
{
    public string Code { get; }

    public ApplicationException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }
}
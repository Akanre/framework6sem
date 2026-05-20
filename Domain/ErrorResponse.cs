namespace Pr3.ConfigAndSecurity.Domain;

public sealed record ErrorResponse(string Code, string Message, string RequestId);

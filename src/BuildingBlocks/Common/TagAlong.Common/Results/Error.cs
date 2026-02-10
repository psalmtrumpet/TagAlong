namespace TagAlong.Common.Results;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");

    public static Error NotFound(string entityName, Guid id) =>
        new($"{entityName}.NotFound", $"{entityName} with ID '{id}' was not found.", ErrorType.NotFound);

    public static Error NotFound(string message) =>
        new("Error.NotFound", message, ErrorType.NotFound);

    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static Error Validation(string message) =>
        new("Error.Validation", message, ErrorType.Validation);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static Error Conflict(string message) =>
        new("Error.Conflict", message, ErrorType.Conflict);

    public static Error Unauthorized(string message = "Unauthorized access.") =>
        new("Error.Unauthorized", message, ErrorType.Unauthorized);

    public static Error Forbidden(string message = "Access forbidden.") =>
        new("Error.Forbidden", message, ErrorType.Forbidden);
}

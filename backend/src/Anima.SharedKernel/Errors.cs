using Anima.Contracts;

namespace Anima.SharedKernel;

/// <summary>Lỗi nghiệp vụ có mã cố định. Ném ra để rollback transaction đang chạy.</summary>
public sealed class DomainException(string code, string message, int status = 400, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public object? Details { get; } = details;

    public static DomainException Validation(string message, object? details = null) => new(ErrorCodes.ValidationFailed, message, 400, details);
    public static DomainException NotFound(string what) => new(ErrorCodes.NotFound, $"{what} not found", 404);
    public static DomainException Forbidden(string code, string message) => new(code, message, 403);
    public static DomainException Conflict(string code, string message) => new(code, message, 409);
}

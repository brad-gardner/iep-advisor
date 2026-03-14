namespace IepAssistant.Services.Models;

public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ServiceResult SuccessResult(string? message = null)
        => new() { Success = true, Message = message };

    public static ServiceResult FailureResult(string message)
        => new() { Success = false, Message = message };

    public static ServiceResult FailureResult(List<string> errors)
        => new() { Success = false, Errors = errors };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public new static ServiceResult<T> FailureResult(string message)
        => new() { Success = false, Message = message };
}

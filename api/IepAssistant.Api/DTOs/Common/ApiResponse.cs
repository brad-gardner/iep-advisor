namespace IepAssistant.Api.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> SuccessResponse(T? data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Error(string message)
        => new() { Success = false, Message = message };

    public static ApiResponse<T> Error(List<string> errors)
        => new() { Success = false, Errors = errors };
}

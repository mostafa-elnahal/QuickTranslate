using System;

namespace QuickTranslate.Models;

/// <summary>
/// Wraps the result of a pronunciation operation, including success status, data, and user-friendly messages.
/// </summary>
/// <typeparam name="T">The type of data returned on success.</typeparam>
public class PronunciationResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? TechnicalDetails { get; init; }

    public static PronunciationResult<T> Success(T data, string message = "")
    {
        return new PronunciationResult<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message
        };
    }

    public static PronunciationResult<T> Failure(string message, Exception? ex = null)
    {
        return new PronunciationResult<T>
        {
            IsSuccess = false,
            Data = default,
            Message = message,
            TechnicalDetails = ex?.ToString()
        };
    }
}

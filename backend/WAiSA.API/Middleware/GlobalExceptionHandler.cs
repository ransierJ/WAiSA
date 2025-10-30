using Microsoft.AspNetCore.Diagnostics;

namespace WAiSA.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception,
            "GLOBAL EXCEPTION HANDLER: Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}",
            exception.GetType().Name, exception.Message);

        _logger.LogError("Stack Trace: {StackTrace}", exception.StackTrace);

        if (exception.InnerException != null)
        {
            _logger.LogError("Inner Exception Type: {InnerExceptionType}, Message: {InnerMessage}",
                exception.InnerException.GetType().Name, exception.InnerException.Message);
            _logger.LogError("Inner Stack Trace: {InnerStackTrace}", exception.InnerException.StackTrace);
        }

        // Return false to allow the default exception handler to continue processing
        return ValueTask.FromResult(false);
    }
}

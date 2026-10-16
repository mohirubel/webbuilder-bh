using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebBuilders.Domains.Business.Common;

namespace WebBuilders.Domains.Api.Infrastructure;

/// <summary>Translates business-layer exceptions into RFC 7807 problem responses.</summary>
public sealed class DomainsExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Request cannot be completed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            FeatureUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Not available"),
            ExternalServiceException => (StatusCodes.Status502BadGateway, "Upstream provider error"),
            _ => (0, "")
        };

        if (status == 0)
            return false; // let the default handler log and return a generic 500

        if (exception is ExternalServiceException)
            logger.LogWarning(exception, "Upstream failure");

        var problem = new ProblemDetails { Status = status, Title = title, Detail = exception.Message };
        if (exception is ValidationException { Errors.Count: > 0 } validation)
            problem.Extensions["errors"] = validation.Errors;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}

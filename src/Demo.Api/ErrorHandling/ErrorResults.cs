using Demo.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Api.ErrorHandling;

/// <summary>Maps expected application errors to RFC 9457 ProblemDetails responses (ADR-0006).</summary>
public static class ErrorResults
{
    public static IActionResult ToProblemResult(this ControllerBase controller, Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorType.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Service unavailable"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Unknown error type.")
        };

        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: status,
            title: title,
            detail: error.Message);

        // Stable, machine-readable discriminator; clients should branch on it rather than on the text.
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}

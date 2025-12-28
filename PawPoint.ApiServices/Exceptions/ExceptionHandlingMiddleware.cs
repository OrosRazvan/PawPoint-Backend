using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Authentication;

namespace PawPoint.ApiServices.Exceptions
{
    public class ExceptionHandlingMiddleware : IExceptionHandler
    {
        private readonly ProblemDetailsFactory _problemDetailsFactory;

        public ExceptionHandlingMiddleware(ProblemDetailsFactory problemDetailsFactory)
        {
            _problemDetailsFactory = problemDetailsFactory;
        }
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (httpContext.Response.HasStarted)
                return false;

            var statusCode = GetStatusCode(exception);
            var title = GetTitle(exception);

            var problemDetails = _problemDetailsFactory.CreateProblemDetails(
                httpContext,
                statusCode: statusCode,
                title: title,
                detail: exception.Message,
                instance: httpContext.Request.Path
            );

            httpContext.Response.Clear();
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";
            await Results.Problem(problemDetails).ExecuteAsync(httpContext);

            return true;
        }

        private static int GetStatusCode(Exception ex)
        {
            return ex switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                AuthenticationException => StatusCodes.Status401Unauthorized,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                InvalidOperationException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };
        }

        private static string GetTitle(Exception ex)
        {
            return ex switch
            {
                ArgumentException => "Bad Request",
                KeyNotFoundException => "Not Found",
                AuthenticationException => "Unauthorized",
                UnauthorizedAccessException => "Forbidden",
                InvalidOperationException => "Conflict",
                _ => "Internal Server Error"
            };
        }
    }
}

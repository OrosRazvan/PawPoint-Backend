using PawPoint.ApiServices.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PawPoint.Tests
{
    public class ExceptionHandlingMiddlewareTest
    {
        [Fact]
        public void NoException_Test()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.StatusCode = StatusCodes.Status200OK;

            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.NotEqual(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Exception400_Test()
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddOptions()
                .AddControllers()
                .Services
                .AddProblemDetails()
                .BuildServiceProvider();

            var factory = services.GetRequiredService<ProblemDetailsFactory>();
            var handler = new ExceptionHandlingMiddleware(factory);

            var ctx = new DefaultHttpContext();
            ctx.RequestServices = services;
            ctx.Request.Path = "/test";

            var handled = await handler.TryHandleAsync(ctx, new ArgumentException("Invalid argument"), CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
            Assert.StartsWith("application/problem+json", ctx.Response.ContentType);
        }
    }
}

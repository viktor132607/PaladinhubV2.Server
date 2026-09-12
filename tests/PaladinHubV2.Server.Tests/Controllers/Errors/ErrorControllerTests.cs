using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Controllers.Errors;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Errors;

public sealed class ErrorControllerTests
{
    [Fact]
    public void NotFound404_ReturnsExpectedProblemDetails()
    {
        var controller = CreateController("/missing");

        IActionResult result = controller.NotFound404();

        ObjectResult problemResult = Assert.IsType<ObjectResult>(result);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Resource not found", problem.Title);
        Assert.Equal("The requested resource could not be found.", problem.Detail);
        Assert.Equal("/missing", problem.Instance);
    }

    [Fact]
    public void InternalServerError_ReturnsExpectedProblemDetails()
    {
        var controller = CreateController("/boom");

        IActionResult result = controller.InternalServerError();

        ObjectResult problemResult = Assert.IsType<ObjectResult>(result);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("Internal server error", problem.Title);
        Assert.Equal("An unexpected server error occurred.", problem.Detail);
        Assert.Equal("/boom", problem.Instance);
    }

    [Fact]
    public void LegacyHomeError_IncludesTraceIdentifier()
    {
        var controller = CreateController("/Home/Error", "trace-123");

        IActionResult result = controller.LegacyHomeError();

        ObjectResult problemResult = Assert.IsType<ObjectResult>(result);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("trace-123", problem.Extensions["requestId"]);
    }

    private static ErrorController CreateController(
        string path,
        string? traceIdentifier = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (traceIdentifier is not null)
        {
            context.TraceIdentifier = traceIdentifier;
        }

        return new ErrorController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }
}

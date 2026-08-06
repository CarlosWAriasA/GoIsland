using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.DTOs.Common;

public static class ApiProblemDetailsFactory
{
    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string title,
        string message)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = message,
            Instance = context.Request.Path
        };
        AddCommonExtensions(problem, context, message);
        return problem;
    }

    public static ValidationProblemDetails CreateValidation(
        HttpContext context,
        IDictionary<string, string[]> errors,
        string message)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos no válidos",
            Detail = message,
            Instance = context.Request.Path
        };
        AddCommonExtensions(problem, context, message);
        return problem;
    }

    private static void AddCommonExtensions(
        ProblemDetails problem,
        HttpContext context,
        string message)
    {
        problem.Extensions["message"] = message;
        problem.Extensions["correlationId"] = context.TraceIdentifier;
    }
}

using FluentResults;
using Microsoft.AspNetCore.Mvc;

namespace Tutor.Api.Utilities;

public static class ResultHttpExtensions
{
    public static IActionResult ToHttpError(this Result result, ControllerBase controller) =>
        MapError(result.Errors, controller);

    public static IActionResult ToHttpError<T>(this Result<T> result, ControllerBase controller) =>
        MapError(result.Errors, controller);

    private static IActionResult MapError(IReadOnlyList<IError> errors, ControllerBase controller)
    {
        if (errors.Count == 0 ||
            !errors[0].Metadata.TryGetValue("MethodName", out var methodName))
        {
            return controller.BadRequest(errors);
        }

        return methodName switch
        {
            "Unauthorized" => controller.Unauthorized(errors),
            _ => controller.BadRequest(errors)
        };
    }
}
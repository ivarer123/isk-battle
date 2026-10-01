namespace IskBattle.Api;

/// <summary>ProblemDetails with a stable <c>code</c> the frontend maps to Swedish text.</summary>
internal static class Problems
{
    public static IResult Of(int status, string code, string detail, IDictionary<string, object?>? extra = null)
    {
        var extensions = new Dictionary<string, object?>(extra ?? new Dictionary<string, object?>()) { ["code"] = code };
        return Results.Problem(statusCode: status, detail: detail, extensions: extensions);
    }

    public static IResult BadRequest(string code, string detail) => Of(StatusCodes.Status400BadRequest, code, detail);

    public static IResult NotFound(string code, string detail) => Of(StatusCodes.Status404NotFound, code, detail);

    public static IResult Forbidden(string code, string detail) => Of(StatusCodes.Status403Forbidden, code, detail);

    public static IResult Conflict() =>
        Of(StatusCodes.Status409Conflict, "concurrent_update", "The league was changed by someone else. Try again.");
}

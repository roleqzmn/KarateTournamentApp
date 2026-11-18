using System.ComponentModel.DataAnnotations;

namespace KarateTournament.Api.Endpoints;

internal static class RequestValidation
{
    public static IResult? GetProblem(object request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
            return null;

        var errors = results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (Member: member, Message: result.ErrorMessage ?? "Invalid value.")))
            .GroupBy(error => error.Member)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
        return Results.ValidationProblem(errors);
    }
}

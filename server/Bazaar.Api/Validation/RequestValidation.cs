using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Validation;

public static class RequestValidation
{
    /// <summary>
    /// Runs DataAnnotations validation for a single object. Returns false and a
    /// field -> messages map suitable for <see cref="Microsoft.AspNetCore.Http.Results.ValidationProblem"/>.
    /// </summary>
    public static bool TryValidate(object model, out Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        var ok = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        errors = results
            .SelectMany(
                r => r.MemberNames.DefaultIfEmpty(string.Empty),
                (r, member) => (Member: member, Message: r.ErrorMessage ?? "Invalid value."))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).Distinct().ToArray());

        return ok;
    }

    /// <summary>Validate a request and any nested child objects, prefixing child errors with a path.</summary>
    public static bool TryValidateGraph(
        object model,
        IEnumerable<(string prefix, object child)> children,
        out Dictionary<string, string[]> errors)
    {
        var ok = TryValidate(model, out errors);

        foreach (var (prefix, child) in children)
        {
            if (TryValidate(child, out var childErrors)) continue;
            ok = false;
            foreach (var (key, messages) in childErrors)
            {
                var path = string.IsNullOrEmpty(key) ? prefix : $"{prefix}.{key}";
                errors[path] = messages;
            }
        }

        return ok;
    }
}

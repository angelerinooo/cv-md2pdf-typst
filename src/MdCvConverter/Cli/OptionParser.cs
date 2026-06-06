namespace MdCvConverter.Cli;

public sealed class ParseResult
{
    public bool Success { get; init; }
    public bool ShowHelp { get; init; }
    public ConverterOptions? Options { get; init; }
    public List<string> Errors { get; } = new();
}

public static class OptionParser
{
    private static readonly HashSet<string> KnownOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "input",
        "output",
        "template",
        "author",
        "position",
        "help",
        "h"
    };

    public static ParseResult Parse(string[] args)
    {
        var result = new ParseResult();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase))
            {
                values["help"] = "true";
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                result.Errors.Add($"Unexpected argument: {token}");
                continue;
            }

            var key = token[2..];
            if (!KnownOptions.Contains(key))
            {
                result.Errors.Add($"Unknown option: --{key}");
                continue;
            }

            if (string.Equals(key, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "h", StringComparison.OrdinalIgnoreCase))
            {
                values["help"] = "true";
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result.Errors.Add($"Missing value for option --{key}");
                continue;
            }

            if (values.ContainsKey(key))
            {
                result.Errors.Add($"Duplicate option: --{key}");
                continue;
            }

            values[key] = args[++i];
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        if (values.ContainsKey("help"))
        {
            return new ParseResult
            {
                Success = true,
                ShowHelp = true
            };
        }

        var input = GetRequired(values, "input", result.Errors);
        var output = GetRequired(values, "output", result.Errors);
        var template = values.TryGetValue("template", out var templateValue)
            ? templateValue
            : ResolveDefaultTemplatePath();

        values.TryGetValue("author", out var author);
        values.TryGetValue("position", out var position);

        if (result.Errors.Count > 0)
        {
            return result;
        }

        return new ParseResult
        {
            Success = true,
            Options = new ConverterOptions
            {
                InputPath = input!,
                OutputPdfPath = output!,
                TemplatePath = template,
                Author = author,
                Position = position
            }
        };
    }

    private static string? GetRequired(IDictionary<string, string> values, string key, ICollection<string> errors)
    {
        if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        errors.Add($"Required option --{key} was not provided");
        return null;
    }

    private static string ResolveDefaultTemplatePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "templates", "default", "cv.typ"),
            Path.Combine(AppContext.BaseDirectory, "templates", "default", "cv.typ"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates", "default", "cv.typ"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }
}

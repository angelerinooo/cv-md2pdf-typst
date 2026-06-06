using System.Text.RegularExpressions;

namespace MdCvConverter.Services;

public sealed class TemplateRenderer
{
    private static readonly Regex ImportOrIncludePattern = new(
        "(#(?:import|include)\\s+\")([^\"]+)(\")",
        RegexOptions.Compiled);

    public async Task<string> RenderAsync(
        string templatePath,
        string author,
        string position,
        string bodyTypst,
        string sidebarComponentsTypst,
        string? outputDirectory = null)
    {
        var template = await File.ReadAllTextAsync(templatePath);
        var templateDirectory = Path.GetDirectoryName(Path.GetFullPath(templatePath)) ?? Environment.CurrentDirectory;
        var renderedFileDirectory = outputDirectory is null
            ? templateDirectory
            : Path.GetFullPath(outputDirectory);
        var resolvedTemplate = ResolveRelativeTypstPaths(template, templateDirectory, renderedFileDirectory);

        return resolvedTemplate
            .Replace("{{AUTHOR}}", author, StringComparison.Ordinal)
            .Replace("{{POSITION}}", position, StringComparison.Ordinal)
            .Replace("{{ASSETS_ROOT}}", "/assets", StringComparison.Ordinal)
            .Replace("{{SIDEBAR_COMPONENTS}}", sidebarComponentsTypst, StringComparison.Ordinal)
            .Replace("{{BODY}}", bodyTypst, StringComparison.Ordinal);
    }

    private static string ResolveRelativeTypstPaths(string template, string templateDirectory, string renderedFileDirectory)
    {
        return ImportOrIncludePattern.Replace(template, match =>
        {
            var directivePrefix = match.Groups[1].Value;
            var referencedPath = match.Groups[2].Value;
            var directiveSuffix = match.Groups[3].Value;

            if (ShouldKeepAsIs(referencedPath))
            {
                return match.Value;
            }

            var sourcePath = Path.GetFullPath(Path.Combine(templateDirectory, referencedPath));
            var relativePath = Path.GetRelativePath(renderedFileDirectory, sourcePath)
                .Replace("\\", "/", StringComparison.Ordinal);

            return $"{directivePrefix}{relativePath}{directiveSuffix}";
        });
    }

    private static bool ShouldKeepAsIs(string path)
    {
        return Path.IsPathRooted(path)
            || path.StartsWith("@", StringComparison.Ordinal)
            || path.Contains("://", StringComparison.Ordinal)
            || path.StartsWith("/", StringComparison.Ordinal);
    }
}

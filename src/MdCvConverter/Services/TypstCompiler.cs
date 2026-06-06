using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MdCvConverter.Services;

public sealed class CompileResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class TypstCompiler
{
    public async Task<CompileResult> CompileAsync(string inputTypPath, string outputPdfPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "typst",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("compile");
        var rootPath = ResolveProjectRoot(inputTypPath);
        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            startInfo.ArgumentList.Add("--root");
            startInfo.ArgumentList.Add(rootPath);
        }

        foreach (var fontPath in ResolveFontPaths())
        {
            startInfo.ArgumentList.Add("--font-path");
            startInfo.ArgumentList.Add(fontPath);
        }
        startInfo.ArgumentList.Add(inputTypPath);
        startInfo.ArgumentList.Add(outputPdfPath);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new CompileResult
                {
                    Success = false,
                    ErrorMessage = "Failed to start Typst process."
                };
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var outText = await stdout;
            var errText = await stderr;

            if (process.ExitCode != 0)
            {
                var msg = new StringBuilder();
                msg.AppendLine("Typst compilation failed.");
                if (!string.IsNullOrWhiteSpace(outText))
                {
                    msg.AppendLine(outText.Trim());
                }

                if (!string.IsNullOrWhiteSpace(errText))
                {
                    msg.AppendLine(errText.Trim());
                }

                return new CompileResult
                {
                    Success = false,
                    ErrorMessage = msg.ToString().TrimEnd()
                };
            }

            return new CompileResult { Success = true };
        }
        catch (Win32Exception)
        {
            return new CompileResult
            {
                Success = false,
                ErrorMessage = "Typst CLI was not found. Install Typst and ensure 'typst' is on your PATH."
            };
        }
    }

    private static IEnumerable<string> ResolveFontPaths()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "assets", "fonts"),
            Path.Combine(AppContext.BaseDirectory, "assets", "fonts"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "fonts"))
        };

        return candidates
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveProjectRoot(string inputTypPath)
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(Path.GetFullPath(inputTypPath))
        };

        foreach (var candidate in candidates)
        {
            var root = FindPackageRoot(candidate);
            if (!string.IsNullOrWhiteSpace(root))
            {
                return root;
            }
        }

        return Path.GetDirectoryName(Path.GetFullPath(inputTypPath)) ?? Environment.CurrentDirectory;
    }

    private static string? FindPackageRoot(string? startingDirectory)
    {
        if (string.IsNullOrWhiteSpace(startingDirectory))
        {
            return null;
        }

        var directory = new DirectoryInfo(startingDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "templates")) &&
                Directory.Exists(Path.Combine(directory.FullName, "assets")))
            {
                return directory.FullName;
            }

            if (File.Exists(Path.Combine(directory.FullName, "MdCvConverter.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MdCvConverter.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

using MdCvConverter.Cli;
using MdCvConverter.Services;

var parseResult = OptionParser.Parse(args);
if (parseResult.ShowHelp)
{
    PrintUsage();
    return 0;
}

if (!parseResult.Success || parseResult.Options is null)
{
    foreach (var error in parseResult.Errors)
    {
        Console.Error.WriteLine($"Error: {error}");
    }

    PrintUsage();
    return 1;
}

var options = parseResult.Options;
var inputPath = Path.GetFullPath(options.InputPath);
var outputPdfPath = Path.GetFullPath(options.OutputPdfPath);
var templatePath = Path.GetFullPath(options.TemplatePath);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 1;
}

if (!File.Exists(templatePath))
{
    Console.Error.WriteLine($"Template file not found: {templatePath}");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath) ?? ".");

var markdown = await File.ReadAllTextAsync(inputPath);
var bodyTypst = MarkdownToTypstConverter.Convert(markdown, options.NoHyphenate);
var metadata = MarkdownToTypstConverter.ExtractMetadata(markdown);
var resolvedAuthor = options.Author ?? metadata.Author ?? "Curriculum Vitae";
var resolvedPosition = options.Position ?? metadata.Position ?? string.Empty;
var sidebarComponentsTypst = metadata.SidebarComponentsTypst ?? string.Empty;
var intermediateTypPath = Path.ChangeExtension(outputPdfPath, ".typ");

var templateRenderer = new TemplateRenderer();
var fullTypst = await templateRenderer.RenderAsync(
    templatePath,
    resolvedAuthor,
    resolvedPosition,
    bodyTypst,
    sidebarComponentsTypst,
    Path.GetDirectoryName(intermediateTypPath));

await File.WriteAllTextAsync(intermediateTypPath, fullTypst);

var compiler = new TypstCompiler();
var compile = await compiler.CompileAsync(intermediateTypPath, outputPdfPath);
if (!compile.Success)
{
    Console.Error.WriteLine(compile.ErrorMessage);
    return 1;
}

Console.WriteLine($"PDF generated: {outputPdfPath}");
Console.WriteLine($"Intermediate Typst file: {intermediateTypPath}");
return 0;

static void PrintUsage()
{
    Console.WriteLine("Usage: MdCvConverter --input <file.md> --output <file.pdf> --template <file.typ> [--no-hyphenate] [--help|-h]");
}

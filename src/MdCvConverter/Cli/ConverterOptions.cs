namespace MdCvConverter.Cli;

public sealed class ConverterOptions
{
    public required string InputPath { get; init; }
    public required string OutputPdfPath { get; init; }
    public required string TemplatePath { get; init; }
    public string? Author { get; init; }
    public string? Position { get; init; }
    public bool NoHyphenate { get; init; }
}

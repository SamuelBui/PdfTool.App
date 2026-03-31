namespace PdfTool.App.Models;

public class PdfCompressionOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int CompressionLevel { get; set; } = 50;
}

namespace PdfTool.App.Models;

public class PdfCompressionRunSummary
{
    public string MethodUsed { get; set; } = string.Empty;
    public int RasterizedPageCount { get; set; }
    public int TotalPageCount { get; set; }
    public int GrayscalePageCount { get; set; }
    public string ColorMode { get; set; } = string.Empty;
}

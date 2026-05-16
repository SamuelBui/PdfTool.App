namespace PdfTool.App.Models;

public class PdfCompressionPlan
{
    public PdfCompressionStrategy Strategy { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public int MixedPageTargetDpi { get; set; }
    public int ImageHeavyPageTargetDpi { get; set; }
    public int MixedPageJpegQuality { get; set; }
    public int ImageHeavyPageJpegQuality { get; set; }
    public bool PreferGrayscaleForLowColorPages { get; set; }
    public bool RequireScanLikePages { get; set; }
    public bool AllowSelectiveRasterization { get; set; }
    public double MixedPageBudgetRatio { get; set; }
    public double ImageHeavyPageBudgetRatio { get; set; }
    public double MinimumSavingsRatio { get; set; }
}

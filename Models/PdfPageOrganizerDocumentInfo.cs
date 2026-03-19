namespace PdfTool.App.Models;

public class PdfPageOrganizerDocumentInfo
{
    public int PageCount { get; set; }
    public List<PdfPageOrganizerItem> Pages { get; set; } = new();
}

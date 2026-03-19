using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFiumSharp;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfThumbnailService : IPdfThumbnailService
{
    private static readonly object RenderSync = new();

    public IReadOnlyList<PdfPageThumbnailResult> RenderDocumentThumbnails(string filePath, int width, int height, int? maxPages = null, string? password = null)
    {
        var results = new List<PdfPageThumbnailResult>();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || width <= 0 || height <= 0)
        {
            return results;
        }

        lock (RenderSync)
        {
            try
            {
                using var document = new PdfDocument(filePath, password ?? string.Empty);
                var pageCount = maxPages.HasValue
                    ? Math.Min(document.Pages.Count, maxPages.Value)
                    : document.Pages.Count;

                for (var index = 0; index < pageCount; index++)
                {
                    using var page = document.Pages[index];
                    var image = RenderingExtensionsWpf.CreateImageSource(
                        page,
                        width,
                        height,
                        true,
                        page.Orientation,
                        RenderingFlags.None);

                    if (image is Freezable freezable && freezable.CanFreeze)
                    {
                        freezable.Freeze();
                    }

                    results.Add(new PdfPageThumbnailResult
                    {
                        PageNumber = index + 1,
                        Thumbnail = image
                    });
                }
            }
            catch
            {
                return results;
            }
        }

        return results;
    }
}

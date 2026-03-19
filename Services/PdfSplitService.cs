using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfSplitService : IPdfSplitService
{
    public PdfPageOrganizerDocumentInfo LoadDocumentInfo(string inputPath)
    {
        var result = new PdfPageOrganizerDocumentInfo();

        if (!FileAccessHelper.TryValidateReadableFile(inputPath, out _))
        {
            return result;
        }

        using var source = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        result.PageCount = source.PageCount;

        for (var index = 0; index < source.PageCount; index++)
        {
            var page = source.Pages[index];
            result.Pages.Add(new PdfPageOrganizerItem
            {
                PageNumber = index + 1,
                SourcePageNumber = index + 1,
                SourceFilePath = inputPath,
                WidthPoints = page.Width.Point,
                HeightPoints = page.Height.Point,
                Rotation = page.Rotate
            });
        }

        return result;
    }

    public OperationResult SplitEveryPage(string inputPath, string outputFolder)
        => ExtractSelectedPages(new PdfSplitOperationOptions
        {
            InputPath = inputPath,
            OutputFolder = outputFolder,
            SelectedPages = LoadDocumentInfo(inputPath).Pages.Select(page => page.PageNumber).ToList(),
            OutputStrategy = SplitOutputStrategy.SeparateFiles
        });

    public OperationResult SplitByRanges(string inputPath, string outputFolder, string pageRanges)
    {
        try
        {
            if (!FileAccessHelper.TryValidateReadableFile(inputPath, out var inputError))
            {
                return OperationResult.Fail(inputError);
            }

            if (!FileAccessHelper.TryValidateOutputFolder(outputFolder, out var folderError))
            {
                return OperationResult.Fail(folderError);
            }

            Directory.CreateDirectory(outputFolder);

            using var source = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var parsedRanges = PageRangeParser.Parse(pageRanges, source.PageCount);
            var outputs = new List<string>();
            var baseName = Path.GetFileNameWithoutExtension(inputPath);

            foreach (var range in parsedRanges)
            {
                using var target = new PdfDocument();
                foreach (var pageNumber in range)
                {
                    target.AddPage(source.Pages[pageNumber - 1]);
                }

                var outputPath = Path.Combine(outputFolder, CreateRangeFileName(baseName, range));
                target.Save(outputPath);
                outputs.Add(outputPath);
            }

            return new OperationResult
            {
                Success = true,
                Message = $"Split by ranges completed. {outputs.Count} file(s) created.",
                OutputPaths = outputs
            };
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Split by ranges failed: {ex.Message}");
        }
    }

    public OperationResult ExtractSelectedPages(PdfSplitOperationOptions options)
    {
        try
        {
            if (!TryValidateOptions(options, requireSelection: true, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            Directory.CreateDirectory(options.OutputFolder);

            var selectedPages = options.SelectedPages
                .Distinct()
                .ToList();

            using var source = PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Import);
            var baseName = Path.GetFileNameWithoutExtension(options.InputPath);
            var outputs = new List<string>();

            switch (options.OutputStrategy)
            {
                case SplitOutputStrategy.SeparateFiles:
                    foreach (var pageNumber in selectedPages)
                    {
                        using var target = new PdfDocument();
                        var page = target.AddPage(source.Pages[pageNumber - 1]);
                        ApplyConfiguredRotation(page, pageNumber, options);

                        var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_page_{pageNumber:000}.pdf");
                        target.Save(outputPath);
                        outputs.Add(outputPath);
                    }

                    break;

                case SplitOutputStrategy.RangeFiles:
                    foreach (var range in BuildContiguousRanges(selectedPages))
                    {
                        using var target = new PdfDocument();
                        foreach (var pageNumber in range)
                        {
                            var page = target.AddPage(source.Pages[pageNumber - 1]);
                            ApplyConfiguredRotation(page, pageNumber, options);
                        }

                        var outputPath = Path.Combine(options.OutputFolder, CreateRangeFileName(baseName, range));
                        target.Save(outputPath);
                        outputs.Add(outputPath);
                    }

                    break;

                default:
                    using (var target = new PdfDocument())
                    {
                        foreach (var pageNumber in selectedPages)
                        {
                            var page = target.AddPage(source.Pages[pageNumber - 1]);
                            ApplyConfiguredRotation(page, pageNumber, options);
                        }

                        var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_selected.pdf");
                        target.Save(outputPath);
                        outputs.Add(outputPath);
                    }

                    break;
            }

            return new OperationResult
            {
                Success = true,
                Message = $"Extract completed. {outputs.Count} file(s) created.",
                OutputPaths = outputs
            };
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Extract failed: {ex.Message}");
        }
    }

    public OperationResult RemoveSelectedPages(PdfSplitOperationOptions options)
    {
        try
        {
            if (!TryValidateOptions(options, requireSelection: true, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            Directory.CreateDirectory(options.OutputFolder);

            using var source = PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Import);
            var selectedPages = options.SelectedPages.Distinct().ToHashSet();
            var pageSequence = options.PageSequence.Count > 0
                ? options.PageSequence
                : Enumerable.Range(1, source.PageCount).ToList();
            var remainingPages = pageSequence
                .Where(page => !selectedPages.Contains(page))
                .ToList();

            if (remainingPages.Count == 0)
            {
                return OperationResult.Fail("Cannot remove all pages. At least one page must remain.");
            }

            using var target = new PdfDocument();
            foreach (var pageNumber in remainingPages)
            {
                var page = target.AddPage(source.Pages[pageNumber - 1]);
                ApplyConfiguredRotation(page, pageNumber, options);
            }

            var baseName = Path.GetFileNameWithoutExtension(options.InputPath);
            var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_without_selected.pdf");
            target.Save(outputPath);

            return new OperationResult
            {
                Success = true,
                Message = $"Remove selected pages completed. {remainingPages.Count} page(s) remain.",
                OutputPath = outputPath,
                OutputPaths = new List<string> { outputPath }
            };
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Remove selected pages failed: {ex.Message}");
        }
    }

    public OperationResult RotateSelectedPages(PdfSplitOperationOptions options)
    {
        try
        {
            if (!TryValidateOptions(options, requireSelection: true, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            Directory.CreateDirectory(options.OutputFolder);

            using var source = PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Import);
            using var target = new PdfDocument();
            var selectedPages = options.SelectedPages.Distinct().ToHashSet();

            for (var index = 0; index < source.PageCount; index++)
            {
                var page = target.AddPage(source.Pages[index]);
                var pageNumber = index + 1;

                if (selectedPages.Contains(pageNumber))
                {
                    var rotated = (page.Rotate + options.RotationDelta) % 360;
                    page.Rotate = rotated < 0 ? rotated + 360 : rotated;
                }
            }

            var baseName = Path.GetFileNameWithoutExtension(options.InputPath);
            var suffix = options.RotationDelta >= 0 ? "rotated_right" : "rotated_left";
            var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_{suffix}.pdf");
            target.Save(outputPath);

            return new OperationResult
            {
                Success = true,
                Message = "Rotate selected pages completed.",
                OutputPath = outputPath,
                OutputPaths = new List<string> { outputPath }
            };
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Rotate selected pages failed: {ex.Message}");
        }
    }

    private static void ApplyConfiguredRotation(PdfPage page, int pageNumber, PdfSplitOperationOptions options)
    {
        if (options.PageRotations.TryGetValue(pageNumber, out var rotation))
        {
            page.Rotate = rotation;
        }
    }

    private static bool TryValidateOptions(PdfSplitOperationOptions options, bool requireSelection, out string validationError)
    {
        if (!FileAccessHelper.TryValidateReadableFile(options.InputPath, out var inputError))
        {
            validationError = inputError;
            return false;
        }

        if (!FileAccessHelper.TryValidateOutputFolder(options.OutputFolder, out var outputError))
        {
            validationError = outputError;
            return false;
        }

        if (requireSelection && (options.SelectedPages == null || options.SelectedPages.Count == 0))
        {
            validationError = "Please select at least one page.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static List<List<int>> BuildContiguousRanges(IReadOnlyList<int> sortedPages)
    {
        var ranges = new List<List<int>>();
        if (sortedPages.Count == 0)
        {
            return ranges;
        }

        var currentRange = new List<int> { sortedPages[0] };
        for (var i = 1; i < sortedPages.Count; i++)
        {
            if (sortedPages[i] == sortedPages[i - 1] + 1)
            {
                currentRange.Add(sortedPages[i]);
                continue;
            }

            ranges.Add(currentRange);
            currentRange = new List<int> { sortedPages[i] };
        }

        ranges.Add(currentRange);
        return ranges;
    }

    private static string CreateRangeFileName(string baseName, IReadOnlyList<int> range)
    {
        if (range.Count == 1)
        {
            return $"{baseName}_p{range[0]}.pdf";
        }

        return $"{baseName}_p{range.First()}-p{range.Last()}.pdf";
    }
}

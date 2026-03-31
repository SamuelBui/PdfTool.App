using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PdfTool.App.Commands;
using PdfTool.App.Helpers;
using PdfTool.App.Models;
using PdfTool.App.Services;
using WinForms = System.Windows.Forms;

namespace PdfTool.App.ViewModels;

public class CompressViewModel : BaseViewModel
{
    private readonly IPdfCompressionService _service;
    private readonly IPdfCompressionInspectorService _compressionInspectorService;
    private readonly IAppStatusService _statusService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly Dictionary<string, PdfCompressionInspectionResult> _inspectionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inspectionPending = new(StringComparer.OrdinalIgnoreCase);
    private string _inputPath = string.Empty;
    private string _outputPath = string.Empty;
    private string _sharedOutputFolder = string.Empty;
    private string _statusMessage = "Choose one or more PDF files, then compress them.";
    private bool _lastOperationSucceeded;
    private bool _isBusy;
    private bool _isSingleMode = true;
    private int _compressionLevel = 50;
    private string _compressionProfileLabel = "Balanced profile";
    private string _compressionHint = "The app inspects the PDF first, keeps text/vector pages when possible, and selectively optimizes heavier image pages.";
    private string _previewContextLabel = "Single file";
    private string _previewFileName = "-";
    private string _previewInputPath = "-";
    private string _previewOriginalSize = "-";
    private string _previewOutputPath = "-";
    private string _previewResultSize = "-";
    private string _previewSavedText = "Run compression to compare file size.";
    private string _previewPreflightSummary = "Choose a PDF to inspect before compression.";
    private string _previewRiskWarnings = "No major risk detected.";
    private string _previewMethodUsed = "Not run yet.";
    private string _previewRasterizedPages = "-";
    private string _previewColorMode = "-";
    private int _previewInspectionRequestId;
    private bool _cancelBatchRequested;
    private PdfCompressionRunSummary? _singleRunSummary;
    private CompressBatchItem? _selectedBatchItem;

    public CompressViewModel(
        IPdfCompressionService service,
        IPdfCompressionInspectorService compressionInspectorService,
        IAppStatusService statusService,
        IRecentFilesService recentFilesService)
    {
        _service = service;
        _compressionInspectorService = compressionInspectorService;
        _statusService = statusService;
        _recentFilesService = recentFilesService;
        BatchItems = new ObservableCollection<CompressBatchItem>();
        BatchItems.CollectionChanged += BatchItems_CollectionChanged;
        RecentFiles = recentFilesService.Files;

        BrowseInputCommand = new RelayCommand(BrowseInput, () => !IsBusy && IsSingleMode);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, () => !IsBusy && IsSingleMode);
        LoadMultipleFilesCommand = new RelayCommand(LoadMultipleFiles, () => !IsBusy && !IsSingleMode);
        AddBatchFilesCommand = new RelayCommand(AddBatchFiles, () => !IsBusy && !IsSingleMode);
        RemoveSelectedBatchItemCommand = new RelayCommand(RemoveSelectedBatchItem, () => !IsBusy && SelectedBatchItem != null);
        SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder, () => !IsBusy && !IsSingleMode && BatchItems.Count > 0);
        RetryFailedItemsCommand = new RelayCommand(RetryFailedItems, CanRetryFailedItems);
        CancelBatchCommand = new RelayCommand(RequestBatchCancel, () => IsBusy && IsBatchMode);
        CompressCommand = new RelayCommand(CompressPdf, CanCompress);

        UpdateCompressionProfile();
        RefreshCompressionInfo();
    }

    public string InputPath
    {
        get => _inputPath;
        set
        {
            if (!SetProperty(ref _inputPath, value))
            {
                return;
            }

            _singleRunSummary = null;
            if (string.IsNullOrWhiteSpace(OutputPath) && !string.IsNullOrWhiteSpace(value))
            {
                OutputPath = FileNameHelper.CreateCompressedFilePath(value);
            }

            RefreshCompressionInfo();
            RefreshCommands();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                _singleRunSummary = null;
                RefreshCompressionInfo();
                RefreshCommands();
            }
        }
    }

    public string SharedOutputFolder
    {
        get => _sharedOutputFolder;
        set
        {
            if (SetProperty(ref _sharedOutputFolder, value))
            {
                RefreshCompressionInfo();
                RefreshCommands();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool LastOperationSucceeded
    {
        get => _lastOperationSucceeded;
        set => SetProperty(ref _lastOperationSucceeded, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool IsSingleMode
    {
        get => _isSingleMode;
        set
        {
            if (SetProperty(ref _isSingleMode, value))
            {
                OnPropertyChanged(nameof(IsBatchMode));
                RefreshCompressionInfo();
                RefreshCommands();
            }
        }
    }

    public bool IsBatchMode
    {
        get => !IsSingleMode;
        set
        {
            if (value)
            {
                IsSingleMode = false;
            }
        }
    }

    public int CompressionLevel
    {
        get => _compressionLevel;
        set
        {
            if (SetProperty(ref _compressionLevel, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(IsPreset25));
                OnPropertyChanged(nameof(IsPreset50));
                OnPropertyChanged(nameof(IsPreset70));
                OnPropertyChanged(nameof(IsCustomCompression));
                UpdateCompressionProfile();
                RefreshCompressionInfo();
                RefreshCommands();
            }
        }
    }

    public bool IsPreset25
    {
        get => CompressionLevel == 25;
        set
        {
            if (value)
            {
                CompressionLevel = 25;
            }
        }
    }

    public bool IsPreset50
    {
        get => CompressionLevel == 50;
        set
        {
            if (value)
            {
                CompressionLevel = 50;
            }
        }
    }

    public bool IsPreset70
    {
        get => CompressionLevel == 70;
        set
        {
            if (value)
            {
                CompressionLevel = 70;
            }
        }
    }

    public bool IsCustomCompression
    {
        get => CompressionLevel != 25 && CompressionLevel != 50 && CompressionLevel != 70;
        set
        {
            if (value && !IsCustomCompression)
            {
                CompressionLevel = 60;
            }
        }
    }

    public string CompressionProfileLabel
    {
        get => _compressionProfileLabel;
        set => SetProperty(ref _compressionProfileLabel, value);
    }

    public string CompressionHint
    {
        get => _compressionHint;
        set => SetProperty(ref _compressionHint, value);
    }

    public string PreviewContextLabel
    {
        get => _previewContextLabel;
        set => SetProperty(ref _previewContextLabel, value);
    }

    public string PreviewFileName
    {
        get => _previewFileName;
        set => SetProperty(ref _previewFileName, value);
    }

    public string PreviewInputPath
    {
        get => _previewInputPath;
        set => SetProperty(ref _previewInputPath, value);
    }

    public string PreviewOriginalSize
    {
        get => _previewOriginalSize;
        set => SetProperty(ref _previewOriginalSize, value);
    }

    public string PreviewOutputPath
    {
        get => _previewOutputPath;
        set => SetProperty(ref _previewOutputPath, value);
    }

    public string PreviewResultSize
    {
        get => _previewResultSize;
        set => SetProperty(ref _previewResultSize, value);
    }

    public string PreviewSavedText
    {
        get => _previewSavedText;
        set => SetProperty(ref _previewSavedText, value);
    }

    public string PreviewPreflightSummary
    {
        get => _previewPreflightSummary;
        set => SetProperty(ref _previewPreflightSummary, value);
    }

    public string PreviewRiskWarnings
    {
        get => _previewRiskWarnings;
        set => SetProperty(ref _previewRiskWarnings, value);
    }

    public string PreviewMethodUsed
    {
        get => _previewMethodUsed;
        set => SetProperty(ref _previewMethodUsed, value);
    }

    public string PreviewRasterizedPages
    {
        get => _previewRasterizedPages;
        set => SetProperty(ref _previewRasterizedPages, value);
    }

    public string PreviewColorMode
    {
        get => _previewColorMode;
        set => SetProperty(ref _previewColorMode, value);
    }

    public ObservableCollection<CompressBatchItem> BatchItems { get; }
    public ReadOnlyObservableCollection<RecentFileItem> RecentFiles { get; }

    public CompressBatchItem? SelectedBatchItem
    {
        get => _selectedBatchItem;
        set
        {
            if (SetProperty(ref _selectedBatchItem, value))
            {
                RemoveSelectedBatchItemCommand.RaiseCanExecuteChanged();
                RefreshCompressionInfo();
            }
        }
    }

    public RelayCommand BrowseInputCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand LoadMultipleFilesCommand { get; }
    public RelayCommand AddBatchFilesCommand { get; }
    public RelayCommand RemoveSelectedBatchItemCommand { get; }
    public RelayCommand SelectOutputFolderCommand { get; }
    public RelayCommand RetryFailedItemsCommand { get; }
    public RelayCommand CancelBatchCommand { get; }
    public RelayCommand CompressCommand { get; }

    private void BrowseInput()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Select PDF file"
        };

        if (dialog.ShowDialog() == true)
        {
            InputPath = dialog.FileName;
            OutputPath = FileNameHelper.CreateCompressedFilePath(InputPath);
            StatusMessage = "Compression source file selected.";
            LastOperationSucceeded = false;
        }
    }

    private void BrowseOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Save compressed PDF",
            FileName = string.IsNullOrWhiteSpace(InputPath)
                ? "compressed.pdf"
                : Path.GetFileName(FileNameHelper.CreateCompressedFilePath(InputPath))
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
        }
    }

    private void LoadMultipleFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Select PDF files to compress"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadBatchItems(dialog.FileNames, clearExisting: true);
    }

    private void AddBatchFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Add PDF files to compress"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadBatchItems(dialog.FileNames, clearExisting: false);
    }

    private void LoadBatchItems(IEnumerable<string> filePaths, bool clearExisting)
    {
        if (clearExisting)
        {
            BatchItems.Clear();
            SharedOutputFolder = string.Empty;
        }

        var addedItems = new List<CompressBatchItem>();
        foreach (var filePath in filePaths.Where(File.Exists))
        {
            if (BatchItems.Any(item => string.Equals(item.InputPath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new CompressBatchItem
            {
                InputPath = filePath,
                OutputPath = FileNameHelper.CreateCompressedFilePath(filePath),
                OriginalSizeText = FormatBytes(new FileInfo(filePath).Length),
                Status = "Ready"
            };
            BatchItems.Add(item);
            addedItems.Add(item);
        }

        if (SelectedBatchItem == null && BatchItems.Count > 0)
        {
            SelectedBatchItem = BatchItems[0];
        }

        StatusMessage = BatchItems.Count == 0
            ? "No PDF files loaded for compression."
            : $"{BatchItems.Count} file(s) loaded for compression.";
        LastOperationSucceeded = false;
        RefreshCompressionInfo();
        RefreshCommands();
        _ = PrimeInspectionsAsync(addedItems);
    }

    private void RemoveSelectedBatchItem()
    {
        if (SelectedBatchItem == null)
        {
            return;
        }

        var removedFileName = SelectedBatchItem.FileName;
        BatchItems.Remove(SelectedBatchItem);
        SelectedBatchItem = null;
        StatusMessage = $"{removedFileName} removed from compression queue.";
        RefreshCompressionInfo();
        RefreshCommands();
    }

    private void SelectOutputFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        SharedOutputFolder = dialog.SelectedPath;
        foreach (var item in BatchItems)
        {
            item.OutputPath = Path.Combine(SharedOutputFolder, Path.GetFileName(FileNameHelper.CreateCompressedFilePath(item.InputPath)));
            item.ResultSizeText = "-";
            item.LastRunSummary = null;
        }

        StatusMessage = $"Shared output folder set to {SharedOutputFolder}.";
        RefreshCompressionInfo();
    }

    private bool CanCompress()
    {
        if (IsBusy)
        {
            return false;
        }

        return IsSingleMode
            ? !string.IsNullOrWhiteSpace(InputPath) && !string.IsNullOrWhiteSpace(OutputPath)
            : BatchItems.Count > 0 && BatchItems.All(item => !string.IsNullOrWhiteSpace(item.OutputPath));
    }

    private bool CanRetryFailedItems()
        => !IsBusy && BatchItems.Any(item => item.IsFailed);

    private async void CompressPdf()
    {
        if (IsSingleMode)
        {
            await CompressSingleAsync();
            return;
        }

        await CompressBatchAsync();
    }

    private async void RetryFailedItems()
    {
        await CompressBatchAsync(retryFailedOnly: true);
    }

    private void RequestBatchCancel()
    {
        if (!IsBusy || !IsBatchMode)
        {
            return;
        }

        _cancelBatchRequested = true;
        LastOperationSucceeded = false;
        StatusMessage = "Cancel requested. The current file will finish before the batch stops.";
        RefreshCommands();
    }

    private async Task CompressSingleAsync()
    {
        IsBusy = true;
        _statusService.Start("Compressing PDF...");
        _statusService.Report("Applying compression profile...", 40);

        var result = await Task.Run(() => _service.Compress(new PdfCompressionOptions
        {
            InputPath = InputPath,
            OutputPath = OutputPath,
            CompressionLevel = CompressionLevel
        }));

        IsBusy = false;
        LastOperationSucceeded = result.Success;
        StatusMessage = result.Message;

        if (result.Success)
        {
            _recentFilesService.AddFile(InputPath);
            _singleRunSummary = result.CompressionRunSummary;
            _statusService.Complete("Compress PDF completed.");
            OpenOutputFolderIfAvailable();
        }
        else
        {
            _singleRunSummary = null;
            _statusService.Fail("Compress PDF failed.");
        }

        RefreshCompressionInfo();
    }

    private async Task CompressBatchAsync(bool retryFailedOnly = false)
    {
        var workItems = retryFailedOnly
            ? BatchItems.Where(item => item.IsFailed).ToList()
            : BatchItems.ToList();
        if (workItems.Count == 0)
        {
            StatusMessage = retryFailedOnly
                ? "There are no failed items to retry."
                : "No PDF files loaded for compression.";
            LastOperationSucceeded = false;
            return;
        }

        _cancelBatchRequested = false;
        IsBusy = true;
        LastOperationSucceeded = false;
        _statusService.Start(
            retryFailedOnly ? "Retrying failed PDFs..." : "Compressing batch PDFs...",
            isIndeterminate: false,
            progressValue: 0);

        var successCount = 0;
        var failCount = 0;
        var processedCount = 0;
        var canceled = false;

        for (var index = 0; index < workItems.Count; index++)
        {
            if (_cancelBatchRequested)
            {
                canceled = true;
                break;
            }

            var item = workItems[index];
            item.Status = retryFailedOnly ? "Retrying..." : "Processing...";
            _statusService.Report(
                $"Compressing {item.FileName} ({index + 1}/{workItems.Count})",
                (index * 100.0) / workItems.Count);

            var result = await Task.Run(() => _service.Compress(new PdfCompressionOptions
            {
                InputPath = item.InputPath,
                OutputPath = item.OutputPath,
                CompressionLevel = CompressionLevel
            }));

            if (result.Success)
            {
                item.Status = "Success";
                item.IsFailed = false;
                item.LastRunSummary = result.CompressionRunSummary;
                item.ResultSizeText = File.Exists(item.OutputPath)
                    ? FormatBytes(new FileInfo(item.OutputPath).Length)
                    : "-";
                successCount++;
                _recentFilesService.AddFile(item.InputPath);
            }
            else
            {
                item.Status = result.Message;
                item.IsFailed = true;
                item.LastRunSummary = null;
                item.ResultSizeText = "-";
                failCount++;
            }

            processedCount++;
        }

        IsBusy = false;
        LastOperationSucceeded = !canceled && failCount == 0;

        if (canceled)
        {
            var remainingCount = Math.Max(0, workItems.Count - processedCount);
            StatusMessage = $"Batch compression canceled. Success: {successCount}. Failed: {failCount}. Remaining: {remainingCount}.";
            _statusService.Fail("Batch compression canceled.");
        }
        else
        {
            StatusMessage = retryFailedOnly
                ? $"Retry finished. Success: {successCount}. Failed: {failCount}."
                : $"Batch compression finished. Success: {successCount}. Failed: {failCount}.";

            if (failCount == 0)
            {
                _statusService.Complete(retryFailedOnly ? "Retry completed." : "Batch compress completed.");
            }
            else
            {
                _statusService.Fail(retryFailedOnly ? "Retry finished with some failures." : "Batch compress finished with some failures.");
            }
        }

        if (successCount > 0)
        {
            OpenOutputFolderIfAvailable();
        }

        RefreshCompressionInfo();
    }

    private void UpdateCompressionProfile()
    {
        if (CompressionLevel <= 15)
        {
            CompressionProfileLabel = "Safe color profile";
            CompressionHint = "Uses very light selective color image compression. This is the reference range with the most conservative size reduction and the best display quality.";
            return;
        }

        if (CompressionLevel <= 50)
        {
            CompressionProfileLabel = "Balanced color profile";
            CompressionHint = "Scales selectively from the 15% reference profile, so size reduction increases more gradually while images remain in color.";
            return;
        }

        if (CompressionLevel <= 80)
        {
            CompressionProfileLabel = "Strong color profile";
            CompressionHint = "Uses stronger selective color compression up to 80%, but still avoids grayscale so display quality stays closer to the 15% reference.";
            return;
        }

        CompressionProfileLabel = "Strong grayscale profile";
        CompressionHint = "Only above 80% does the app allow grayscale conversion for low-color scan pages, together with stronger image compression.";
    }

    private void RefreshCommands()
    {
        BrowseInputCommand.RaiseCanExecuteChanged();
        BrowseOutputCommand.RaiseCanExecuteChanged();
        LoadMultipleFilesCommand.RaiseCanExecuteChanged();
        AddBatchFilesCommand.RaiseCanExecuteChanged();
        RemoveSelectedBatchItemCommand.RaiseCanExecuteChanged();
        SelectOutputFolderCommand.RaiseCanExecuteChanged();
        RetryFailedItemsCommand.RaiseCanExecuteChanged();
        CancelBatchCommand.RaiseCanExecuteChanged();
        CompressCommand.RaiseCanExecuteChanged();
    }

    private string GetOutputFolderPath()
    {
        if (IsSingleMode)
        {
            return Path.GetDirectoryName(OutputPath) ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(SharedOutputFolder))
        {
            return SharedOutputFolder;
        }

        if (SelectedBatchItem != null)
        {
            return Path.GetDirectoryName(SelectedBatchItem.OutputPath) ?? string.Empty;
        }

        return BatchItems.Count > 0
            ? Path.GetDirectoryName(BatchItems[0].OutputPath) ?? string.Empty
            : string.Empty;
    }

    private void OpenOutputFolderIfAvailable()
    {
        var folderPath = GetOutputFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
    }

    private void RefreshCompressionInfo()
    {
        var previewInputPath = GetCurrentPreviewInputPath();
        if (IsSingleMode)
        {
            var inputExists = File.Exists(InputPath);
            var outputExists = File.Exists(OutputPath);
            var originalBytes = inputExists ? new FileInfo(InputPath).Length : 0;
            var outputBytes = outputExists ? new FileInfo(OutputPath).Length : 0;

            PreviewContextLabel = "Single file";
            PreviewFileName = string.IsNullOrWhiteSpace(InputPath) ? "-" : Path.GetFileName(InputPath);
            PreviewOriginalSize = inputExists ? FormatBytes(originalBytes) : "-";
            PreviewResultSize = outputExists ? FormatBytes(outputBytes) : "-";
            PreviewSavedText = BuildSavedText(originalBytes, outputBytes);
        }
        else
        {
            var item = GetPreviewBatchItem();
            if (item == null)
            {
                PreviewContextLabel = "Batch file";
                PreviewFileName = "-";
                PreviewOriginalSize = "-";
                PreviewResultSize = "-";
                PreviewSavedText = "Select a file in the queue to compare file size.";
            }
            else
            {
                var batchInputExists = File.Exists(item.InputPath);
                var batchOutputExists = File.Exists(item.OutputPath);
                var batchOriginalBytes = batchInputExists ? new FileInfo(item.InputPath).Length : 0;
                var batchOutputBytes = batchOutputExists ? new FileInfo(item.OutputPath).Length : 0;

                PreviewContextLabel = SelectedBatchItem != null ? "Selected batch file" : "First batch file";
                PreviewFileName = item.FileName;
                PreviewOriginalSize = batchInputExists ? FormatBytes(batchOriginalBytes) : item.OriginalSizeText;
                PreviewResultSize = batchOutputExists ? FormatBytes(batchOutputBytes) : item.ResultSizeText;
                PreviewSavedText = BuildSavedText(batchOriginalBytes, batchOutputBytes);
            }
        }

        RefreshExecutionDetails(previewInputPath);
        RefreshPreviewInspectionAsync(previewInputPath);
    }

    private CompressBatchItem? GetPreviewBatchItem()
    {
        if (SelectedBatchItem != null)
        {
            return SelectedBatchItem;
        }

        return BatchItems.Count > 0 ? BatchItems[0] : null;
    }

    private void BatchItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<CompressBatchItem>())
            {
                item.PropertyChanged -= BatchItem_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<CompressBatchItem>())
            {
                item.PropertyChanged += BatchItem_PropertyChanged;
            }
        }

        RefreshCompressionInfo();
        RefreshCommands();
    }

    private void BatchItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshCompressionInfo();
        RefreshCommands();
    }

    private string GetCurrentPreviewInputPath()
    {
        if (IsSingleMode)
        {
            return InputPath;
        }

        return GetPreviewBatchItem()?.InputPath ?? string.Empty;
    }

    private PdfCompressionRunSummary? GetCurrentRunSummary()
    {
        if (IsSingleMode)
        {
            return _singleRunSummary;
        }

        return GetPreviewBatchItem()?.LastRunSummary;
    }

    private void RefreshExecutionDetails(string previewInputPath)
    {
        var runSummary = GetCurrentRunSummary();
        if (runSummary != null)
        {
            PreviewMethodUsed = runSummary.MethodUsed;
            PreviewRasterizedPages = $"{runSummary.RasterizedPageCount} / {runSummary.TotalPageCount} pages";
            PreviewColorMode = runSummary.ColorMode;
            return;
        }

        if (string.IsNullOrWhiteSpace(previewInputPath))
        {
            PreviewMethodUsed = "Not run yet.";
            PreviewRasterizedPages = "-";
            PreviewColorMode = "-";
            return;
        }

        PreviewMethodUsed = "Selective page rasterization + JPEG recompression (planned)";
        if (_inspectionCache.TryGetValue(previewInputPath, out var inspection) && inspection.Success)
        {
            var estimatedRasterizedPages = inspection.Pages.Count(page => page.Category != PdfCompressionPageCategory.TextOrVector);
            PreviewMethodUsed = estimatedRasterizedPages > 0
                ? "Selective page rasterization + JPEG recompression (planned)"
                : "Structure optimization only (planned)";
            PreviewRasterizedPages = estimatedRasterizedPages > 0
                ? $"Estimated up to {estimatedRasterizedPages} / {inspection.PageCount} pages"
                : "0 pages expected";
            PreviewColorMode = estimatedRasterizedPages == 0
                ? "Original color preserved"
                : CompressionLevel > 80
                    ? "Color + grayscale on low-color pages"
                    : "Color only";
            return;
        }
        else
        {
            PreviewRasterizedPages = "Analyzing PDF...";
        }

        PreviewColorMode = CompressionLevel > 80
            ? "Color + grayscale on low-color pages"
            : "Color only";
    }

    private void RefreshPreviewInspectionAsync(string previewInputPath)
    {
        _previewInspectionRequestId++;
        if (string.IsNullOrWhiteSpace(previewInputPath) || !File.Exists(previewInputPath))
        {
            PreviewPreflightSummary = "Choose a PDF to inspect before compression.";
            PreviewRiskWarnings = "No major risk detected.";
            return;
        }

        if (_inspectionCache.TryGetValue(previewInputPath, out var cachedInspection))
        {
            ApplyInspectionPreview(cachedInspection);
            return;
        }

        PreviewPreflightSummary = "Analyzing PDF before compression...";
        PreviewRiskWarnings = "Checking for risky PDF conditions...";
        if (_inspectionPending.Contains(previewInputPath))
        {
            return;
        }

        _inspectionPending.Add(previewInputPath);
        var requestId = _previewInspectionRequestId;
        _ = LoadInspectionAsync(previewInputPath, requestId);
    }

    private async Task LoadInspectionAsync(string inputPath, int requestId)
    {
        var inspection = await Task.Run(() => _compressionInspectorService.Inspect(inputPath));
        _inspectionPending.Remove(inputPath);
        _inspectionCache[inputPath] = inspection;

        if (requestId != _previewInspectionRequestId
            || !string.Equals(inputPath, GetCurrentPreviewInputPath(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyInspectionPreview(inspection);
    }

    private void ApplyInspectionPreview(PdfCompressionInspectionResult inspection)
    {
        if (inspection.Success)
        {
            PreviewPreflightSummary =
                $"{inspection.PageCount} pages · {inspection.TextVectorPageCount} text/vector · {inspection.MixedPageCount} mixed · {inspection.ImageHeavyPageCount} image-heavy.{Environment.NewLine}{inspection.Guidance}";
        }
        else
        {
            PreviewPreflightSummary = string.IsNullOrWhiteSpace(inspection.Message)
                ? "Inspection failed for this PDF."
                : inspection.Message;
        }

        PreviewRiskWarnings = inspection.RiskWarnings.Count > 0
            ? string.Join(Environment.NewLine, inspection.RiskWarnings.Select(warning => $"• {warning}"))
            : "No major risk detected.";

        RefreshExecutionDetails(inspection.FilePath);
    }

    private async Task PrimeInspectionsAsync(IEnumerable<CompressBatchItem> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.InputPath)
                || !File.Exists(item.InputPath)
                || _inspectionCache.ContainsKey(item.InputPath)
                || _inspectionPending.Contains(item.InputPath))
            {
                continue;
            }

            _inspectionPending.Add(item.InputPath);
            var inspection = await Task.Run(() => _compressionInspectorService.Inspect(item.InputPath));
            _inspectionPending.Remove(item.InputPath);
            _inspectionCache[item.InputPath] = inspection;

            if (string.Equals(item.InputPath, GetCurrentPreviewInputPath(), StringComparison.OrdinalIgnoreCase))
            {
                ApplyInspectionPreview(inspection);
            }
        }
    }

    public CompressSessionState CaptureSessionState()
    {
        return new CompressSessionState
        {
            IsSingleMode = IsSingleMode,
            CompressionLevel = CompressionLevel,
            InputPath = InputPath,
            OutputPath = OutputPath,
            SharedOutputFolder = SharedOutputFolder,
            SelectedBatchItemIndex = SelectedBatchItem != null ? BatchItems.IndexOf(SelectedBatchItem) : -1,
            SingleRunSummary = _singleRunSummary == null
                ? null
                : new PdfCompressionRunSummary
                {
                    MethodUsed = _singleRunSummary.MethodUsed,
                    RasterizedPageCount = _singleRunSummary.RasterizedPageCount,
                    TotalPageCount = _singleRunSummary.TotalPageCount,
                    GrayscalePageCount = _singleRunSummary.GrayscalePageCount,
                    ColorMode = _singleRunSummary.ColorMode
                },
            BatchItems = BatchItems.Select(item => new CompressBatchItemSessionState
            {
                InputPath = item.InputPath,
                OutputPath = item.OutputPath,
                Status = item.Status,
                OriginalSizeText = item.OriginalSizeText,
                ResultSizeText = item.ResultSizeText,
                IsFailed = item.IsFailed,
                LastRunSummary = item.LastRunSummary == null
                    ? null
                    : new PdfCompressionRunSummary
                    {
                        MethodUsed = item.LastRunSummary.MethodUsed,
                        RasterizedPageCount = item.LastRunSummary.RasterizedPageCount,
                        TotalPageCount = item.LastRunSummary.TotalPageCount,
                        GrayscalePageCount = item.LastRunSummary.GrayscalePageCount,
                        ColorMode = item.LastRunSummary.ColorMode
                    }
            }).ToList()
        };
    }

    public void RestoreSessionState(CompressSessionState? state)
    {
        if (state == null)
        {
            return;
        }

        IsSingleMode = state.IsSingleMode;
        CompressionLevel = state.CompressionLevel;
        InputPath = state.InputPath ?? string.Empty;
        OutputPath = state.OutputPath ?? string.Empty;
        SharedOutputFolder = state.SharedOutputFolder ?? string.Empty;
        _singleRunSummary = state.SingleRunSummary;

        BatchItems.Clear();
        foreach (var itemState in state.BatchItems)
        {
            BatchItems.Add(new CompressBatchItem
            {
                InputPath = itemState.InputPath,
                OutputPath = itemState.OutputPath,
                Status = string.IsNullOrWhiteSpace(itemState.Status) ? "Pending" : itemState.Status,
                OriginalSizeText = itemState.OriginalSizeText,
                ResultSizeText = itemState.ResultSizeText,
                IsFailed = itemState.IsFailed,
                LastRunSummary = itemState.LastRunSummary
            });
        }

        SelectedBatchItem = state.SelectedBatchItemIndex >= 0 && state.SelectedBatchItemIndex < BatchItems.Count
            ? BatchItems[state.SelectedBatchItemIndex]
            : BatchItems.FirstOrDefault();

        _cancelBatchRequested = false;
        LastOperationSucceeded = false;
        StatusMessage = "Compression session restored.";
        RefreshCompressionInfo();
        RefreshCommands();
    }

    private string BuildSavedText(long originalBytes, long outputBytes)
    {
        if (originalBytes <= 0 || outputBytes <= 0)
        {
            return "Run compression to compare file size.";
        }

        if (outputBytes == originalBytes)
        {
            return "No size change.";
        }

        var deltaBytes = originalBytes - outputBytes;
        var deltaPercent = Math.Abs(deltaBytes) * 100.0 / originalBytes;

        return deltaBytes > 0
            ? $"Saved {FormatBytes(deltaBytes)} ({deltaPercent:0.#}%)."
            : $"Output is {FormatBytes(-deltaBytes)} larger ({deltaPercent:0.#}%).";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double length = bytes;

        while (length >= 1024 && order < sizes.Length - 1)
        {
            order++;
            length /= 1024;
        }

        return $"{length:0.##} {sizes[order]}";
    }
}

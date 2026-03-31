using System.IO;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Models;

public class CompressBatchItem : BaseViewModel
{
    private string _inputPath = string.Empty;
    private string _outputPath = string.Empty;
    private string _status = "Pending";
    private string _originalSizeText = "-";
    private string _resultSizeText = "-";
    private bool _isFailed;
    private PdfCompressionRunSummary? _lastRunSummary;

    public string FileName => Path.GetFileName(InputPath);

    public string InputPath
    {
        get => _inputPath;
        set
        {
            if (SetProperty(ref _inputPath, value))
            {
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string OriginalSizeText
    {
        get => _originalSizeText;
        set => SetProperty(ref _originalSizeText, value);
    }

    public string ResultSizeText
    {
        get => _resultSizeText;
        set => SetProperty(ref _resultSizeText, value);
    }

    public bool IsFailed
    {
        get => _isFailed;
        set => SetProperty(ref _isFailed, value);
    }

    public PdfCompressionRunSummary? LastRunSummary
    {
        get => _lastRunSummary;
        set => SetProperty(ref _lastRunSummary, value);
    }
}

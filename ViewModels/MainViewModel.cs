using PdfTool.App.Services;

namespace PdfTool.App.ViewModels;

public class MainViewModel
{
    public MainViewModel(ProtectViewModel protect, SplitViewModel split, MergeViewModel merge, IAppStatusService status)
    {
        Protect = protect;
        Split = split;
        Merge = merge;
        Status = status;
    }

    public ProtectViewModel Protect { get; }
    public SplitViewModel Split { get; }
    public MergeViewModel Merge { get; }
    public IAppStatusService Status { get; }
}

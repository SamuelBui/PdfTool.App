using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PdfTool.App.Services;
using PdfTool.App.ViewModels;
using PdfTool.App.Views;

namespace PdfTool.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAppStatusService, AppStatusService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IPdfDocumentInspectorService, PdfDocumentInspectorService>();
        services.AddSingleton<IPdfThumbnailService, PdfThumbnailService>();
        services.AddSingleton<IPdfProtectionService, PdfProtectionService>();
        services.AddSingleton<IPdfSplitService, PdfSplitService>();
        services.AddSingleton<IPdfMergeService, PdfMergeService>();

        services.AddTransient<ProtectViewModel>();
        services.AddTransient<SplitViewModel>();
        services.AddTransient<MergeViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        window.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

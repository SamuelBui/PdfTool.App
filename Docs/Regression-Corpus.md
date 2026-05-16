# PDF Regression Corpus

Regression corpus cho `PdfTool.App` được sinh và chạy bởi project console:

- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\PdfToolSmoke.csproj`

## Mục tiêu

Corpus này kiểm tra lại các luồng cốt lõi của app:

- `Protect`
- `Unlock`
- `Split`
- `Merge`
- `Compress`

Nó sinh dữ liệu mẫu có tính lặp lại, thay vì phụ thuộc vào file PDF thủ công từ máy người dùng.

## Thành phần corpus

Thư mục sinh tự động:

- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\RegressionCorpus\Input`
- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\RegressionCorpus\Derived`
- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\RegressionCorpus\Assets`
- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\RegressionCorpus\Results`

Manifest và report:

- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\RegressionCorpus\corpus-manifest.json`
- `C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\RegressionCorpus\regression-report.json`

## Case hiện có

- `01-text-report.pdf`: tài liệu text/layout nhẹ cho protect/merge
- `02-vector-diagrams.pdf`: tài liệu vector cho merge/split/rotate
- `03-mixed-brochure.pdf`: tài liệu mixed cho split/compress
- `04-scan-color.pdf`: scan màu, dùng cho compress 15% và 50%
- `05-scan-lowcolor.pdf`: scan low-color, dùng cho compress 85%
- `06-protected-restricted.pdf`: file protected với `user password` + `owner password`
- `99-invalid.pdf`: file giả PDF để test negative case

## Cách chạy

### Build harness

```powershell
$env:DOTNET_CLI_HOME='C:\Users\nambu\OneDrive\Documents\Playground\.dotnet'
$env:NUGET_PACKAGES='C:\Users\nambu\OneDrive\Documents\Playground\.nuget\packages'
$env:APPDATA='C:\Users\nambu\OneDrive\Documents\Playground\.appdata'
$env:USERPROFILE='C:\Users\nambu\OneDrive\Documents\Playground\.userprofile'
dotnet build C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\PdfToolSmoke.csproj --configfile C:\Users\nambu\OneDrive\Documents\Playground\PdfTool.App\NuGet.Config
```

### Generate + run regression

```powershell
$env:DOTNET_CLI_HOME='C:\Users\nambu\OneDrive\Documents\Playground\.dotnet'
$env:NUGET_PACKAGES='C:\Users\nambu\OneDrive\Documents\Playground\.nuget\packages'
$env:APPDATA='C:\Users\nambu\OneDrive\Documents\Playground\.appdata'
$env:USERPROFILE='C:\Users\nambu\OneDrive\Documents\Playground\.userprofile'
dotnet run --project C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\PdfToolSmoke.csproj --configfile C:\Users\nambu\OneDrive\Documents\Playground\PdfTool.App\NuGet.Config
```

### Chỉ generate corpus

```powershell
$env:DOTNET_CLI_HOME='C:\Users\nambu\OneDrive\Documents\Playground\.dotnet'
$env:NUGET_PACKAGES='C:\Users\nambu\OneDrive\Documents\Playground\.nuget\packages'
$env:APPDATA='C:\Users\nambu\OneDrive\Documents\Playground\.appdata'
$env:USERPROFILE='C:\Users\nambu\OneDrive\Documents\Playground\.userprofile'
dotnet run --project C:\Users\nambu\OneDrive\Documents\Playground\PdfToolSmoke\PdfToolSmoke.csproj --configfile C:\Users\nambu\OneDrive\Documents\Playground\PdfTool.App\NuGet.Config -- --generate-only
```

## Regression checks hiện tại

- `Protect` file text và reject invalid pseudo-PDF
- `Unlock` chặn `user password` và cho phép `owner password`
- `Split` extract/remove/rotate
- `Merge` basic merge và merge với reorder + rotate
- `Compress` màu ở `15%`, màu ở `50%`, grayscale ở `85%`

## Gợi ý mở rộng

- thêm corpus cho `locked file` và `wrong password` theo kiểu test runtime
- thêm PDF malformed thực tế từ người dùng
- thêm corpus cho `session auto-restore`
- thêm so sánh `Before/After` cho compression bằng threshold riêng theo từng loại tài liệu

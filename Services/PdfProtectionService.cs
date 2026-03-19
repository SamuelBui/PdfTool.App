using System.IO;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfProtectionService : IPdfProtectionService
{
    public OperationResult Protect(PdfProtectionOptions options)
    {
        try
        {
            if (!FileAccessHelper.TryValidateReadableFile(options.InputPath, out var inputError))
            {
                return OperationResult.Fail(inputError);
            }

            if (!FileAccessHelper.TryValidateOutputFile(options.OutputPath, out var outputError))
            {
                return OperationResult.Fail(outputError);
            }

            if (string.IsNullOrWhiteSpace(options.UserPassword))
            {
                return OperationResult.Fail("User password cannot be empty.");
            }

            if (!PasswordHelper.MeetsStrongPolicy(options.UserPassword))
            {
                return OperationResult.Fail("User password must be at least 12 characters and include uppercase, lowercase, number, and special character.");
            }

            if (string.Equals(options.InputPath, options.OutputPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Output PDF must be different from input PDF.");
            }

            var ownerPassword = string.IsNullOrWhiteSpace(options.OwnerPassword)
                ? null
                : options.OwnerPassword;

            if (!string.IsNullOrWhiteSpace(ownerPassword) && !PasswordHelper.MeetsStrongPolicy(ownerPassword))
            {
                return OperationResult.Fail("Owner password must be at least 12 characters and include uppercase, lowercase, number, and special character.");
            }

            var restrictionsApplied =
                !options.AllowPrint ||
                !options.AllowFullQualityPrint ||
                !options.AllowModifyDocument ||
                !options.AllowExtractContent ||
                !options.AllowAnnotations ||
                !options.AllowFormsFill ||
                !options.AllowAssembleDocument;

            if (restrictionsApplied && string.IsNullOrWhiteSpace(ownerPassword))
            {
                return OperationResult.Fail("Owner password is required when permissions are restricted.");
            }

            using var document = PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Modify);
            var security = document.SecuritySettings;
            security.UserPassword = options.UserPassword;

            if (!string.IsNullOrWhiteSpace(ownerPassword))
            {
                security.OwnerPassword = ownerPassword;
            }

            security.PermitPrint = options.AllowPrint;
            security.PermitFullQualityPrint = options.AllowFullQualityPrint;
            security.PermitModifyDocument = options.AllowModifyDocument;
            security.PermitExtractContent = options.AllowExtractContent;
            security.PermitAnnotations = options.AllowAnnotations;
            security.PermitFormsFill = options.AllowFormsFill;
            security.PermitAssembleDocument = options.AllowAssembleDocument;

            document.Save(options.OutputPath);

            return OperationResult.Ok("PDF protected successfully.", options.OutputPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Protect failed: {ex.Message}");
        }
    }
}

namespace KKL.WordStudio.Infrastructure.Persistence;

using System.IO.Compression;
using System.Text.Json;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.ReferenceFormatting;
using KKL.WordStudio.Infrastructure.Word;
using KKL.WordStudio.Shared.Constants;
using KKL.WordStudio.Shared.Results;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads/writes the native .kws project container. A .kws file is a zip
/// archive containing manifest.json, project.json, and project-owned binary
/// assets. Sprint 8 stores imported front matter as a separate
/// /resources/frontmatter/front-matter.docx entry — never as base64 in JSON.
/// </summary>
public sealed class KwsProjectRepository : IProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate
    };

    private readonly ILogger<KwsProjectRepository> _logger;

    public KwsProjectRepository(ILogger<KwsProjectRepository> logger) => _logger = logger;

    public Project CreateNew()
    {
        var project = new Project { Name = "Yeni Proje" };
        var report = new Report { Name = "Rapor 1" };
        var page = new Domain.Reports.Page();
        page.Sections.Add(new Domain.Reports.Section { Name = "Body", Kind = Domain.Reports.SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return project;
    }

    public async Task<Result> SaveAsync(Project project, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!filePath.EndsWith(AppConstants.ProjectFileExtension, StringComparison.OrdinalIgnoreCase))
                filePath += AppConstants.ProjectFileExtension;

            NormalizeFrontMatterState(project.FrontMatter);
            NormalizeReferenceFormatState(project.ReferenceFormat);

            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            var manifest = new KwsProjectManifest
            {
                FormatVersion = AppConstants.ProjectFileFormatVersion,
                ProductVersion = AppConstants.ApplicationName
            };

            await WriteJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken);
            await WriteJsonEntryAsync(archive, "project.json", project, cancellationToken);
            await WriteFrontMatterEntryAsync(archive, project.FrontMatter, cancellationToken);
            await WriteReferenceFormatEntryAsync(archive, project.ReferenceFormat, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project to {FilePath}", filePath);
            return Result.Failure($"Proje kaydedilemedi. '{filePath}' konumuna yazma izniniz ve yeterli disk alanınız olduğundan emin olup yeniden deneyin.");
        }
    }

    public async Task<Result<Project>> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result.Failure<Project>($"Proje dosyası bulunamadı: {filePath}");

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            var entry = archive.GetEntry("project.json")
                ?? throw new InvalidDataException("The .kws package is missing project.json.");

            Project project;
            await using (var entryStream = entry.Open())
            {
                project = await JsonSerializer.DeserializeAsync<Project>(entryStream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("project.json could not be deserialized.");
            }

            await MaterializeFrontMatterAssetAsync(archive, project, cancellationToken);
            await MaterializeReferenceFormatAssetAsync(archive, project, cancellationToken);
            return Result.Success(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open project from {FilePath}", filePath);
            return Result.Failure<Project>($"Proje açılamadı: '{filePath}'. Dosya bozulmuş veya daha yeni bir KKL Word Studio sürümüyle oluşturulmuş olabilir.");
        }
    }

    private static void NormalizeFrontMatterState(FrontMatterDocument? frontMatter)
    {
        if (frontMatter is null) return;
        frontMatter.EmbeddedAssetEntryName = FrontMatterDocument.DefaultEmbeddedAssetEntryName;
    }

    private static void NormalizeReferenceFormatState(ReferenceFormatDocument? referenceFormat)
    {
        if (referenceFormat is null) return;
        referenceFormat.EmbeddedAssetEntryName = ReferenceFormatDocument.DefaultEmbeddedAssetEntryName;
    }

    private static async Task WriteFrontMatterEntryAsync(
        ZipArchive archive,
        FrontMatterDocument? frontMatter,
        CancellationToken cancellationToken)
    {
        var sourcePath = FrontMatterSourcePathResolver.Resolve(frontMatter);
        if (frontMatter is null || sourcePath is null)
            return; // preserve state in project.json; missing-source state is explicit and non-fatal

        var entry = archive.CreateEntry(FrontMatterDocument.DefaultEmbeddedAssetEntryName, CompressionLevel.Optimal);
        await using var target = entry.Open();
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task WriteReferenceFormatEntryAsync(
        ZipArchive archive,
        ReferenceFormatDocument? referenceFormat,
        CancellationToken cancellationToken)
    {
        var sourcePath = ReferenceFormatSourcePathResolver.Resolve(referenceFormat);
        if (referenceFormat is null || sourcePath is null)
            return; // missing reference asset remains an explicit non-fatal project state

        var entry = archive.CreateEntry(ReferenceFormatDocument.DefaultEmbeddedAssetEntryName, CompressionLevel.Optimal);
        await using var target = entry.Open();
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task MaterializeFrontMatterAssetAsync(
        ZipArchive archive,
        Project project,
        CancellationToken cancellationToken)
    {
        var frontMatter = project.FrontMatter;
        if (frontMatter is null) return;

        NormalizeFrontMatterState(frontMatter);
        var assetEntry = archive.GetEntry(FrontMatterDocument.DefaultEmbeddedAssetEntryName);
        if (assetEntry is not null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "KKL.WordStudio",
                "frontmatter",
                project.Id.ToString("N"));
            Directory.CreateDirectory(directory);

            var materializedPath = Path.Combine(directory, SanitizeFileName(frontMatter.FileName));
            await using var source = assetEntry.Open();
            await using var target = new FileStream(materializedPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await source.CopyToAsync(target, cancellationToken);
            frontMatter.ResolvedFilePath = materializedPath;
            return;
        }

        // Compatibility/reference fallback: a project whose JSON remembers a
        // front-matter source but has no embedded asset still opens. The UI can
        // show "Ön belge bulunamadı" instead of the repository rejecting the
        // whole project.
        if (!string.IsNullOrWhiteSpace(frontMatter.OriginalSourcePath) && File.Exists(frontMatter.OriginalSourcePath))
            frontMatter.ResolvedFilePath = frontMatter.OriginalSourcePath;
    }

    private static async Task MaterializeReferenceFormatAssetAsync(
        ZipArchive archive,
        Project project,
        CancellationToken cancellationToken)
    {
        var referenceFormat = project.ReferenceFormat;
        if (referenceFormat is null) return;

        NormalizeReferenceFormatState(referenceFormat);
        var assetEntry = archive.GetEntry(ReferenceFormatDocument.DefaultEmbeddedAssetEntryName);
        if (assetEntry is not null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "KKL.WordStudio",
                "reference-format",
                project.Id.ToString("N"));
            Directory.CreateDirectory(directory);

            var materializedPath = Path.Combine(
                directory,
                SanitizeFileName(referenceFormat.FileName, "reference-format.docx"));
            await using var source = assetEntry.Open();
            await using var target = new FileStream(materializedPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await source.CopyToAsync(target, cancellationToken);
            referenceFormat.ResolvedFilePath = materializedPath;
            return;
        }

        if (!string.IsNullOrWhiteSpace(referenceFormat.OriginalSourcePath)
            && File.Exists(referenceFormat.OriginalSourcePath))
        {
            referenceFormat.ResolvedFilePath = referenceFormat.OriginalSourcePath;
        }
    }

    private static string SanitizeFileName(string fileName, string fallbackFileName = "front-matter.docx")
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? fallbackFileName : sanitized;
    }

    private static async Task WriteJsonEntryAsync<T>(ZipArchive archive, string entryName, T value, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }
}

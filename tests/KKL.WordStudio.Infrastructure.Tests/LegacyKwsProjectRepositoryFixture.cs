namespace KKL.WordStudio.Infrastructure.Persistence;

using System.IO.Compression;
using System.Text.Json;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;
using Microsoft.Extensions.Logging;

/// <summary>
/// Test-only compatibility fixture for historical post-Sprint-15 regression
/// scenarios that verify old portable asset packages. Production native-project
/// persistence, its Application contract, and its DI registration are removed.
/// </summary>
internal sealed class KwsProjectRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate
    };

    private readonly ILogger<KwsProjectRepository> _logger;

    public KwsProjectRepository(ILogger<KwsProjectRepository> logger) => _logger = logger;

    public async Task<Result> SaveAsync(
        Project project,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetPath = filePath.EndsWith(".kws", StringComparison.OrdinalIgnoreCase)
                ? filePath
                : filePath + ".kws";

            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
            await WriteJsonEntryAsync(archive, "project.json", project, cancellationToken);
            await WriteAssetAsync(
                archive,
                project.FrontMatter?.ResolvedFilePath ?? project.FrontMatter?.OriginalSourcePath,
                FrontMatterDocument.DefaultEmbeddedAssetEntryName,
                cancellationToken);
            await WriteAssetAsync(
                archive,
                project.ReferenceFormat?.ResolvedFilePath ?? project.ReferenceFormat?.OriginalSourcePath,
                ReferenceFormatDocument.DefaultEmbeddedAssetEntryName,
                cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Legacy test fixture could not write {FilePath}", filePath);
            return Result.Failure(exception.Message);
        }
    }

    public async Task<Result<Project>> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var projectEntry = archive.GetEntry("project.json")
                ?? throw new InvalidDataException("project.json is missing.");

            Project project;
            await using (var entryStream = projectEntry.Open())
            {
                project = await JsonSerializer.DeserializeAsync<Project>(
                    entryStream,
                    JsonOptions,
                    cancellationToken)
                    ?? throw new InvalidDataException("project.json could not be read.");
            }

            if (project.FrontMatter is not null)
            {
                project.FrontMatter.ResolvedFilePath = await MaterializeAsync(
                    archive,
                    FrontMatterDocument.DefaultEmbeddedAssetEntryName,
                    project.FrontMatter.FileName,
                    "frontmatter",
                    cancellationToken)
                    ?? ResolveExisting(project.FrontMatter.OriginalSourcePath);
            }

            if (project.ReferenceFormat is not null)
            {
                project.ReferenceFormat.ResolvedFilePath = await MaterializeAsync(
                    archive,
                    ReferenceFormatDocument.DefaultEmbeddedAssetEntryName,
                    project.ReferenceFormat.FileName,
                    "reference-format",
                    cancellationToken)
                    ?? ResolveExisting(project.ReferenceFormat.OriginalSourcePath);
            }

            return Result.Success(project);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Legacy test fixture could not read {FilePath}", filePath);
            return Result.Failure<Project>(exception.Message);
        }
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static async Task WriteAssetAsync(
        ZipArchive archive,
        string? sourcePath,
        string entryName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var target = entry.Open();
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task<string?> MaterializeAsync(
        ZipArchive archive,
        string entryName,
        string fileName,
        string category,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
            return null;

        var directory = Path.Combine(
            Path.GetTempPath(),
            "KKL.WordStudio.Tests",
            category,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var targetPath = Path.Combine(directory, Sanitize(fileName));
        await using var source = entry.Open();
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await source.CopyToAsync(target, cancellationToken);
        return targetPath;
    }

    private static string? ResolveExisting(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;

    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "asset.docx" : sanitized;
    }
}

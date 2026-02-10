namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;
using static Umbraco.Community.PerformanceTestDataSeeder.Infrastructure.SeederConstants;

/// <summary>
/// Seeds media items (PDFs, images, videos) for use in content.
/// Execution order: 5 (after DocumentTypeSeeder).
/// </summary>
public class MediaSeeder : BaseSeeder<MediaSeeder>
{
    private readonly IMediaService _mediaService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly MediaFileManager _mediaFileManager;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;

    /// <summary>
    /// Creates a new MediaSeeder instance.
    /// </summary>
    public MediaSeeder(
        IMediaService mediaService,
        IMediaTypeService mediaTypeService,
        MediaFileManager mediaFileManager,
        IShortStringHelper shortStringHelper,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        IScopeProvider scopeProvider,
        ILogger<MediaSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
    {
        _mediaService = mediaService;
        _mediaTypeService = mediaTypeService;
        _mediaFileManager = mediaFileManager;
        _shortStringHelper = shortStringHelper;
        _mediaUrlGenerators = mediaUrlGenerators;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 5;

    /// <inheritdoc />
    public override string SeederName => "MediaSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.Media;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var prefix = GetPrefix(PrefixType.Media);
        var rootMedia = _mediaService.GetRootMedia();
        return rootMedia.Any(m => m.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        var mediaConfig = Config.Media;
        var prefix = GetPrefix(PrefixType.Media);
        int totalTarget = mediaConfig.TotalCount;

        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create media: {PDFs} PDFs, {PNGs} PNGs, {JPGs} JPGs, {Videos} videos (total: {Total})",
                mediaConfig.PDF.Count, mediaConfig.PNG.Count, mediaConfig.JPG.Count, mediaConfig.Video.Count, totalTarget);
        }

        Logger.LogInformation("Starting media seeding (target: {Target} items)...", totalTarget);

        // Create root folders
        var pdfFolder = CreateFolder($"{prefix}PDFs", -1);
        var imagesFolder = CreateFolder($"{prefix}Images", -1);
        var videosFolder = CreateFolder($"{prefix}Videos", -1);

        // Create subfolders for images (use parent ID or -1 for DryRun)
        var imagesFolderId = imagesFolder?.Id ?? -1;
        var pngFolder = CreateFolder("PNG", imagesFolderId);
        var jpgFolder = CreateFolder("JPG", imagesFolderId);

        // Seed PDFs
        var pdfFolderId = pdfFolder?.Id ?? -1;
        SeedPDFs(pdfFolderId, prefix, mediaConfig.PDF.Count, mediaConfig.PDF.FolderCount, cancellationToken);

        // Seed PNGs
        var pngFolderId = pngFolder?.Id ?? -1;
        SeedImages(pngFolderId, prefix, mediaConfig.PNG.Count, mediaConfig.PNG.FolderCount, "png", cancellationToken);

        // Seed JPGs
        var jpgFolderId = jpgFolder?.Id ?? -1;
        SeedImages(jpgFolderId, prefix, mediaConfig.JPG.Count, mediaConfig.JPG.FolderCount, "jpg", cancellationToken);

        // Seed Videos
        var videosFolderId = videosFolder?.Id ?? -1;
        SeedVideos(videosFolderId, prefix, mediaConfig.Video.Count, cancellationToken);

        // Cache media items for ContentSeeder (skip in DryRun as nothing was created)
        if (ShouldPersist)
        {
            LoadMediaItemsToContext();
        }

        Logger.LogInformation("Media seeding completed! Cached {Count} images for content linking.", Context.MediaItems.Count);

        return Task.CompletedTask;
    }

    private void LoadMediaItemsToContext()
    {
        var imageMedia = new List<IMedia>();
        var prefix = GetPrefix(PrefixType.Media);

        // Get all root media folders we created
        var rootMedia = _mediaService.GetRootMedia()
            .Where(m => m.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (var root in rootMedia)
        {
            // Page through all descendants to get all images
            long pageIndex = 0;
            var pageSize = PaginationPageSize;
            long totalRecords;

            do
            {
                var descendants = _mediaService.GetPagedDescendants(root.Id, pageIndex, pageSize, out totalRecords);
                var images = descendants.Where(m => m.ContentType.Alias == Constants.Conventions.MediaTypes.Image);
                imageMedia.AddRange(images);
                pageIndex++;
            } while (pageIndex * pageSize < totalRecords);
        }

        Context.AddMediaItems(imageMedia);
        Logger.LogDebug("Loaded {Count} images into context for content linking", imageMedia.Count);
    }

    private IMedia? CreateFolder(string name, int parentId)
    {
        if (IsDryRun)
        {
            LogDryRun("Media Folder", name, $"parent={parentId}");
            return null;
        }

        var folder = _mediaService.CreateMedia(name, parentId, Constants.Conventions.MediaTypes.Folder);
        _mediaService.Save(folder);
        return folder;
    }

    private void SeedPDFs(int parentId, string prefix, int totalCount, int folderCount, CancellationToken cancellationToken)
    {
        if (totalCount <= 0) return;

        int filesPerFolder = totalCount / Math.Max(folderCount, 1);
        int created = 0;

        for (int f = 1; f <= folderCount; f++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folder = CreateFolder($"PDFs_Folder_{f}", parentId);
            var folderId = folder?.Id ?? parentId;

            // Use a scope per folder for batching (skip in DryRun)
            // Don't suppress notifications - cache needs to be updated for media
            using var scope = ShouldPersist ? CreateScopedBatch(suppressNotifications: false) : null;

            for (int i = 1; i <= filesPerFolder && created < totalCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileName = $"{prefix}PDF_{created + 1}.pdf";
                    var pdfBytes = GenerateMinimalPDF(created + 1);

                    CreateMediaWithFile(folderId, fileName, pdfBytes, Constants.Conventions.MediaTypes.File);
                    created++;

                    LogProgress(created, totalCount, "PDFs");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to create PDF {Index}", created + 1);
                    if (Options.StopOnError) throw;
                }
            }

            scope?.Complete();
        }

        Logger.LogInformation("Created {Count} PDFs total", created);
    }

    private void SeedImages(int parentId, string prefix, int totalCount, int folderCount, string format, CancellationToken cancellationToken)
    {
        if (totalCount <= 0) return;

        int filesPerFolder = totalCount / Math.Max(folderCount, 1);
        int created = 0;

        // Pre-generate seeds for reproducibility using thread-safe method
        var seeds = Context.GetRandomBatch(totalCount);

        for (int f = 1; f <= folderCount; f++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folder = CreateFolder($"{format.ToUpper()}_Folder_{f}", parentId);
            var folderId = folder?.Id ?? parentId;

            // Calculate how many images for this folder
            int startIndex = (f - 1) * filesPerFolder;
            int endIndex = Math.Min(startIndex + filesPerFolder, totalCount);
            int imagesInThisFolder = endIndex - startIndex;

            if (imagesInThisFolder <= 0) continue;

            // Generate images in parallel (CPU-bound) - do this even in DryRun to show work
            var imageData = new ConcurrentBag<(int index, string fileName, byte[] data)>();

            Parallel.For(startIndex, endIndex, new ParallelOptions
            {
                MaxDegreeOfParallelism = Options.ParallelDegree,
                CancellationToken = cancellationToken
            }, i =>
            {
                var fileName = $"{prefix}Image_{i + 1}.{format}";
                var bytes = GenerateColoredImage(DefaultImageWidth, DefaultImageHeight, format, seeds[i]);
                imageData.Add((i, fileName, bytes));
            });

            // Save in batched scope (sequential - DB bound)
            var orderedImages = imageData.OrderBy(x => x.index).ToList();
            int batchCount = 0;
            IScope? currentScope = null;

            try
            {
                foreach (var (index, fileName, data) in orderedImages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (currentScope == null && ShouldPersist)
                        {
                            // Don't suppress notifications - cache needs to be updated for media
                            currentScope = CreateScopedBatch(suppressNotifications: false);
                            batchCount = 0;
                        }

                        CreateMediaWithFile(folderId, fileName, data, Constants.Conventions.MediaTypes.Image);
                        created++;
                        batchCount++;

                        if (ShouldPersist && batchCount >= Options.BatchSize)
                        {
                            currentScope?.Complete();
                            currentScope?.Dispose();
                            currentScope = null;
                        }

                        LogProgress(created, totalCount, $"{format.ToUpper()} images");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to create {Format} image {Index}", format.ToUpper(), index + 1);
                        if (Options.StopOnError) throw;
                    }
                }

                currentScope?.Complete();
            }
            finally
            {
                currentScope?.Dispose();
            }
        }

        Logger.LogInformation("Created {Count} {Format} images total", created, format.ToUpper());
    }

    private void SeedVideos(int parentId, string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return;

        // Don't suppress notifications - cache needs to be updated for media
        using var scope = ShouldPersist ? CreateScopedBatch(suppressNotifications: false) : null;

        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileName = $"{prefix}Video_{i}.mp4";
                var videoBytes = GeneratePlaceholderVideo();

                CreateMediaWithFile(parentId, fileName, videoBytes, Constants.Conventions.MediaTypes.File);

                Logger.LogDebug("Created video {Index}/{Count}", i, count);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create video {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        scope?.Complete();
        Logger.LogInformation("Created {Count} videos", count);
    }

    private void CreateMediaWithFile(int parentId, string fileName, byte[] fileBytes, string mediaTypeAlias)
    {
        if (IsDryRun)
        {
            LogDryRun("Media", fileName, $"type={mediaTypeAlias}, size={fileBytes.Length} bytes");
            return;
        }

        // Validate file bytes
        if (fileBytes == null || fileBytes.Length == 0)
        {
            Logger.LogWarning("Cannot create media {FileName}: file bytes are empty", fileName);
            return;
        }

        using var stream = new MemoryStream(fileBytes);

        var media = _mediaService.CreateMedia(
            Path.GetFileNameWithoutExtension(fileName),
            parentId,
            mediaTypeAlias);

        media.SetValue(
            _mediaFileManager,
            _mediaUrlGenerators,
            _shortStringHelper,
            _contentTypeBaseServiceProvider,
            Constants.Conventions.Media.File,
            fileName,
            stream);

        _mediaService.Save(media);

        // Validate media was saved successfully
        if (media.Id == 0)
        {
            Logger.LogWarning("Media {FileName} may not have been saved correctly (ID=0)", fileName);
        }
    }

    private static byte[] GenerateMinimalPDF(int index)
    {
        var content = $@"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT
/F1 12 Tf
100 700 Td
(Test PDF {index}) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000206 00000 n
trailer
<< /Size 5 /Root 1 0 R >>
startxref
300
%%EOF";

        return System.Text.Encoding.ASCII.GetBytes(content);
    }

    private static byte[] GenerateColoredImage(int width, int height, string format, int seed)
    {
        // Validate dimensions
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException($"Invalid image dimensions: {width}x{height}");
        }

        var random = new Random(seed);
        var color = Color.FromRgb(
            (byte)random.Next(256),
            (byte)random.Next(256),
            (byte)random.Next(256));

        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(color));

        using var ms = new MemoryStream();

        if (format.Equals("png", StringComparison.OrdinalIgnoreCase))
        {
            image.SaveAsPng(ms);
        }
        else
        {
            image.SaveAsJpeg(ms);
        }

        var result = ms.ToArray();

        // Validate generated image has content
        if (result.Length == 0)
        {
            throw new InvalidOperationException($"Generated {format} image is empty (seed: {seed})");
        }

        return result;
    }

    private static byte[] GeneratePlaceholderVideo()
    {
        // Minimal MP4 signature (placeholder, not a valid playable video)
        var header = new byte[]
        {
            0x00, 0x00, 0x00, 0x1C,
            0x66, 0x74, 0x79, 0x70, // ftyp
            0x69, 0x73, 0x6F, 0x6D, // isom
            0x00, 0x00, 0x02, 0x00,
            0x69, 0x73, 0x6F, 0x6D, // isom
            0x69, 0x73, 0x6F, 0x32, // iso2
            0x6D, 0x70, 0x34, 0x31  // mp41
        };

        var placeholder = new byte[PlaceholderVideoSizeBytes];
        Array.Copy(header, placeholder, header.Length);

        return placeholder;
    }
}

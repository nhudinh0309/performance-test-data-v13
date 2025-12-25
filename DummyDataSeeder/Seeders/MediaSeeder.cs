using Bogus;
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

public class MediaSeeder : IHostedService
{
    private readonly IMediaService _mediaService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly MediaFileManager _mediaFileManager;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    private readonly Faker _faker = new("en");

    public MediaSeeder(
        IMediaService mediaService,
        IMediaTypeService mediaTypeService,
        MediaFileManager mediaFileManager,
        IShortStringHelper shortStringHelper,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config)
    {
        _mediaService = mediaService;
        _mediaTypeService = mediaTypeService;
        _mediaFileManager = mediaFileManager;
        _shortStringHelper = shortStringHelper;
        _mediaUrlGenerators = mediaUrlGenerators;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _runtimeState = runtimeState;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed when Umbraco is fully installed and running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            Console.WriteLine("MediaSeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

        SeedMedia();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedMedia()
    {
        // Check if already seeded
        var rootMedia = _mediaService.GetRootMedia();
        if (rootMedia.Any(m => m.Name?.StartsWith("Test_") == true)) return;

        var mediaConfig = _config.Media;
        Console.WriteLine($"Starting media seeding (target: {mediaConfig.TotalCount} items)...");

        // Create root folders
        var pdfFolder = CreateFolder("Test_PDFs", -1);
        var imagesFolder = CreateFolder("Test_Images", -1);
        var videosFolder = CreateFolder("Test_Videos", -1);

        // Create subfolders for images
        var pngFolder = CreateFolder("PNG", imagesFolder.Id);
        var jpgFolder = CreateFolder("JPG", imagesFolder.Id);

        // Seed PDFs
        Console.WriteLine("Seeding PDFs...");
        SeedPDFs(pdfFolder.Id, mediaConfig.PDF.Count, mediaConfig.PDF.FolderCount);

        // Seed PNGs
        Console.WriteLine("Seeding PNG images...");
        SeedImages(pngFolder.Id, mediaConfig.PNG.Count, mediaConfig.PNG.FolderCount, "png");

        // Seed JPGs
        Console.WriteLine("Seeding JPG images...");
        SeedImages(jpgFolder.Id, mediaConfig.JPG.Count, mediaConfig.JPG.FolderCount, "jpg");

        // Seed Videos
        Console.WriteLine("Seeding videos...");
        SeedVideos(videosFolder.Id, mediaConfig.Video.Count);

        Console.WriteLine($"Media seeding completed! (target: {mediaConfig.TotalCount})");
    }

    private IMedia CreateFolder(string name, int parentId)
    {
        var folderType = _mediaTypeService.Get(Constants.Conventions.MediaTypes.Folder);
        var folder = _mediaService.CreateMedia(name, parentId, Constants.Conventions.MediaTypes.Folder);
        _mediaService.Save(folder);
        return folder;
    }

    private void SeedPDFs(int parentId, int totalCount, int folderCount)
    {
        int filesPerFolder = totalCount / folderCount;
        int created = 0;

        for (int f = 1; f <= folderCount; f++)
        {
            var folder = CreateFolder($"PDFs_Folder_{f}", parentId);

            for (int i = 1; i <= filesPerFolder && created < totalCount; i++)
            {
                var fileName = $"TestPDF_{created + 1}.pdf";
                var pdfBytes = GenerateMinimalPDF(created + 1);

                CreateMediaWithFile(folder.Id, fileName, pdfBytes, Constants.Conventions.MediaTypes.File);
                created++;

                if (created % 500 == 0)
                    Console.WriteLine($"Created {created}/{totalCount} PDFs...");
            }
        }

        Console.WriteLine($"Created {created} PDFs total.");
    }

    private void SeedImages(int parentId, int totalCount, int folderCount, string format)
    {
        int filesPerFolder = totalCount / folderCount;
        int created = 0;

        for (int f = 1; f <= folderCount; f++)
        {
            var folder = CreateFolder($"{format.ToUpper()}_Folder_{f}", parentId);

            for (int i = 1; i <= filesPerFolder && created < totalCount; i++)
            {
                var fileName = $"TestImage_{created + 1}.{format}";
                var imageBytes = GenerateColoredImage(100, 100, format, created);

                CreateMediaWithFile(folder.Id, fileName, imageBytes, Constants.Conventions.MediaTypes.Image);
                created++;

                if (created % 500 == 0)
                    Console.WriteLine($"Created {created}/{totalCount} {format.ToUpper()} images...");
            }
        }

        Console.WriteLine($"Created {created} {format.ToUpper()} images total.");
    }

    private void SeedVideos(int parentId, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            var fileName = $"TestVideo_{i}.mp4";
            var videoBytes = GeneratePlaceholderVideo();

            CreateMediaWithFile(parentId, fileName, videoBytes, Constants.Conventions.MediaTypes.File);
            Console.WriteLine($"Created video {i}/{count}");
        }
    }

    private void CreateMediaWithFile(int parentId, string fileName, byte[] fileBytes, string mediaTypeAlias)
    {
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
    }

    private byte[] GenerateMinimalPDF(int index)
    {
        // Create a minimal valid PDF
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

    private byte[] GenerateColoredImage(int width, int height, string format, int seed)
    {
        // Generate a unique color based on seed
        var random = new Random(seed);
        var color = Color.FromRgb(
            (byte)random.Next(256),
            (byte)random.Next(256),
            (byte)random.Next(256));

        using var image = new Image<Rgba32>(width, height);

        // Fill with the color
        image.Mutate(ctx => ctx.BackgroundColor(color));

        using var ms = new MemoryStream();

        if (format.ToLower() == "png")
        {
            image.SaveAsPng(ms);
        }
        else
        {
            image.SaveAsJpeg(ms);
        }

        return ms.ToArray();
    }

    private byte[] GeneratePlaceholderVideo()
    {
        // Create a minimal placeholder file with MP4 signature
        // This is just a placeholder - not a valid playable video
        var header = new byte[]
        {
            0x00, 0x00, 0x00, 0x1C, // Size
            0x66, 0x74, 0x79, 0x70, // ftyp
            0x69, 0x73, 0x6F, 0x6D, // isom
            0x00, 0x00, 0x02, 0x00, // Version
            0x69, 0x73, 0x6F, 0x6D, // isom
            0x69, 0x73, 0x6F, 0x32, // iso2
            0x6D, 0x70, 0x34, 0x31  // mp41
        };

        // Pad to make it larger (but still small)
        var placeholder = new byte[1024 * 100]; // 100KB
        Array.Copy(header, placeholder, header.Length);

        return placeholder;
    }
}

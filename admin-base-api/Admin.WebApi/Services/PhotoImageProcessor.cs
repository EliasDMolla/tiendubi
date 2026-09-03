using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Admin.WebApi.Services;

public class PhotoImageProcessor : IPhotoImageProcessor
{
    private static readonly JpegEncoder ThumbnailJpegEncoder = new() { Quality = 76 };
    private static readonly JpegEncoder WatermarkedJpegEncoder = new() { Quality = 80 };
    private const string FixedWatermarkText = "CAPTURAR";
    private static readonly Size ProcessedImageMaxSize = new(1280, 1280);
    private static readonly string[] PreferredFontFamilies =
    {
        "Arial",
        "DejaVu Sans",
        "Liberation Sans",
        "Noto Sans",
        "Helvetica",
        "Verdana"
    };

    public async Task<Stream> CreateThumbnailAsync(Stream originalImage, CancellationToken cancellationToken = default)
    {
        originalImage.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(originalImage, cancellationToken);

        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.XmpProfile = null;

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
            Size = ProcessedImageMaxSize
        }));

        var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, ThumbnailJpegEncoder, cancellationToken);
        output.Position = 0;
        return output;
    }

    public async Task<Stream> CreateWatermarkedAsync(Stream originalImage, string watermarkText, CancellationToken cancellationToken = default)
    {
        originalImage.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(originalImage, cancellationToken);

        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.XmpProfile = null;

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
            Size = ProcessedImageMaxSize
        }));

        var text = string.IsNullOrWhiteSpace(watermarkText) ? FixedWatermarkText : watermarkText.Trim().ToUpperInvariant();
        var fontSize = Math.Clamp(image.Width / 8f, 30f, 82f);
        var font = CreateWatermarkFont(fontSize);
        var rowStep = Math.Max(fontSize * 3.6f, 140f);
        var colStep = Math.Max(fontSize * 6.2f, 260f);

        image.Mutate(ctx =>
        {
            for (var y = -rowStep; y <= image.Height + rowStep; y += rowStep)
            {
                var shifted = ((int)(y / rowStep) & 1) == 0;

                for (var x = -colStep; x <= image.Width + colStep; x += colStep)
                {
                    var drawX = shifted ? x + (colStep * 0.5f) : x;
                    var origin = new PointF(drawX, y);

                    ctx.DrawText(new RichTextOptions(font)
                    {
                        Origin = origin,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    }, text, Color.Black.WithAlpha(0.13f));

                    ctx.DrawText(new RichTextOptions(font)
                    {
                        Origin = new PointF(drawX + 2f, y + 2f),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    }, text, Color.White.WithAlpha(0.18f));
                }
            }
        });

        var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, WatermarkedJpegEncoder, cancellationToken);
        output.Position = 0;
        return output;
    }

    private static Font CreateWatermarkFont(float fontSize)
    {
        foreach (var preferredFamily in PreferredFontFamilies)
        {
            if (SystemFonts.Collection.TryGet(preferredFamily, out _))
            {
                return SystemFonts.CreateFont(preferredFamily, fontSize, FontStyle.Bold);
            }
        }

        var fallbackFamily = SystemFonts.Collection.Families.Select(f => f.Name).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fallbackFamily))
        {
            return SystemFonts.CreateFont(fallbackFamily, fontSize, FontStyle.Bold);
        }

        throw new InvalidOperationException("No hay fuentes disponibles en el sistema para aplicar watermark.");
    }
}

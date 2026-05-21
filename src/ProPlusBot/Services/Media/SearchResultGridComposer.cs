using System.Globalization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProPlusBot.Services.Media;

public class SearchResultGridComposer(IHttpClientFactory httpClientFactory, ILogger<SearchResultGridComposer> logger)
{
    private const int GridColumns = 3;
    private const int CellSize = 240;
    private const int Gap = 4;
    private const int BadgeSize = 44;
    private static readonly Color PlaceholderColor = Color.FromRgb(48, 48, 52);
    private static readonly Color LabelBackground = Color.FromRgba(0, 0, 0, 170);
    private static readonly Color LabelText = Color.White;

    private static readonly Lazy<Font?> LabelFont = new(CreateLabelFont);

    public async Task<byte[]?> CreateGridJpegAsync(
        IReadOnlyList<MediaSearchResultItem> pageItems,
        CancellationToken ct = default)
    {
        if (pageItems.Count == 0)
            return null;

        var cells = new Image<Rgba32>[MediaConstants.SearchResultsPerPage];
        var client = httpClientFactory.CreateClient(nameof(SearchResultGridComposer));

        try
        {
            for (var i = 0; i < MediaConstants.SearchResultsPerPage; i++)
            {
                if (i < pageItems.Count && !string.IsNullOrWhiteSpace(pageItems[i].ThumbnailUrl))
                {
                    cells[i] = await LoadThumbnailCellAsync(client, pageItems[i].ThumbnailUrl!, ct)
                        ?? CreatePlaceholderCell();
                }
                else
                {
                    cells[i] = CreatePlaceholderCell();
                }

                if (i < pageItems.Count)
                    DrawNumberLabel(cells[i], i + 1);
            }

            var gridSide = GridColumns * CellSize + (GridColumns - 1) * Gap;
            using var grid = new Image<Rgba32>(gridSide, gridSide);

            for (var index = 0; index < MediaConstants.SearchResultsPerPage; index++)
            {
                var row = index / GridColumns;
                var col = index % GridColumns;
                var x = col * (CellSize + Gap);
                var y = row * (CellSize + Gap);
                grid.Mutate(ctx => ctx.DrawImage(cells[index], new Point(x, y), 1f));
            }

            using var ms = new MemoryStream();
            await grid.SaveAsJpegAsync(ms, ct);
            return ms.ToArray();
        }
        finally
        {
            foreach (var cell in cells)
                cell?.Dispose();
        }
    }

    private async Task<Image<Rgba32>?> LoadThumbnailCellAsync(
        HttpClient client,
        string url,
        CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var image = await Image.LoadAsync<Rgba32>(stream, ct);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(CellSize, CellSize),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));
            return image;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to load search thumbnail {Url}", url);
            return null;
        }
    }

    private static Image<Rgba32> CreatePlaceholderCell()
    {
        var image = new Image<Rgba32>(CellSize, CellSize, PlaceholderColor);
        return image;
    }

    private static void DrawNumberLabel(Image<Rgba32> cell, int number)
    {
        var badgeX = CellSize - BadgeSize - 8f;
        var badgeY = 8f;

        cell.Mutate(ctx =>
        {
            ctx.Fill(LabelBackground, new RectangleF(badgeX, badgeY, BadgeSize, BadgeSize));

            if (LabelFont.Value is { } font)
            {
                var label = number.ToString(CultureInfo.InvariantCulture);
                var textSize = TextMeasurer.MeasureSize(label, new TextOptions(font));
                var textX = badgeX + (BadgeSize - textSize.Width) / 2f;
                var textY = badgeY + (BadgeSize - textSize.Height) / 2f;
                ctx.DrawText(label, font, LabelText, new PointF(textX, textY));
            }
            else
            {
                DrawBitmapDigit(ctx, number, badgeX, badgeY, BadgeSize);
            }
        });
    }

    /// <summary>5×7 pixel patterns for digits 1–9 (no system fonts required).</summary>
    private static void DrawBitmapDigit(IImageProcessingContext ctx, int number, float badgeX, float badgeY, float badgeSize)
    {
        if (number is < 1 or > 9)
            return;

        var pattern = DigitPatterns[number - 1];
        const int cols = 5;
        const int rows = 7;
        var pixel = badgeSize / (Math.Max(cols, rows) + 2f);
        var originX = badgeX + (badgeSize - cols * pixel) / 2f;
        var originY = badgeY + (badgeSize - rows * pixel) / 2f;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                if (pattern[row * cols + col] != '1')
                    continue;

                ctx.Fill(
                    LabelText,
                    new RectangleF(originX + col * pixel, originY + row * pixel, pixel, pixel));
            }
        }
    }

    private static Font? CreateLabelFont()
    {
        foreach (var name in new[] { "Arial", "Segoe UI", "DejaVu Sans", "Liberation Sans", "FreeSans", "Noto Sans" })
        {
            if (SystemFonts.TryGet(name, out var family))
                return family.CreateFont(CellSize / 5f, FontStyle.Bold);
        }

        foreach (var path in LinuxFontPaths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var collection = new FontCollection();
                var family = collection.Add(path);
                return family.CreateFont(CellSize / 5f, FontStyle.Bold);
            }
            catch
            {
                // try next path
            }
        }

        return null;
    }

    private static readonly string[] LinuxFontPaths =
    [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/freefont/FreeSansBold.ttf"
    ];

    // Rows of 5 chars: '1' = lit pixel, '0' = off
    private static readonly string[] DigitPatterns =
    [
        "00100" + "01100" + "10100" + "00100" + "00100" + "00100" + "11100", // 1
        "01110" + "10001" + "00001" + "00110" + "01000" + "10000" + "11111", // 2
        "01110" + "10001" + "00001" + "00110" + "00001" + "10001" + "01110", // 3
        "10001" + "10001" + "10001" + "11111" + "00001" + "00001" + "00001", // 4
        "11111" + "10000" + "10000" + "11110" + "00001" + "00001" + "11110", // 5
        "01110" + "10000" + "10000" + "11110" + "10001" + "10001" + "01110", // 6
        "11111" + "00001" + "00010" + "00100" + "01000" + "10000" + "10000", // 7
        "01110" + "10001" + "10001" + "01110" + "10001" + "10001" + "01110", // 8
        "01110" + "10001" + "10001" + "01111" + "00001" + "00010" + "01100"  // 9
    ];
}

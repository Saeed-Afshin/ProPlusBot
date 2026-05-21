using System.Globalization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProPlusBot.Services.Media;

public sealed record SearchGridLayout(int Columns, int Rows, int JpegQuality)
{
    public int PageSize => SearchGridPresets.PageSize(Columns, Rows);
}

public class SearchResultGridComposer(IHttpClientFactory httpClientFactory, ILogger<SearchResultGridComposer> logger)
{
    private const int Gap = 4;
    private const int MinBadgeSize = 44;
    private const int MaxGridSide = 900;
    private static readonly Color PlaceholderColor = Color.FromRgb(48, 48, 52);
    private static readonly Color LabelBackground = Color.FromRgba(0, 0, 0, 170);
    private static readonly Color LabelText = Color.White;

    private static readonly Lazy<Font?> LabelFont = new(CreateLabelFont);

    public async Task<byte[]?> CreateGridJpegAsync(
        IReadOnlyList<MediaSearchResultItem> pageItems,
        SearchGridLayout layout,
        CancellationToken ct = default)
    {
        var itemCount = pageItems.Count;
        if (itemCount == 0)
            return null;

        var columns = layout.Columns;
        var usedRows = (itemCount + columns - 1) / columns;
        var cellSize = Math.Clamp(MaxGridSide / Math.Max(columns, usedRows), 120, 240);

        var cells = new Image<Rgba32>[itemCount];
        var client = httpClientFactory.CreateClient(nameof(SearchResultGridComposer));

        try
        {
            for (var i = 0; i < itemCount; i++)
            {
                if (!string.IsNullOrWhiteSpace(pageItems[i].ThumbnailUrl))
                {
                    cells[i] = await LoadThumbnailCellAsync(client, pageItems[i].ThumbnailUrl!, cellSize, ct)
                        ?? CreatePlaceholderCell(cellSize);
                }
                else
                {
                    cells[i] = CreatePlaceholderCell(cellSize);
                }

                DrawNumberLabel(cells[i], i + 1, cellSize);
            }

            var gridWidth = columns * cellSize + (columns - 1) * Gap;
            var gridHeight = usedRows * cellSize + (usedRows - 1) * Gap;
            using var grid = new Image<Rgba32>(gridWidth, gridHeight);

            for (var i = 0; i < itemCount; i++)
            {
                var row = i / columns;
                var col = i % columns;
                var x = col * (cellSize + Gap);
                var y = row * (cellSize + Gap);
                grid.Mutate(ctx => ctx.DrawImage(cells[i], new Point(x, y), 1f));
            }

            using var ms = new MemoryStream();
            var encoder = new JpegEncoder { Quality = layout.JpegQuality };
            await grid.SaveAsJpegAsync(ms, encoder, ct);
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
        int cellSize,
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
                Size = new Size(cellSize, cellSize),
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

    private static Image<Rgba32> CreatePlaceholderCell(int cellSize) =>
        new(cellSize, cellSize, PlaceholderColor);

    private static void DrawNumberLabel(Image<Rgba32> cell, int number, int cellSize)
    {
        var label = number.ToString(CultureInfo.InvariantCulture);

        if (LabelFont.Value is { } font)
        {
            var scaledFont = font.Family.CreateFont(Math.Min(cellSize / 6f, font.Size), FontStyle.Bold);
            var textSize = TextMeasurer.MeasureSize(label, new TextOptions(scaledFont));
            var padding = 8f;
            var badgeWidth = Math.Max(MinBadgeSize, textSize.Width + padding * 2);
            var badgeHeight = Math.Max(MinBadgeSize, textSize.Height + padding);
            var badgeX = cellSize - badgeWidth - 8f;
            var badgeY = 8f;
            var textX = badgeX + (badgeWidth - textSize.Width) / 2f;
            var textY = badgeY + (badgeHeight - textSize.Height) / 2f;

            cell.Mutate(ctx =>
            {
                ctx.Fill(LabelBackground, new RectangleF(badgeX, badgeY, badgeWidth, badgeHeight));
                ctx.DrawText(label, scaledFont, LabelText, new PointF(textX, textY));
            });
            return;
        }

        var bitmapBadgeWidth = Math.Max(MinBadgeSize, label.Length * 18f + 12f);
        var badgeXBitmap = cellSize - bitmapBadgeWidth - 8f;
        var badgeYBitmap = 8f;

        cell.Mutate(ctx =>
        {
            ctx.Fill(LabelBackground, new RectangleF(badgeXBitmap, badgeYBitmap, bitmapBadgeWidth, MinBadgeSize));
            DrawBitmapNumber(ctx, label, badgeXBitmap, badgeYBitmap, bitmapBadgeWidth, MinBadgeSize);
        });
    }

    private static void DrawBitmapNumber(
        IImageProcessingContext ctx,
        string text,
        float badgeX,
        float badgeY,
        float badgeWidth,
        float badgeHeight)
    {
        const int patternCols = 5;
        const int patternRows = 7;
        const float digitGap = 2f;
        var pixel = badgeHeight / (patternRows + 2f);
        var digitWidth = patternCols * pixel;
        var totalDigitsWidth = text.Length * digitWidth + (text.Length - 1) * digitGap;
        var originX = badgeX + (badgeWidth - totalDigitsWidth) / 2f;
        var originY = badgeY + (badgeHeight - patternRows * pixel) / 2f;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is < '0' or > '9')
                continue;

            var digit = text[i] - '0';
            var digitX = originX + i * (digitWidth + digitGap);
            DrawBitmapDigit(ctx, digit, digitX, originY, pixel);
        }
    }

    private static void DrawBitmapDigit(
        IImageProcessingContext ctx,
        int digit,
        float originX,
        float originY,
        float pixel)
    {
        if (digit is < 0 or > 9)
            return;

        var pattern = DigitPatterns[digit];
        const int cols = 5;
        const int rows = 7;

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
                return family.CreateFont(40, FontStyle.Bold);
        }

        foreach (var path in LinuxFontPaths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var collection = new FontCollection();
                var family = collection.Add(path);
                return family.CreateFont(40, FontStyle.Bold);
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

    // Index 0–9
    private static readonly string[] DigitPatterns =
    [
        "01110" + "10001" + "10001" + "10001" + "10001" + "10001" + "01110", // 0
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

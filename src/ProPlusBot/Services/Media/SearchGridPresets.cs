namespace ProPlusBot.Services.Media;

public static class SearchGridPresets
{
    public static readonly (int Columns, int Rows)[] Allowed =
    [
        (3, 3), (3, 4), (3, 5),
        (4, 3), (4, 4), (4, 5),
        (5, 3), (5, 4), (5, 5)
    ];

    public const int MinColumns = 3;
    public const int MaxColumns = 5;
    public const int MinRows = 3;
    public const int MaxRows = 5;
    public const int MinJpegQuality = 40;
    public const int MaxJpegQuality = 100;
    public const int DefaultJpegQuality = 85;

    public static int PageSize(int columns, int rows) => columns * rows;

    public static string Format(int columns, int rows) => $"{columns}×{rows}";

    public static bool IsAllowed(int columns, int rows) =>
        Allowed.Any(p => p.Columns == columns && p.Rows == rows);

    public static (int Columns, int Rows) Normalize(int columns, int rows)
    {
        if (IsAllowed(columns, rows))
            return (columns, rows);

        return (3, 3);
    }

    public static int NormalizeJpegQuality(int quality) =>
        Math.Clamp(quality, MinJpegQuality, MaxJpegQuality);
}

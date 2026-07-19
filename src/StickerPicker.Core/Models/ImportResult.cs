namespace StickerPicker.Core.Models;

public sealed class ImportResult
{
    public int Imported { get; init; }
    public int Duplicates { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> FailedPaths { get; init; } = [];
    public IReadOnlyList<string> ImportedRelativePaths { get; init; } = [];

    public int TotalProcessed => Imported + Duplicates + Failed;

    public string Summary =>
        $"导入 {Imported}，跳过重复 {Duplicates}，失败 {Failed}";
}

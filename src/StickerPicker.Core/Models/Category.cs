namespace StickerPicker.Core.Models;

/// <summary>
/// A sticker category. Real categories map to folders under library/;
/// the virtual "All" category aggregates every sticker.
/// </summary>
public sealed class Category
{
    public const string AllId = "__all__";
    public const string InboxName = "Inbox";

    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsVirtual { get; init; }
    public int StickerCount { get; init; }

    public static Category All(int count) => new()
    {
        Id = AllId,
        Name = "全部",
        IsVirtual = true,
        StickerCount = count,
    };

    public static Category FromFolder(string folderName, int count) => new()
    {
        Id = folderName,
        Name = folderName,
        IsVirtual = false,
        StickerCount = count,
    };
}

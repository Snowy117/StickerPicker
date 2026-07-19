using StickerPicker.Core.Models;

namespace StickerPicker.Core.Abstractions;

public interface IConfigStore
{
    AppConfig Load();
    void Save(AppConfig config);
}

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using StickerPicker.ViewModels;

namespace StickerPicker.Controls;

public partial class TagEditor : UserControl
{
    private readonly ObservableCollection<string> _tags = [];

    public TagEditor()
    {
        InitializeComponent();
        ChipsPanel.Children.Clear();
    }

    private StickerItemViewModel? Target => DataContext as StickerItemViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _tags.Clear();
        if (Target is { } target)
        {
            foreach (var tag in target.Sticker.Tags)
            {
                _tags.Add(tag);
            }
        }

        RebuildChips();
    }

    // jb emits mutually-exclusive MissingSomeEnumCasesNoDefault / HandlesSomeKnownEnumValuesWithDefault
    // for a switch on the 100+ value Key enum, so the if-chain stays.
    // ReSharper disable once ConvertIfStatementToSwitchStatement
    private void OnTagInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryAddTag(TagInput.Text);
            TagInput.Text = "";
            e.Handled = true;
            return;
        }

        // Inverting this compound guard would hurt readability (De Morgan on 3-way &&).
        // ReSharper disable once InvertIf
        if (e.Key == Key.Back
            && string.IsNullOrEmpty(TagInput.Text)
            && _tags.Count > 0)
        {
            _tags.RemoveAt(_tags.Count - 1);
            RebuildChips();
            e.Handled = true;
        }
    }

    private void TryAddTag(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value) || _tags.Contains(value))
        {
            return;
        }

        _tags.Add(value);
        RebuildChips();
    }

    private void OnChipRemove(string tag)
    {
        _tags.Remove(tag);
        RebuildChips();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => ApplyAndClose();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => CloseHost();

    private void RebuildChips()
    {
        ChipsPanel.Children.Clear();
        foreach (var tag in _tags)
        {
            ChipsPanel.Children.Add(BuildChip(tag));
        }
    }

    private Border BuildChip(string tag)
    {
        var label = new TextBlock
        {
            Text = tag,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        };
        var remove = new Button
        {
            Content = "×",
            Padding = new Thickness(4, 0),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            Classes = { "subtle" },
        };
        remove.Click += (_, _) => OnChipRemove(tag);

        var accent = ResolveBrush("SteamPanelAltBrush");
        var border = ResolveBrush("SteamBorderSoftBrush");
        return new Border
        {
            Background = accent,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(0, 0, 4, 4),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { label, remove },
            },
        };
    }

    private static IBrush? ResolveBrush(string key)
        => Application.Current?.FindResource(key) as IBrush;

    private void ApplyAndClose()
    {
        if (Target is { } target
            && TopLevel.GetTopLevel(this)?.DataContext is MainViewModel main
            && main.EditStickerTagsCommand.CanExecute((target, _tags.ToArray())))
        {
            _ = main.EditStickerTagsCommand.ExecuteAsync((target, _tags.ToArray()));
        }

        CloseHost();
    }

    private void CloseHost()
    {
        if (TopLevel.GetTopLevel(this)?.DataContext is MainViewModel main)
        {
            main.CloseTagEditor();
        }
    }
}

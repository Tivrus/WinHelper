using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TransparentHotkeyUtility.Infrastructure.Native;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Presentation.Figure;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using WF = System.Windows.Forms;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TransparentHotkeyUtility.Presentation.Windows;

public partial class PopupWindow
{
    private string? _selectedConfigId;

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_settingsMode)
        {
            var src = e.OriginalSource as DependencyObject;
            if (ConfirmClosePanel.Visibility == Visibility.Visible
                && !WpfUtils.IsDescendantOf(src, ConfirmClosePanel))
                ConfirmClosePanel.Visibility = Visibility.Collapsed;
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        if (!WpfUtils.IsDescendantOf(source, CircleLayer)
            && !WpfUtils.IsDescendantOf(source, InputCard))
            HidePopup();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+C / Ctrl+V для кружков — только в настройках и вне текстовых полей
        if (_settingsMode
            && Keyboard.Modifiers == ModifierKeys.Control
            && Keyboard.FocusedElement is not TextBox)
        {
            if (e.Key == Key.C)
            {
                CopySelectedCircle();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.V)
            {
                PasteCircle();
                e.Handled = true;
                return;
            }
        }

        if (e.Key != Key.Escape) return;
        e.Handled = true;

        if (_settingsMode)
        {
            if (ConfirmClosePanel.Visibility == Visibility.Visible)
                ConfirmClosePanel.Visibility = Visibility.Collapsed;
            return;
        }

        HidePopup();
    }

    // ── Буфер обмена кружков (Ctrl+C / Ctrl+V, работает между страницами) ────

    private CircleConfig? _circleClipboard;

    private void CopySelectedCircle()
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;
        _circleClipboard = DeepCloneCircle(cfg);
    }

    private void PasteCircle()
    {
        if (_circleClipboard is null) return;

        var clone = DeepCloneCircle(_circleClipboard);
        RegenerateCircleIds(clone);

        var selected = GetCurrentCfg();

        // Выделена группа — вставляем внутрь неё
        if (selected is not null && selected.Type == CircleType.Group)
        {
            clone.OffsetX = 0;
            clone.OffsetY = -120;
            selected.Children.Add(clone);
            RebuildFigureInSettings();
            SelectConfig(clone);
            return;
        }

        // Выделен обычный кружок — вставляем рядом (в ту же группу или корень)
        if (selected is not null)
        {
            var parent = GetParentGroup(selected);
            if (parent is not null)
            {
                parent.Children.Add(clone);
                RebuildFigureInSettings();
                SelectConfig(clone);
                return;
            }
        }

        // Ничего не выделено — в корень текущей страницы
        CurrentCircles.Add(clone);
        RebuildFigureInSettings();
        SelectConfig(clone);
    }

    private static CircleConfig DeepCloneCircle(CircleConfig cfg)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(cfg);
        return System.Text.Json.JsonSerializer.Deserialize<CircleConfig>(json)!;
    }

    private static void RegenerateCircleIds(CircleConfig cfg)
    {
        cfg.Id = Guid.NewGuid().ToString("N");
        foreach (var child in cfg.Children)
            RegenerateCircleIds(child);
    }

    private CircleConfig? GetCurrentCfg()
    {
        if (_selectedConfigId is null) return null;
        return FindConfigById(CurrentCircles, _selectedConfigId);
    }

    private CircleConfig? FindConfigById(IEnumerable<CircleConfig> list, string id)
    {
        foreach (var c in list)
        {
            if (c.Id == id) return c;
            if (FindConfigById(c.Children, id) is { } found)
                return found;
        }
        return null;
    }

    private CircleConfig? GetParentGroup(CircleConfig target)
    {
        foreach (var root in CurrentCircles)
        {
            if (ReferenceEquals(root, target)) return null;
            if (FindParent(root, target) is { } found)
                return found;
        }
        return null;

        CircleConfig? FindParent(CircleConfig current, CircleConfig tgt)
        {
            if (current.Children.Contains(tgt)) return current;
            foreach (var child in current.Children)
            {
                if (FindParent(child, tgt) is { } p) return p;
            }
            return null;
        }
    }

    private void SelectConfig(CircleConfig? cfg)
    {
        _selectedConfigId = cfg?.Id;
        
        BtnDeleteCircle.IsEnabled = cfg is not null;

        foreach (var node in _figure.GetAllNodes())
            CircleElementFactory.SetSelected(node.Visual, ReferenceEquals(node.Config, cfg));

        if (cfg is null)
        {
            CirclePropsPanel.Visibility = Visibility.Collapsed;
            SettingsHintText.Visibility = Visibility.Visible;
            return;
        }

        SettingsHintText.Visibility = Visibility.Collapsed;
        CirclePropsPanel.Visibility = Visibility.Visible;

        PopulatePropsFields(cfg);
        
        bool isGroup = cfg.Type == CircleType.Group;
        ChildrenSection.Visibility = isGroup ? Visibility.Visible : Visibility.Collapsed;
        if (isGroup) RefreshChildList(cfg);

        EnsureNodeVisibleInSettings(cfg);
    }

    private void EnsureNodeVisibleInSettings(CircleConfig target)
    {
        if (!_settingsMode) return;

        // Сначала собираем список предков сверху вниз
        var path = new List<CircleConfig>();
        var parent = GetParentGroup(target);
        while (parent is not null)
        {
            path.Insert(0, parent);
            parent = GetParentGroup(parent);
        }

        // Раскрываем сверху вниз, чтобы узлы успевали создаваться
        foreach (var p in path)
        {
            var pNode = _figure.FindNode(p);
            if (pNode is not null && !pNode.IsExpanded)
                _figure.ExpandGroupImmediate(pNode);
        }

        // Если выбрали саму группу, раскрываем её
        if (target.Type == CircleType.Group)
        {
            var node = _figure.FindNode(target);
            if (node is not null && !node.IsExpanded)
                _figure.ExpandGroupImmediate(node);
        }

        // Переподключаем Drag для новых узлов
        _drag.EnableForTree(_figure.RootNodes, SelectConfig);

        // Новые узлы после Expand — в режим настроек + кольцо на выбранном
        ApplySettingsVisualMode(target);
    }

    /// <summary>В настройках: без hover-scale, выделение пунктирным кольцом.</summary>
    private void ApplySettingsVisualMode(CircleConfig? selected)
    {
        foreach (var n in _figure.GetAllNodes())
        {
            CircleElementFactory.SetSettingsMode(n.Visual, true);
            CircleElementFactory.SetSelected(n.Visual, selected is not null && ReferenceEquals(n.Config, selected));
        }
    }

    private void PopulatePropsFields(CircleConfig cfg)
    {
        _suppressCircleTypeChange = true;
        RbTypeAction.IsChecked = cfg.Type == CircleType.FireAndForget;
        RbTypeModal.IsChecked = cfg.Type == CircleType.Modal;
        RbTypeGroup.IsChecked = cfg.Type == CircleType.Group;
        _suppressCircleTypeChange = false;

        PropLabel.Text = cfg.Label ?? string.Empty;
        PropRadius.Text = ((int)Math.Round(cfg.Radius)).ToString();
        PropColor.Text = cfg.Color ?? "#8C4BFF";
        PropExePath.Text = cfg.ExecutablePath ?? string.Empty;
        UpdateColorSwatch(cfg.Color);

        SyncFormFieldsSectionVisibility(cfg);
    }

    private void SyncFormFieldsSectionVisibility(CircleConfig cfg)
    {
        ExeSection.Visibility = cfg.Type != CircleType.Group ? Visibility.Visible : Visibility.Collapsed;
        bool showForm = cfg.Type == CircleType.Modal || cfg.Type == CircleType.Group;
        FormFieldsSection.Visibility = showForm ? Visibility.Visible : Visibility.Collapsed;
        if (showForm) RefreshFormFieldsList(cfg);
        else FormFieldListPanel.Children.Clear();
    }

    private void PropField_LostFocus(object sender, RoutedEventArgs e) => ApplyCircleProps();

    private void CircleTypeBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressCircleTypeChange) return;
        ApplyCircleProps();
        if (GetCurrentCfg() is { } cfg)
            SyncFormFieldsSectionVisibility(cfg);
    }

    private void RefreshChildList(CircleConfig groupCfg)
    {
        ChildListPanel.Children.Clear();
        foreach (var child in groupCfg.Children)
        {
            string typeIcon = child.Type switch
            {
                CircleType.Modal => "≡ ",
                CircleType.Group => "▶ ",
                _ => "▸ ",
            };

            var btn = new Button
            {
                Content = typeIcon + (child.Label ?? "•"),
                Style = (Style)FindResource("ChildItemBtn"),
            };

            var capturedChild = child;
            btn.Click += (_, _) => SelectConfig(capturedChild);
            ChildListPanel.Children.Add(btn);
        }
    }

    private void BtnRadiusUp_Click(object sender, RoutedEventArgs e) => AdjustRadius(+1);
    private void BtnRadiusDown_Click(object sender, RoutedEventArgs e) => AdjustRadius(-1);

    private void PropRadius_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        AdjustRadius(e.Delta > 0 ? +1 : -1);
        e.Handled = true;
    }

    private void AdjustRadius(int delta)
    {
        if (!double.TryParse(PropRadius.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double r))
            r = 45;
        PropRadius.Text = ((int)Math.Max(8, r + delta)).ToString();
        ApplyCircleProps();
    }

    private void PropColorSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedConfigId is null) return;

        _suppressDeactivated = true;
        try
        {
            using var dlg = new WF.ColorDialog { FullOpen = true };
            TryPresetDialogColor(dlg, PropColor.Text);

            var owner = new Win32Window(new WindowInteropHelper(this).Handle);
            if (dlg.ShowDialog(owner) == WF.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                PropColor.Text = hex;
                UpdateColorSwatch(hex);
                ApplyCircleProps();
            }
        }
        finally
        {
            _suppressDeactivated = false;
            Activate();
        }
    }

    private void PropColor_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateColorSwatch(PropColor.Text);

    private void UpdateColorSwatch(string? hex)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(hex ?? "#8C4BFF"); }
        catch { c = Color.FromRgb(140, 75, 255); }
        PropColorSwatch.Background = new SolidColorBrush(c);
    }

    private void ApplyCircleProps()
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;

        cfg.Label = PropLabel.Text;
        if (double.TryParse(PropRadius.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double r) && r > 0)
            cfg.Radius = r;
        try { ColorConverter.ConvertFromString(PropColor.Text); cfg.Color = PropColor.Text; } catch { }

        cfg.Type = RbTypeGroup.IsChecked == true ? CircleType.Group
                 : RbTypeModal.IsChecked == true ? CircleType.Modal
                 : CircleType.FireAndForget;

        cfg.ExecutablePath = string.IsNullOrWhiteSpace(PropExePath.Text)
            ? null : PropExePath.Text.Trim();

        RebuildFigureInSettings();
    }

    private void RebuildFigureInSettings()
    {
        var savedId = _selectedConfigId;
        _figure.Build(_anchorCx, _anchorCy, CurrentCircles, OnCircleActivated);
        ApplySettingsVisualMode(null);
        _drag.EnableForTree(_figure.RootNodes, SelectConfig);
        SelectConfig(savedId != null ? FindConfigById(CurrentCircles, savedId) : null);
    }

    private void BtnAddCircle_Click(object sender, RoutedEventArgs e)
    {
        var current = GetCurrentCfg();
        var targetGroup = current?.Type == CircleType.Group ? current : GetParentGroup(current ?? new CircleConfig());

        if (targetGroup is not null)
        {
            var newChild = new CircleConfig
            {
                OffsetX = 0, OffsetY = -120, Radius = 35,
                Color = "#8C4BFF", Label = "new",
            };
            targetGroup.Children.Add(newChild);
            RebuildFigureInSettings();
            SelectConfig(newChild);
            return;
        }

        var newRoot = new CircleConfig
        {
            OffsetX = 0, OffsetY = -200, Radius = 45,
            Color = "#8C4BFF", Label = "new",
        };
        CurrentCircles.Add(newRoot);
        RebuildFigureInSettings();
        SelectConfig(newRoot);
    }

    private void BtnDeleteCircle_Click(object sender, RoutedEventArgs e)
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;

        var parent = GetParentGroup(cfg);
        if (parent is not null)
        {
            parent.Children.Remove(cfg);
            RebuildFigureInSettings();
            SelectConfig(parent);
            return;
        }

        CurrentCircles.Remove(cfg);
        RebuildFigureInSettings();
        SelectConfig(null);
    }

    private void RefreshFormFieldsList(CircleConfig cfg)
    {
        FormFieldListPanel.Children.Clear();
        for (int i = 0; i < cfg.FormFields.Count; i++)
            FormFieldListPanel.Children.Add(BuildFormFieldCard(cfg.FormFields[i], cfg));
    }

    private Border BuildFormFieldCard(FormFieldConfig field, CircleConfig parent)
    {
        var rbText = MakeFieldTypeBtn("Текст", field.FieldType == FormFieldType.TextInput);
        var rbFolder = MakeFieldTypeBtn("Папка", field.FieldType == FormFieldType.FolderPath);
        var rbCheck = MakeFieldTypeBtn("Флажок", field.FieldType == FormFieldType.Checkbox);
        var rbLabel = MakeFieldTypeBtn("Метка", field.FieldType == FormFieldType.Label);

        var typeHost = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(0x24, 0x20, 0x3A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x3E, 0x70)),
            BorderThickness = new Thickness(1),
            Height = 26,
            ClipToBounds = true,
        };
        var typeGrid = new UniformGrid { Columns = 4 };
        typeGrid.Children.Add(rbText);
        typeGrid.Children.Add(rbFolder);
        typeGrid.Children.Add(rbCheck);
        typeGrid.Children.Add(rbLabel);
        typeHost.Child = typeGrid;

        var delBtn = new Button
        {
            Content = "🗑",
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            Cursor = Cursors.Hand,
            ToolTip = "Удалить поле",
            VerticalAlignment = VerticalAlignment.Center,
        };
        delBtn.Click += (_, _) => { parent.FormFields.Remove(field); RefreshFormFieldsList(parent); };

        var topRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(typeHost, 0); topRow.Children.Add(typeHost);
        Grid.SetColumn(delBtn, 1); topRow.Children.Add(delBtn);

        var labelTb = new TextBox
        {
            Text = field.Label,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE1, 0xD7, 0xFF)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xAA, 0xFF)),
            SelectionBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x40, 0xFF)),
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 0, 8, 0),
        };
        labelTb.LostFocus += (_, _) => field.Label = labelTb.Text;

        var labelBorder = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(0x24, 0x20, 0x3A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x3E, 0x70)),
            BorderThickness = new Thickness(1),
            Height = 28,
            Child = labelTb,
        };

        var sp = new StackPanel();
        sp.Children.Add(topRow);
        sp.Children.Add(MakeCaption("Название"));
        sp.Children.Add(labelBorder);

        if (field.FieldType != FormFieldType.Label)
        {
            sp.Children.Add(MakeCaption("По умолчанию"));
            sp.Children.Add(BuildDefaultSection(field));
        }

        if (field.FieldType != FormFieldType.Label)
        {
            var remChk = new CheckBox
            {
                Content = "Запомнить значение",
                IsChecked = field.Remember,
                Style = (Style)FindResource("PurpleCheckBox"),
                Margin = new Thickness(0, 6, 0, 0),
            };
            remChk.Checked += (_, _) => field.Remember = true;
            remChk.Unchecked += (_, _) => field.Remember = false;
            sp.Children.Add(remChk);

            var reqChk = new CheckBox
            {
                Content = "Обязательное поле",
                IsChecked = field.Required,
                Style = (Style)FindResource("PurpleCheckBox"),
                Margin = new Thickness(0, 4, 0, 0),
            };
            reqChk.Checked += (_, _) => field.Required = true;
            reqChk.Unchecked += (_, _) => field.Required = false;
            sp.Children.Add(reqChk);
        }

        rbText.Checked += (_, _) => { if (field.FieldType != FormFieldType.TextInput) { field.FieldType = FormFieldType.TextInput; RefreshFormFieldsList(parent); } };
        rbFolder.Checked += (_, _) => { if (field.FieldType != FormFieldType.FolderPath) { field.FieldType = FormFieldType.FolderPath; RefreshFormFieldsList(parent); } };
        rbCheck.Checked += (_, _) => { if (field.FieldType != FormFieldType.Checkbox) { field.FieldType = FormFieldType.Checkbox; RefreshFormFieldsList(parent); } };
        rbLabel.Checked += (_, _) => { if (field.FieldType != FormFieldType.Label) { field.FieldType = FormFieldType.Label; RefreshFormFieldsList(parent); } };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x20, 0x1C, 0x35)),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x60)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 6),
            Child = sp,
        };
    }

    private FrameworkElement BuildDefaultSection(FormFieldConfig field)
    {
        if (field.FieldType == FormFieldType.Checkbox)
        {
            var chk = new CheckBox
            {
                Content = "Включён по умолчанию",
                IsChecked = field.DefaultValue == "true",
                Style = (Style)FindResource("PurpleCheckBox"),
                Margin = new Thickness(0, 2, 0, 0),
            };
            chk.Checked += (_, _) => field.DefaultValue = "true";
            chk.Unchecked += (_, _) => field.DefaultValue = "false";
            return chk;
        }

        if (field.FieldType == FormFieldType.FolderPath)
        {
            var folderTb = new TextBox
            {
                Text = field.DefaultValue,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE1, 0xD7, 0xFF)),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xAA, 0xFF)),
                SelectionBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x40, 0xFF)),
                BorderThickness = new Thickness(0),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
            };
            folderTb.LostFocus += (_, _) => field.DefaultValue = folderTb.Text;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            var textBorder = new Border
            {
                CornerRadius = new CornerRadius(5, 0, 0, 5),
                Background = new SolidColorBrush(Color.FromRgb(0x24, 0x20, 0x3A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x3E, 0x70)),
                BorderThickness = new Thickness(1, 1, 0, 1),
                Height = 28,
                Child = folderTb,
            };
            Grid.SetColumn(textBorder, 0);
            grid.Children.Add(textBorder);

            var browseBtn = new Button
            {
                Content = "…",
                Style = (Style)FindResource("BrowseBtn"),
                Height = 28,
                Width = 30,
            };
            browseBtn.Click += (_, _) =>
            {
                SuppressDeactivatedWhile(() =>
                {
                    using var dlg = new WF.FolderBrowserDialog();
                    if (!string.IsNullOrWhiteSpace(folderTb.Text) && Directory.Exists(folderTb.Text))
                        dlg.SelectedPath = folderTb.Text;
                    var owner = new Win32Window(new WindowInteropHelper(this).Handle);
                    if (dlg.ShowDialog(owner) == WF.DialogResult.OK)
                    {
                        folderTb.Text = dlg.SelectedPath;
                        field.DefaultValue = folderTb.Text;
                    }
                });
            };
            Grid.SetColumn(browseBtn, 1);
            grid.Children.Add(browseBtn);
            return grid;
        }

        var textTb = new TextBox
        {
            Text = field.DefaultValue,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE1, 0xD7, 0xFF)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xAA, 0xFF)),
            SelectionBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x40, 0xFF)),
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 0, 8, 0),
        };
        textTb.LostFocus += (_, _) => field.DefaultValue = textTb.Text;

        return new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(0x24, 0x20, 0x3A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x3E, 0x70)),
            BorderThickness = new Thickness(1),
            Height = 28,
            Child = textTb,
        };
    }

    private System.Windows.Controls.RadioButton MakeFieldTypeBtn(string text, bool isChecked) => new()
    {
        Content = text,
        IsChecked = isChecked,
        GroupName = Guid.NewGuid().ToString(),
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
        FontSize = 10,
        Cursor = Cursors.Hand,
        Style = (Style)FindResource("CircleTypeBtn"),
    };

    private static TextBlock MakeCaption(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x80, 0xCC)),
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
        FontSize = 10,
        Margin = new Thickness(0, 5, 0, 3),
    };

    private void BtnAddTextField_Click(object sender, RoutedEventArgs e)
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;
        cfg.FormFields.Add(new FormFieldConfig { FieldType = FormFieldType.TextInput, Label = "Поле" });
        RefreshFormFieldsList(cfg);
    }

    private void BtnAddCheckboxField_Click(object sender, RoutedEventArgs e)
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;
        cfg.FormFields.Add(new FormFieldConfig { FieldType = FormFieldType.Checkbox, Label = "Флажок" });
        RefreshFormFieldsList(cfg);
    }

    private void BtnAddFolderField_Click(object sender, RoutedEventArgs e)
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;
        cfg.FormFields.Add(new FormFieldConfig { FieldType = FormFieldType.FolderPath, Label = "Папка" });
        RefreshFormFieldsList(cfg);
    }

    private void BtnAddLabelField_Click(object sender, RoutedEventArgs e)
    {
        var cfg = GetCurrentCfg();
        if (cfg is null) return;
        cfg.FormFields.Add(new FormFieldConfig { FieldType = FormFieldType.Label, Label = "Текст" });
        RefreshFormFieldsList(cfg);
    }
}

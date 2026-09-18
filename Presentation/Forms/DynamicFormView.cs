using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TransparentHotkeyUtility.Infrastructure.Native;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Presentation.Windows;
using Color = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPanel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using GridLength = System.Windows.GridLength;
using GridUnitType = System.Windows.GridUnitType;
using WF = System.Windows.Forms;

namespace TransparentHotkeyUtility.Presentation.Forms;

/// <summary>Сборка динамической формы по <see cref="FormFieldConfig"/> и чтение аргументов для .exe.</summary>
internal static class DynamicFormView
{
    public static void Fill(
        WpfPanel                   host,
        Dictionary<FormFieldConfig, FrameworkElement> controls,
        FrameworkElement           resourceScope,
        CircleConfig               circle,
        IReadOnlyList<CircleConfig>? groupPrefixes)
    {
        host.Children.Clear();
        controls.Clear();

        var groupSectionIndex = 0;
        var anyGroupFields    = false;
        if (groupPrefixes is not null)
        {
            foreach (var g in groupPrefixes)
            {
                if (g.FormFields.Count == 0)
                    continue;

                host.Children.Add(MakeSectionCaption(
                    groupSectionIndex++ == 0 ? "Группа" : "Подгруппа",
                    top: anyGroupFields ? 8 : 0,
                    bottom: 8));
                foreach (var field in g.FormFields)
                    AddField(host, controls, resourceScope, field);
                anyGroupFields = true;
            }
        }

        if (circle.FormFields.Count > 0 && anyGroupFields)
            host.Children.Add(MakeSectionCaption("Кружок", top: 8, bottom: 8));

        foreach (var field in circle.FormFields)
            AddField(host, controls, resourceScope, field);
    }

    public static void AppendArgumentParts(
        CircleConfig                                            cfg,
        IReadOnlyDictionary<FormFieldConfig, FrameworkElement> controls,
        List<string>                                            argumentParts,
        ref bool                                                needSave)
    {
        foreach (var field in cfg.FormFields)
        {
            if (field.FieldType == FormFieldType.Label) continue;
            if (!controls.TryGetValue(field, out var ctrl)) continue;

            switch (field.FieldType)
            {
                case FormFieldType.Checkbox when ctrl is CheckBox cb:
                    argumentParts.Add(cb.IsChecked == true ? "true" : "false");
                    if (field.Remember)
                    {
                        field.SavedValue = cb.IsChecked == true ? "true" : "false";
                        needSave         = true;
                    }
                    break;

                case FormFieldType.TextInput:
                case FormFieldType.FolderPath:
                    if (ctrl is not TextBox textBox) break;
                    argumentParts.Add(textBox.Text);
                    if (field.Remember)
                    {
                        field.SavedValue = textBox.Text;
                        needSave         = true;
                    }
                    break;
            }
        }
    }

    public static void AppendStoredArgumentParts(
        CircleConfig cfg,
        List<string> argumentParts)
    {
        foreach (var field in cfg.FormFields)
        {
            if (field.FieldType == FormFieldType.Label)
                continue;

            switch (field.FieldType)
            {
                case FormFieldType.Checkbox:
                    var checkboxValue = field.Remember && field.SavedValue is not null
                        ? field.SavedValue
                        : field.DefaultValue;
                    argumentParts.Add(checkboxValue == "true" ? "true" : "false");
                    break;

                case FormFieldType.TextInput:
                case FormFieldType.FolderPath:
                    var textValue = field.Remember && field.SavedValue is not null
                        ? field.SavedValue
                        : field.DefaultValue;
                    argumentParts.Add(textValue ?? string.Empty);
                    break;
            }
        }
    }

    public static bool TryValidateRequired(
        CircleConfig                                            cfg,
        IReadOnlyDictionary<FormFieldConfig, FrameworkElement> controls,
        out string                                              error)
    {
        foreach (var field in cfg.FormFields)
        {
            if (!field.Required || field.FieldType == FormFieldType.Label) continue;
            if (!controls.TryGetValue(field, out var ctrl)) continue;

            switch (field.FieldType)
            {
                case FormFieldType.Checkbox when ctrl is CheckBox cb:
                    if (cb.IsChecked != true)
                    {
                        error = $"Поле '{field.Label}' обязательно.";
                        return false;
                    }
                    break;

                case FormFieldType.TextInput:
                case FormFieldType.FolderPath:
                    if (ctrl is not TextBox textBox) break;
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        error = $"Поле '{field.Label}' обязательно.";
                        return false;
                    }
                    break;
            }
        }

        error = string.Empty;
        return true;
    }

    private static TextBlock MakeSectionCaption(string text, double top = 0, double bottom = 8) =>
        new()
        {
            Text         = text,
            Foreground   = new SolidColorBrush(Color.FromRgb(0x9A, 0x80, 0xCC)),
            FontFamily   = new WpfFontFamily("Segoe UI"),
            FontSize     = 11,
            Margin       = new Thickness(0, top, 0, bottom),
        };

    private static void AddField(
        WpfPanel                                   host,
        Dictionary<FormFieldConfig, FrameworkElement> controls,
        FrameworkElement                           resourceScope,
        FormFieldConfig                            field)
    {
        switch (field.FieldType)
        {
            case FormFieldType.Label:
                host.Children.Add(new TextBlock
                {
                    Text         = field.Label,
                    Foreground   = new SolidColorBrush(Color.FromRgb(0xC8, 0xAA, 0xFF)),
                    FontFamily   = new WpfFontFamily("Segoe UI"),
                    FontSize     = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 0, 0, 8),
                });
                break;

            case FormFieldType.Checkbox:
                var cbVal = (field.Remember && field.SavedValue is not null)
                    ? field.SavedValue
                    : field.DefaultValue;
                var chk = new CheckBox
                {
                    Content   = field.Label,
                    IsChecked = cbVal == "true",
                    Style     = (Style)resourceScope.FindResource("PurpleCheckBox"),
                };
                host.Children.Add(chk);
                controls[field] = chk;
                break;

            case FormFieldType.TextInput:
            case FormFieldType.FolderPath:
            default:
                var txtVal = (field.Remember && field.SavedValue is not null)
                    ? field.SavedValue
                    : field.DefaultValue;
                host.Children.Add(new TextBlock
                {
                    Text       = field.Label,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xAA, 0xFF)),
                    FontFamily = new WpfFontFamily("Segoe UI"),
                    FontSize   = 12,
                    Margin     = new Thickness(0, 0, 0, 6),
                });
                var txt = new TextBox
                {
                    Text  = txtVal,
                    Style = (Style)resourceScope.FindResource("HintBox"),
                };
                if (field.FieldType == FormFieldType.FolderPath)
                {
                    var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

                    var txtBorder = new Border
                    {
                        CornerRadius    = new CornerRadius(8, 0, 0, 8),
                        Background      = new SolidColorBrush(Color.FromRgb(0x16, 0x09, 0x1E)),
                        BorderBrush     = new SolidColorBrush(Color.FromRgb(0x6E, 0x46, 0xDC)),
                        BorderThickness = new Thickness(1.4, 1.4, 0, 1.4),
                        Height          = 32,
                        Child           = txt,
                    };
                    Grid.SetColumn(txtBorder, 0);
                    grid.Children.Add(txtBorder);

                    var browseBtn = new Button
                    {
                        Content = "…",
                        Height  = 32,
                        Style   = (Style)resourceScope.FindResource("BrowseBtn"),
                    };
                    browseBtn.Click += (_, _) =>
                    {
                        void ShowDlg()
                        {
                            using var dlg = new WF.FolderBrowserDialog();
                            if (!string.IsNullOrWhiteSpace(txt.Text) && Directory.Exists(txt.Text))
                                dlg.SelectedPath = txt.Text;
                            var wnd = Window.GetWindow(resourceScope);
                            var result = wnd is not null
                                ? dlg.ShowDialog(new Win32Window(new WindowInteropHelper(wnd).Handle))
                                : dlg.ShowDialog();
                            if (result == WF.DialogResult.OK)
                                txt.Text = dlg.SelectedPath;
                        }

                        if (resourceScope is PopupWindow popup)
                            popup.SuppressDeactivatedWhile(ShowDlg);
                        else
                            ShowDlg();
                    };
                    Grid.SetColumn(browseBtn, 1);
                    grid.Children.Add(browseBtn);

                    host.Children.Add(grid);
                }
                else
                {
                    var txtBorder = new Border
                    {
                        CornerRadius    = new CornerRadius(8),
                        Background      = new SolidColorBrush(Color.FromRgb(0x16, 0x09, 0x1E)),
                        BorderBrush     = new SolidColorBrush(Color.FromRgb(0x6E, 0x46, 0xDC)),
                        BorderThickness = new Thickness(1.4),
                        Height          = 32,
                        Margin          = new Thickness(0, 0, 0, 12),
                        Child           = txt,
                    };
                    host.Children.Add(txtBorder);
                }
                controls[field] = txt;
                break;
        }
    }
}

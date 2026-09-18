using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TransparentHotkeyUtility.Infrastructure;
using TransparentHotkeyUtility.Models;
using TransparentHotkeyUtility.Presentation.Figure;
using TransparentHotkeyUtility.Presentation.Forms;
using TransparentHotkeyUtility.Services;

using TextBox = System.Windows.Controls.TextBox;

namespace TransparentHotkeyUtility.Presentation.Windows;

/// <summary>Клики по кружкам, внешние .exe/.ahk, модальная форма с динамическими полями.</summary>
public partial class PopupWindow
{
    /// <summary>
    /// Все группы-предки <paramref name="target"/> от корня к прямому родителю,
    /// у которых есть поля формы (порядок = порядок префикса аргументов).
    /// </summary>
    private List<CircleConfig> GetAncestorGroupsWithFormFields(CircleConfig target)
    {
        foreach (var root in CurrentCircles)
        {
            if (ReferenceEquals(root, target))
                return [];
        }

        foreach (var root in CurrentCircles)
        {
            var ordered = new List<CircleConfig>();
            if (TryCollectAncestorGroups(root, target, ordered))
                return ordered;
        }

        return [];
    }

    private static bool TryCollectAncestorGroups(CircleConfig node, CircleConfig target, List<CircleConfig> orderedGroups)
    {
        foreach (var child in node.Children)
        {
            if (ReferenceEquals(child, target))
            {
                if (node.Type == CircleType.Group && node.FormFields.Count > 0)
                    orderedGroups.Add(node);
                return true;
            }

            if (!TryCollectAncestorGroups(child, target, orderedGroups))
                continue;

            if (node.Type == CircleType.Group && node.FormFields.Count > 0)
                orderedGroups.Insert(0, node);
            return true;
        }

        return false;
    }

    private void OnCircleActivated(CircleConfig cfg)
    {
        if (_settingsMode) return;

        var node = _figure.FindNode(cfg);
        if (node is null) return;

        if (cfg.Type == CircleType.Group)
        {
            if (node.IsExpanded && _activeModalConfig is not null)
                ExitModalMode(instant: true);

            _figure.ToggleGroup(node);

            if (cfg.FormFields.Count > 0)
            {
                if (node.IsExpanded)
                {
                    if (_activeModalConfig != cfg)
                    {
                        if (_activeModalConfig is not null) ExitModalMode();
                        EnterModalMode(cfg, GetAncestorGroupsWithFormFields(cfg), centerCircle: false);
                    }
                }
                else if (_activeModalConfig == cfg)
                {
                    ExitModalMode();
                }
            }
            else if (_activeModalConfig == cfg)
            {
                ExitModalMode();
            }

            // Если открыли подгруппу, а до этого была открыта форма другой подгруппы
            if (node.IsExpanded && _activeModalConfig is not null && _activeModalConfig != cfg)
            {
                // Закрыть старую модалку если она не предок этой группы
                ExitModalMode(instant: true);
            }

            return;
        }

        var hasExe = !string.IsNullOrWhiteSpace(cfg.ExecutablePath);
        var needsModalForm = cfg.Type == CircleType.Modal && cfg.FormFields.Count > 0;
        var ancestorGroups = GetAncestorGroupsWithFormFields(cfg);

        if (needsModalForm)
        {
            if (_activeModalConfig == cfg) { ExitModalMode(); return; }
            if (_activeModalConfig is not null) ExitModalMode();
            EnterModalMode(cfg, ancestorGroups);
            return;
        }

        if (cfg.Type == CircleType.FireAndForget && hasExe)
        {
            var argumentParts = new List<string>();
            foreach (var g in ancestorGroups)
                DynamicFormView.AppendStoredArgumentParts(g, argumentParts);
            DynamicFormView.AppendStoredArgumentParts(cfg, argumentParts);

            // Сначала процесс — потом UI, чтобы скрипт стартовал без ожидания HidePopup.
            ExternalProcessLauncher.Start(cfg.ExecutablePath!, argumentParts);
            HidePopup();
            return;
        }

        if (cfg.Type == CircleType.Modal && cfg.FormFields.Count == 0 && hasExe)
        {
            var argumentParts = new List<string>();
            foreach (var g in ancestorGroups)
                DynamicFormView.AppendStoredArgumentParts(g, argumentParts);
            DynamicFormView.AppendStoredArgumentParts(cfg, argumentParts);

            ExternalProcessLauncher.Start(cfg.ExecutablePath!, argumentParts);
            HidePopup();
            return;
        }
    }

    private void EnterModalMode(
        CircleConfig cfg,
        IReadOnlyList<CircleConfig>? groupPrefixes = null,
        bool centerCircle = true)
    {
        _activeModalConfig  = cfg;
        _activeModalGroups  = groupPrefixes is { Count: > 0 }
            ? groupPrefixes.Where(g => g.Type == CircleType.Group && g.FormFields.Count > 0).ToList()
            : null;
        _activeModalNode = _figure.FindNode(cfg);

        DynamicFormView.Fill(DynamicFieldsHost, _dynamicControls, this, cfg, _activeModalGroups);

        BtnConfirmModal.Visibility   = Visibility.Visible;
        MainFieldsPanel.Visibility  = Visibility.Visible;
        DynamicFormPanel.Visibility = Visibility.Visible;
        TxtModalError.Text          = string.Empty;
        TxtModalError.Visibility    = Visibility.Collapsed;

        if (_activeModalNode is not null)
        {
            _figure.MagnetInteractive = false;
            _figure.ResetMagnet();
            CircleElementFactory.SetPinned(_activeModalNode.Visual, true);
            if (centerCircle)
                _figure.AnimateToCenter(_activeModalNode, _anchorCx, _anchorCy, PositionInputCard);
        }
        
        SyncInputCardVisibility();
        PositionInputCard();

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var first = DynamicFieldsHost.Children.OfType<Border>()
                .Select(b => b.Child as TextBox)
                .FirstOrDefault(t => t is not null);
            first?.Focus();
        });
    }

    private void ExitModalMode(bool instant = false)
    {
        if (_activeModalConfig is null) return;

        var node = _activeModalNode;
        _activeModalConfig  = null;
        _activeModalGroups  = null;
        _activeModalNode = null;
        _dynamicControls.Clear();
        DynamicFieldsHost.Children.Clear();

        if (node is not null)
            CircleElementFactory.SetPinned(node.Visual, false);

        DynamicFormPanel.Visibility = Visibility.Collapsed;
        MainFieldsPanel.Visibility  = Visibility.Collapsed;
        SyncInputCardVisibility();
        PositionInputCard();

        if (node is not null)
        {
            if (instant)
            {
                _figure.SnapToRest(node);
                _figure.MagnetInteractive = true;
            }
            else
            {
                _figure.AnimateToRest(node, PositionInputCard);
            }
        }
    }

    private void BtnConfirmModal_Click(object sender, RoutedEventArgs e)
    {
        if (_activeModalConfig is null) return;

        TxtModalError.Visibility = Visibility.Collapsed;
        TxtModalError.Text       = string.Empty;

        if (_activeModalGroups is not null)
        {
            foreach (var g in _activeModalGroups)
            {
                if (!DynamicFormView.TryValidateRequired(g, _dynamicControls, out var groupRequiredError))
                {
                    TxtModalError.Text       = groupRequiredError;
                    TxtModalError.Visibility = Visibility.Visible;
                    return;
                }
            }
        }

        if (!DynamicFormView.TryValidateRequired(_activeModalConfig, _dynamicControls, out var requiredError))
        {
            TxtModalError.Text       = requiredError;
            TxtModalError.Visibility = Visibility.Visible;
            return;
        }

        var argumentParts = new List<string>();
        var needSave      = false;

        if (_activeModalGroups is not null)
        {
            foreach (var g in _activeModalGroups)
                DynamicFormView.AppendArgumentParts(g, _dynamicControls, argumentParts, ref needSave);
        }

        DynamicFormView.AppendArgumentParts(_activeModalConfig, _dynamicControls, argumentParts, ref needSave);

        if (needSave)
            FigureConfigService.Save(_figureConfig);

        var exePath = _activeModalConfig.ExecutablePath;
        var args    = argumentParts;

        if (!string.IsNullOrWhiteSpace(exePath))
            ExternalProcessLauncher.Start(exePath, args);

        ExitModalMode();
        HidePopup();
    }
}

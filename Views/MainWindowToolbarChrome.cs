using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

/// <summary>
/// Applies toolbar-related view-model fields and split-button chrome from <see cref="AppConfig"/> and
/// <see cref="IMainWindowWorkflowCoordinator"/> so <see cref="MainWindow"/> stays thin.
/// </summary>
internal sealed class MainWindowToolbarChrome
{
    private readonly Window _resourceScope;
    private readonly Border _pressureSplitBorder;
    private readonly Border _inputBackendSplitBorder;
    private readonly MainViewModel _viewModel;
    private readonly Func<AppConfig> _getConfig;
    private readonly IMainWindowWorkflowCoordinator _workflowCoordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowToolbarChrome"/> class.
    /// </summary>
    public MainWindowToolbarChrome(
        Window resourceScope,
        Border pressureSplitBorder,
        Border inputBackendSplitBorder,
        MainViewModel viewModel,
        Func<AppConfig> getConfig,
        IMainWindowWorkflowCoordinator workflowCoordinator)
    {
        _resourceScope = resourceScope ?? throw new ArgumentNullException(nameof(resourceScope));
        _pressureSplitBorder = pressureSplitBorder ?? throw new ArgumentNullException(nameof(pressureSplitBorder));
        _inputBackendSplitBorder = inputBackendSplitBorder ?? throw new ArgumentNullException(nameof(inputBackendSplitBorder));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
    }

    /// <summary>
    /// Copies quick-setting flags from config into the VM, refreshes split borders, and applies coordinator toolbar state.
    /// </summary>
    public void SyncAll()
    {
        AppConfig config = _getConfig();
        _viewModel.SendPressureGlobally = config.SendPressureGlobally;
        _viewModel.AllowZoneSwitchWhilePressed = config.AllowZoneSwitchWhilePressed;
        SyncPressureSplitBorder();
        SyncInputBackendSplitBorder();

        MainWindowToolbarState toolbarState = _workflowCoordinator.BuildToolbarState(config);
        _viewModel.ToolbarPressureLabelText = toolbarState.ToolbarPressureLabelText;
        _viewModel.SelectedPressureCurve = toolbarState.SelectedPressureCurve;
        _viewModel.ToolbarInputBackendLabelText = toolbarState.ToolbarInputBackendLabelText;
        _viewModel.SelectedInputBackendMode = toolbarState.SelectedInputBackendMode;
        _viewModel.IsPressureCurveLinearChecked = toolbarState.IsPressureCurveLinearChecked;
        _viewModel.IsPressureCurveLogarithmicChecked = toolbarState.IsPressureCurveLogarithmicChecked;
        _viewModel.IsPressureCurveExponentialChecked = toolbarState.IsPressureCurveExponentialChecked;
        _viewModel.IsInputBackendAutoChecked = toolbarState.IsInputBackendAutoChecked;
        _viewModel.IsInputBackendWintabChecked = toolbarState.IsInputBackendWintabChecked;
        _viewModel.IsInputBackendMouseChecked = toolbarState.IsInputBackendMouseChecked;
    }

    /// <summary>
    /// Updates the pressure split border from the current view-model value (after user toggles global pressure).
    /// </summary>
    public void SyncPressureSplitBorder()
    {
        bool on = _viewModel.SendPressureGlobally;
        _pressureSplitBorder.Background = on ? ThemeBrush("AccentSurfaceBrush", Brushes.Transparent) : Brushes.Transparent;
        _pressureSplitBorder.BorderBrush = on ? ThemeBrush("AccentBrush", Brushes.Transparent) : Brushes.Transparent;
    }

    private void SyncInputBackendSplitBorder()
    {
        _inputBackendSplitBorder.Background = ThemeBrush("AccentSurfaceBrush", Brushes.Transparent);
        _inputBackendSplitBorder.BorderBrush = ThemeBrush("AccentBrush", Brushes.Transparent);
    }

    private Brush ThemeBrush(string key, Brush fallback)
        => _resourceScope.TryFindResource(key) as Brush
           ?? Application.Current?.TryFindResource(key) as Brush
           ?? fallback;
}

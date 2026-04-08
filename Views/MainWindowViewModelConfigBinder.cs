using System;
using System.ComponentModel;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

/// <summary>
/// Pushes a small set of view-model toggles into <see cref="AppConfig"/> and triggers follow-up UI refresh.
/// Keeps <see cref="MainWindow"/> free of <see cref="INotifyPropertyChanged"/> branching.
/// </summary>
internal sealed class MainWindowViewModelConfigBinder
{
    private readonly Func<bool> _isLoadingUi;
    private readonly Func<AppConfig> _getConfig;
    private readonly MainViewModel _viewModel;
    private readonly Action _syncPressureSplitBorder;
    private readonly Action _refreshRoutingStatusUi;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModelConfigBinder"/> class.
    /// </summary>
    public MainWindowViewModelConfigBinder(
        Func<bool> isLoadingUi,
        Func<AppConfig> getConfig,
        MainViewModel viewModel,
        Action syncPressureSplitBorder,
        Action refreshRoutingStatusUi)
    {
        _isLoadingUi = isLoadingUi ?? throw new ArgumentNullException(nameof(isLoadingUi));
        _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _syncPressureSplitBorder = syncPressureSplitBorder ?? throw new ArgumentNullException(nameof(syncPressureSplitBorder));
        _refreshRoutingStatusUi = refreshRoutingStatusUi ?? throw new ArgumentNullException(nameof(refreshRoutingStatusUi));
    }

    /// <summary>
    /// Handles <see cref="MainViewModel"/> property changes that mirror into configuration.
    /// </summary>
    public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingUi())
        {
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SendPressureGlobally))
        {
            _getConfig().SendPressureGlobally = _viewModel.SendPressureGlobally;
            _syncPressureSplitBorder();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.AllowZoneSwitchWhilePressed))
        {
            _getConfig().AllowZoneSwitchWhilePressed = _viewModel.AllowZoneSwitchWhilePressed;
            _refreshRoutingStatusUi();
        }
    }
}

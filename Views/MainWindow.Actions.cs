using System.Windows;
using TabletMidiPad.ViewModels;
using TabletMidiPad;

namespace TabletMidiPad.Views;

public partial class MainWindow
{
    void IMainWindowActions.ApplySettings() => ApplySettings();
    void IMainWindowActions.ResetDefaults() => MenuResetDefaults_Click(this, new RoutedEventArgs());
    void IMainWindowActions.OpenPreferences() => MenuPreferences_Click(this, new RoutedEventArgs());
    void IMainWindowActions.Exit() => Close();
    void IMainWindowActions.RefreshWacomStatus() => MenuRefreshWacom_Click(this, new RoutedEventArgs());
    void IMainWindowActions.ReconnectInput() => MenuReconnectPen_Click(this, new RoutedEventArgs());
    void IMainWindowActions.StartWacomService() => MenuStartWacomService_Click(this, new RoutedEventArgs());
    void IMainWindowActions.ShowAbout() => MenuAbout_Click(this, new RoutedEventArgs());
    void IMainWindowActions.AutoLayoutZones() => AutoLayoutButton_Click(this, new RoutedEventArgs());
    void IMainWindowActions.AddZone() => AddZoneButton_Click(this, new RoutedEventArgs());
    void IMainWindowActions.RemoveZone() => RemoveZoneButton_Click(this, new RoutedEventArgs());
}
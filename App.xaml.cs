using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using TabletMidiPad.Views;

namespace TabletMidiPad;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Register Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMidiService, MidiService>();
        services.AddSingleton<ITabletService, TabletService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<PreferencesViewModel>();

        // Register Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<PreferencesWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    public void ShowPreferences()
    {
        var prefsWindow = _serviceProvider.GetRequiredService<PreferencesWindow>();
        prefsWindow.Owner = MainWindow;
        prefsWindow.ShowDialog();
    }
}

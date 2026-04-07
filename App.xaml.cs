using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using TabletMidiPad.Views;

namespace TabletMidiPad;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        RegisterApplicationWindowIcon();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMidiService, MidiService>();
        services.AddSingleton<ITabletService, TabletService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<PreferencesViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<Func<int, PreferencesWindow>>(serviceProvider => expressKeyCount =>
        {
            var viewModel = serviceProvider.GetRequiredService<PreferencesViewModel>();
            return new PreferencesWindow(viewModel, expressKeyCount);
        });
    }

    private void RegisterApplicationWindowIcon()
    {
        if (TryLoadBestIcoFrame(out BitmapFrame? icoFrame))
        {
            Resources["AppIcon"] = icoFrame;
            return;
        }

        if (TryLoadPngIcon(out BitmapImage? png))
        {
            Resources["AppIcon"] = png;
        }
    }

    private static bool TryLoadBestIcoFrame(out BitmapFrame? frame)
    {
        frame = null;
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute));
            if (info?.Stream is null)
            {
                return false;
            }

            using (info.Stream)
            {
                var decoder = new IconBitmapDecoder(
                    info.Stream,
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool TryLoadPngIcon(out BitmapImage? image)
    {
        image = null;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri("pack://application:,,,/Assets/icon.png", UriKind.Absolute);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            image = bi;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}

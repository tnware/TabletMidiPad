using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        _serviceProvider = ConfigureServices();
        RegisterApplicationWindowIcon();
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMidiService, MidiService>();
        services.AddSingleton<IInputMappingService, InputMappingService>();
        services.AddSingleton<IWacomService, WacomService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Exposes <c>AppIcon</c> for <see cref="System.Windows.Window.Icon"/>. Uses multi-size <c>app.ico</c> when
    /// present so Windows can choose 16/32/48/256 px frames instead of scaling one large bitmap (which looks
    /// soft or distorted in the title bar).
    /// </summary>
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

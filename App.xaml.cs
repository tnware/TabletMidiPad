using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using TabletMidiPad.Composition;
using TabletMidiPad.Views;

namespace TabletMidiPad;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            (_, MainWindow mainWindow) = AppBootstrapper.CreateApplicationShell();
            RegisterApplicationWindowIcon();
            base.OnStartup(e);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "TabletMidiPad startup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>
    /// Exposes <c>AppIcon</c> for <see cref="Window.Icon"/>. Uses multi-size <c>app.ico</c> when
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
            StreamResourceInfo info = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute));
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

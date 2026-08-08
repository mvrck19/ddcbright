using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;

namespace DdcBright;

/// <summary>
/// Renders a Window straight to a PNG without ever calling Show() -- pure
/// in-process WPF rendering, no HWND/DWM/live-desktop needed, so it works
/// headlessly (locked session, CI, etc). Trades away the Acrylic/Mica blur
/// (a DWM-level effect applied to the HWND, outside WPF's own visual tree)
/// for a fast way to eyeball actual layout/text/colors while iterating.
/// </summary>
internal static class PreviewRenderer
{
    public static void Render(Window window, string outputPath, double width)
    {
        // Window.Measure/Arrange alone (no Show()) doesn't compute a real
        // layout size -- Window's sizing is tied to its HWND. Detaching and
        // rendering the plain FrameworkElement Content sidesteps that
        // entirely: ordinary WPF layout, no HWND involved.
        if (window.Content is not FrameworkElement content)
            throw new InvalidOperationException($"{window.GetType().Name} has no content to render.");

        // Windows like the flyout paint no background of their own and rely
        // on the live Acrylic/Mica backdrop (a DWM/HWND effect this renderer
        // can't produce) to sit behind their text. Wrap in the same solid
        // color WPF-UI uses as its backdrop fallback so dark-theme text
        // isn't rendered on literal transparency here -- preview-only, the
        // real window's XAML stays untouched so live Acrylic still shows.
        var background = window.TryFindResource("ApplicationBackgroundBrush") as Brush ?? Brushes.White;
        window.Content = null;
        var wrapper = new Border { Background = background, Child = content };

        wrapper.Measure(new Size(width, double.PositiveInfinity));
        wrapper.Arrange(new Rect(0, 0, width, wrapper.DesiredSize.Height));
        wrapper.UpdateLayout();

        var pixelWidth = Math.Max(1, (int)Math.Ceiling(wrapper.DesiredSize.Width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(wrapper.DesiredSize.Height));

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(wrapper);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}

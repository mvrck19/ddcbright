using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace DdcBright.Tests;

public class AmbientLightSensorTests
{
    [Theory]
    [InlineData(0, AmbientLightSensor.MinBrightness)] // never auto-dim below MinBrightness
    [InlineData(255, 100)]                            // brightest input maps to full brightness
    [InlineData(128, 55)]
    public void MapLumaToBrightness_ScalesIntoTheMinBrightness100Range(int luma, int expected)
    {
        Assert.Equal(expected, AmbientLightSensor.MapLumaToBrightness(luma));
    }

    [Fact]
    public void ComputeAverageLuma_AveragesAUniformColorImageToItsWeightedLuma()
    {
        const byte r = 200, g = 150, b = 50;
        const int width = 8, height = 8;

        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 255; // alpha
        }

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, width, height);

        // Every sampled pixel is identical, so the average equals the
        // per-pixel weighted luma exactly (matching ComputeAverageLuma's
        // own truncating cast, not a rounded value).
        var expectedLuma = (int)(0.299 * r + 0.587 * g + 0.114 * b);
        Assert.Equal(expectedLuma, AmbientLightSensor.ComputeAverageLuma(bitmap));
    }
}

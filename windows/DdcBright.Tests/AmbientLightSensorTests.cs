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

    // Learned calibration: corrections the user made while Ambient was on.

    [Fact]
    public void MapLumaToBrightness_AtALearnedPoint_ReturnsExactlyWhatTheUserChose()
    {
        Assert.Equal(50, AmbientLightSensor.MapLumaToBrightness(60, [new(60, 50)]));
    }

    [Fact]
    public void MapLumaToBrightness_BeyondTheLearnedPoints_FollowsTheDefaultSlopeShiftedThroughTheNearestPoint()
    {
        // The default curve rises 62 - 31 = 31 points between luma 60 and 150.
        Assert.Equal(81, AmbientLightSensor.MapLumaToBrightness(150, [new(60, 50)]));
    }

    [Fact]
    public void MapLumaToBrightness_BetweenTwoLearnedPoints_Interpolates()
    {
        Assert.Equal(25, AmbientLightSensor.MapLumaToBrightness(36, [new(12, 0), new(60, 50)]));
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(5, 0)] // darker than the learned 0% point: clamps at 0, not MinBrightness
    public void MapLumaToBrightness_CanLearnZeroPercent(int luma, int expected)
    {
        Assert.Equal(expected, AmbientLightSensor.MapLumaToBrightness(luma, [new(12, 0)]));
    }

    [Fact]
    public void MapLumaToBrightness_WithCalibration_NeverExceeds100()
    {
        Assert.Equal(100, AmbientLightSensor.MapLumaToBrightness(200, [new(60, 100)]));
    }

    [Fact]
    public void AddCalibrationPoint_ReplacesAPointAtNearlyTheSameLightLevel()
    {
        Assert.Equal<AmbientCalibrationPoint>([new(65, 50)], AmbientLightSensor.AddCalibrationPoint([new(60, 30)], 65, 50));
    }

    [Fact]
    public void AddCalibrationPoint_KeepsConsistentPointsSortedByLightLevel()
    {
        Assert.Equal<AmbientCalibrationPoint>([new(12, 0), new(60, 50)], AmbientLightSensor.AddCalibrationPoint([new(60, 50)], 12, 0));
    }

    [Fact]
    public void AddCalibrationPoint_DropsOlderPointsTheNewCorrectionContradicts()
    {
        // (60, 50) says a darker room wants a brighter screen than the new
        // (100, 40) does -- the newest correction wins.
        Assert.Equal<AmbientCalibrationPoint>([new(12, 20), new(100, 40)],
            AmbientLightSensor.AddCalibrationPoint([new(12, 20), new(60, 50)], 100, 40));
    }

    [Fact]
    public void Calibrate_BeforeAnyCameraReading_LearnsNothing()
    {
        var settings = new Settings();
        Assert.False(new AmbientLightSensor(settings).Calibrate(50));
        Assert.Empty(settings.AmbientCalibration);
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

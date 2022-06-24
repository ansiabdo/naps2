using NAPS2.Images.Gdi;
using NAPS2.ImportExport.Images;
using NAPS2.Scan;
using NAPS2.Sdk.Tests.Asserts;
using Xunit;

namespace NAPS2.Sdk.Tests.ImportExport;

public class ImportPostProcessorTests : ContextualTexts
{
    private readonly ImportPostProcessor _importPostProcessor;

    public ImportPostProcessorTests()
    {
        _importPostProcessor = new ImportPostProcessor(ImageContext);
    }

    [Fact]
    public void NoPostProcessing()
    {
        using var image = ScanningContext.CreateProcessedImage(new GdiImage(SharedData.color_image));
        using var image2 = _importPostProcessor.AddPostProcessingData(image, null, null, new BarcodeDetectionOptions(), false);

        Assert.Null(image2.PostProcessingData.Thumbnail);
        Assert.False(image2.PostProcessingData.BarcodeDetection.IsAttempted);
        Assert.False(IsDisposed(image2));
        image2.Dispose();
        Assert.False(IsDisposed(image));
    }

    [Fact]
    public void DisposesOriginalImageWithNoPostProcessing()
    {
        using var image = ScanningContext.CreateProcessedImage(new GdiImage(SharedData.color_image));
        using var image2 = _importPostProcessor.AddPostProcessingData(image, null, null, new BarcodeDetectionOptions(), true);

        Assert.False(IsDisposed(image2));
        image2.Dispose();
        Assert.True(IsDisposed(image));
    }

    [Fact]
    public void ThumbnailRendering()
    {
        using var image = ScanningContext.CreateProcessedImage(new GdiImage(SharedData.color_image));
        using var image2 = _importPostProcessor.AddPostProcessingData(image, null, 256, new BarcodeDetectionOptions(), false);

        var expected = new GdiImage(SharedData.color_image_thumb_256);
        var actual = image2.PostProcessingData.Thumbnail;

        Assert.NotNull(actual);
        ImageAsserts.Similar(expected, actual, ImageAsserts.GENERAL_RMSE_THRESHOLD);
    }

    [Fact]
    public void ThumbnailRenderingWithPrerenderedImageAndDisposingOriginal()
    {
        using var rendered = new GdiImage(SharedData.color_image);
        using var image = ScanningContext.CreateProcessedImage(rendered);
        using var image2 = _importPostProcessor.AddPostProcessingData(image, rendered, 256, new BarcodeDetectionOptions(), true);

        var expected = new GdiImage(SharedData.color_image_thumb_256);
        var actual = image2.PostProcessingData.Thumbnail;

        Assert.NotNull(actual);
        ImageAsserts.Similar(expected, actual, ImageAsserts.GENERAL_RMSE_THRESHOLD);
        Assert.False(IsDisposed(rendered));
        Assert.False(IsDisposed(image2));
        image2.Dispose();
        Assert.True(IsDisposed(image));
    }

    [Fact]
    public void BarcodeDetection()
    {
        using var image = ScanningContext.CreateProcessedImage(new GdiImage(SharedData.patcht));
        var barcodeOptions = new BarcodeDetectionOptions { DetectBarcodes = true };
        using var image2 = _importPostProcessor.AddPostProcessingData(image, null, null, barcodeOptions, false);

        Assert.True(image2.PostProcessingData.BarcodeDetection.IsPatchT);
    }

    [Fact]
    public void BarcodeDetectionWithPrerenderedImage()
    {
        using var rendered = new GdiImage(SharedData.patcht);
        using var image = ScanningContext.CreateProcessedImage(rendered);
        var barcodeOptions = new BarcodeDetectionOptions { DetectBarcodes = true };
        using var image2 = _importPostProcessor.AddPostProcessingData(image, rendered, null, barcodeOptions, false);

        Assert.True(image2.PostProcessingData.BarcodeDetection.IsPatchT);
    }
}
using System.Collections.Immutable;
using System.Threading;
using Moq;
using NAPS2.Ocr;
using Xunit;

namespace NAPS2.Sdk.Tests.Ocr;

public class OcrRequestQueueTests : ContextualTexts
{
    private readonly OcrRequestQueue _ocrRequestQueue;
    private readonly Mock<IOcrEngine> _mockEngine;
    private readonly Mock<OperationProgress> _mockOperationProgress;
    private readonly ProcessedImage _image;
    private readonly string _tempPath;
    private readonly OcrParams _ocrParams;
    private readonly OcrResult _expectedResult;

    public OcrRequestQueueTests()
    {
        _mockEngine = new Mock<IOcrEngine>(MockBehavior.Strict);
        _mockOperationProgress = new Mock<OperationProgress>();
        _ocrRequestQueue = new OcrRequestQueue(_mockOperationProgress.Object);

        _image = CreateScannedImage();
        _tempPath = CreateTempFile();
        _ocrParams = CreateOcrParams();
        _expectedResult = CreateOcrResult();
    }

    [Fact]
    public async Task Enqueue()
    {
        _mockEngine.Setup(x => x.ProcessImage(_tempPath, _ocrParams, It.IsAny<CancellationToken>()))
            .Returns(_expectedResult);

        var ocrResult = await DoEnqueueForeground(_image, _tempPath, _ocrParams);

        Assert.Equal(_expectedResult, ocrResult);
        Assert.False(File.Exists(_tempPath));
    }

    [Fact]
    public async Task EnqueueTwiceReturnsCached()
    {
        var tempPath1 = CreateTempFile();
        var tempPath2 = CreateTempFile();
        _mockEngine.Setup(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()))
            .Returns(_expectedResult);

        await DoEnqueueForeground(_image, tempPath1, _ocrParams);
        Assert.False(File.Exists(tempPath1));

        var ocrResult2Task = DoEnqueueForeground(_image, tempPath2, _ocrParams);
        // Verify synchronous return for cache
        Assert.True(ocrResult2Task.IsCompleted);

        var ocrResult2 = await ocrResult2Task;
        Assert.Equal(_expectedResult, ocrResult2);
        Assert.False(File.Exists(tempPath2));

        // Verify only a single engine call
        _mockEngine.Verify(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnqueueSimultaneous()
    {
        var tempPath1 = CreateTempFile();
        var tempPath2 = CreateTempFile();
        _mockEngine.Setup(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()))
            .Returns(_expectedResult);

        var ocrResult1Task = DoEnqueueForeground(_image, tempPath1, _ocrParams);

        var ocrResult2Task = DoEnqueueForeground(_image, tempPath2, _ocrParams);

        var ocrResult1 = await ocrResult1Task;
        var ocrResult2 = await ocrResult2Task;
        Assert.Equal(_expectedResult, ocrResult1);
        Assert.Equal(_expectedResult, ocrResult2);
        Assert.False(File.Exists(tempPath1));
        Assert.False(File.Exists(tempPath2));

        // Verify only a single engine call
        _mockEngine.Verify(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnqueueWithDifferentImagesReturnsDifferentResult()
    {
        var image1 = CreateScannedImage();
        var tempPath1 = CreateTempFile();
        var expectedResult1 = CreateOcrResult();
        _mockEngine.Setup(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()))
            .Returns(expectedResult1);

        var image2 = CreateScannedImage();
        var tempPath2 = CreateTempFile();
        var expectedResult2 = CreateOcrResult();
        _mockEngine.Setup(x => x.ProcessImage(tempPath2, _ocrParams, It.IsAny<CancellationToken>()))
            .Returns(expectedResult2);

        var ocrResult1 = await DoEnqueueForeground(image1, tempPath1, _ocrParams);
        Assert.Equal(expectedResult1, ocrResult1);
        Assert.False(File.Exists(tempPath1));

        var ocrResult2 = await DoEnqueueForeground(image2, tempPath2, _ocrParams);
        Assert.Equal(expectedResult2, ocrResult2);
        Assert.False(File.Exists(tempPath2));

        // Verify two engine calls
        _mockEngine.Verify(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.Verify(x => x.ProcessImage(tempPath2, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnqueueWithDifferentParamsReturnsDifferentResult()
    {
        var ocrParams1 = new OcrParams("eng", OcrMode.Fast, 10);
        var ocrParams2 = new OcrParams("fra", OcrMode.Fast, 10);
        var ocrParams3 = new OcrParams("eng", OcrMode.Best, 10);
        var ocrParams4 = new OcrParams("eng", OcrMode.Fast, 0);
        var expectedResult1 = CreateOcrResult();
        var expectedResult2 = CreateOcrResult();
        var expectedResult3 = CreateOcrResult();
        var expectedResult4 = CreateOcrResult();
        _mockEngine.Setup(x => x.ProcessImage(It.IsAny<string>(), ocrParams1, It.IsAny<CancellationToken>()))
            .Returns(expectedResult1);
        _mockEngine.Setup(x => x.ProcessImage(It.IsAny<string>(), ocrParams2, It.IsAny<CancellationToken>()))
            .Returns(expectedResult2);
        _mockEngine.Setup(x => x.ProcessImage(It.IsAny<string>(), ocrParams3, It.IsAny<CancellationToken>()))
            .Returns(expectedResult3);
        _mockEngine.Setup(x => x.ProcessImage(It.IsAny<string>(), ocrParams4, It.IsAny<CancellationToken>()))
            .Returns(expectedResult4);

        var ocrResult1 = await DoEnqueueForeground(_image, CreateTempFile(), ocrParams1);
        Assert.Equal(expectedResult1, ocrResult1);
        var ocrResult2 = await DoEnqueueForeground(_image, CreateTempFile(), ocrParams2);
        Assert.Equal(expectedResult2, ocrResult2);
        var ocrResult3 = await DoEnqueueForeground(_image, CreateTempFile(), ocrParams3);
        Assert.Equal(expectedResult3, ocrResult3);
        var ocrResult4 = await DoEnqueueForeground(_image, CreateTempFile(), ocrParams4);
        Assert.Equal(expectedResult4, ocrResult4);

        // Verify distinct engine calls
        _mockEngine.Verify(x => x.ProcessImage(It.IsAny<string>(), ocrParams1, It.IsAny<CancellationToken>()));
        _mockEngine.Verify(x => x.ProcessImage(It.IsAny<string>(), ocrParams2, It.IsAny<CancellationToken>()));
        _mockEngine.Verify(x => x.ProcessImage(It.IsAny<string>(), ocrParams3, It.IsAny<CancellationToken>()));
        _mockEngine.Verify(x => x.ProcessImage(It.IsAny<string>(), ocrParams4, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnqueueWithEngineError()
    {
        _mockEngine.Setup(x => x.ProcessImage(_tempPath, _ocrParams, It.IsAny<CancellationToken>()))
            .Throws<Exception>();

        var ocrResult = await DoEnqueueForeground(_image, _tempPath, _ocrParams);

        Assert.Null(ocrResult);
        _mockEngine.Verify(x => x.ProcessImage(_tempPath, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnqueueTwiceWithTransientEngineError()
    {
        var tempPath1 = CreateTempFile();
        var tempPath2 = CreateTempFile();
        _mockEngine.Setup(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()))
            .Throws<Exception>();
        _mockEngine.Setup(x => x.ProcessImage(tempPath2, _ocrParams, It.IsAny<CancellationToken>()))
            .Returns(_expectedResult);

        var ocrResult1 = await DoEnqueueForeground(_image, tempPath1, _ocrParams);
        var ocrResult2 = await DoEnqueueForeground(_image, tempPath2, _ocrParams);

        Assert.Null(ocrResult1);
        Assert.Equal(_expectedResult, ocrResult2);
        _mockEngine.Verify(x => x.ProcessImage(tempPath1, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.Verify(x => x.ProcessImage(tempPath2, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImmediateCancel()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var ocrResult = await DoEnqueueForeground(_image, _tempPath, _ocrParams, cts.Token);

        Assert.Null(ocrResult);
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancelAfterCall()
    {
        var cts = new CancellationTokenSource();

        var ocrResultTask = DoEnqueueForeground(_image, _tempPath, _ocrParams, cts.Token);
        cts.Cancel();
        var ocrResult = await ocrResultTask;

        Assert.Null(ocrResult);
        _mockEngine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancelDuringEngine()
    {
        var cancelledAtEngineStart = false;
        var cancelledAtEngineEnd = false;
        var cts = new CancellationTokenSource();
        _mockEngine.Setup(x => x.ProcessImage(_tempPath, _ocrParams, It.IsAny<CancellationToken>())).Returns(
            new InvocationFunc(invocation =>
            {
                var cancelToken = (CancellationToken) invocation.Arguments[2];
                cancelledAtEngineStart = cancelToken.IsCancellationRequested;
                Thread.Sleep(100);
                cancelledAtEngineEnd = cancelToken.IsCancellationRequested;
                return null;
            }));

        cts.CancelAfter(50);
        var ocrResult = await DoEnqueueForeground(_image, _tempPath, _ocrParams, cts.Token);

        Assert.Null(ocrResult);
        Assert.False(cancelledAtEngineStart);
        Assert.True(cancelledAtEngineEnd);
        _mockEngine.Verify(x => x.ProcessImage(_tempPath, _ocrParams, It.IsAny<CancellationToken>()));
        _mockEngine.VerifyNoOtherCalls();
    }

    private string CreateTempFile()
    {
        var path = Path.Combine(FolderPath, $"tempocr{Guid.NewGuid()}.jpg");
        File.WriteAllText(path, @"blah");
        return path;
    }

    private static OcrResult CreateOcrResult()
    {
        var uniqueElement = new OcrResultElement(Guid.NewGuid().ToString(), (0, 0, 1, 1));
        return new OcrResult((0, 0, 1, 1), new List<OcrResultElement> { uniqueElement }, false);
    }

    private static OcrParams CreateOcrParams()
    {
        return new OcrParams("eng", OcrMode.Fast, 10);
    }

    private Task<OcrResult?> DoEnqueueForeground(ProcessedImage image, string tempPath, OcrParams ocrParams,
        CancellationToken cancellationToken = default)
    {
        return _ocrRequestQueue.Enqueue(
            _mockEngine.Object,
            image,
            tempPath,
            ocrParams,
            OcrPriority.Foreground,
            cancellationToken);
    }

    // TODO: Tests to add:
    // - Unsupported language code
    // - Many parallel tasks (more than worker threads - also # of worker threads should be configurable by the test)
    // - Can I break things by overloading task parallelization? I honestly don't remember why this is supposed to work...
    // - Priority (background vs foreground)
    // - Maybe we can parameterize some tests for background/foreground? Or maybe not necessary, priority tests are
    // probably enough.
    // - Cancellation
}
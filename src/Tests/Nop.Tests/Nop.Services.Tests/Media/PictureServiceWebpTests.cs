using AwesomeAssertions;
using Nop.Core;
using Nop.Services.Media;
using NUnit.Framework;
using SkiaSharp;

namespace Nop.Tests.Nop.Services.Tests.Media;

[TestFixture]
public class PictureServiceWebpTests : ServiceTest
{
    private IPictureService _pictureService;

    [OneTimeSetUp]
    public void SetUp()
    {
        _pictureService = GetService<IPictureService>();
    }

    [Test]
    public async Task ValidateAndConvertPictureAsync_ConvertsPngToWebp()
    {
        // Create a simple test PNG
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);

        using var image = SKImage.FromBitmap(bitmap);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        var pngBytes = pngData.ToArray();

        var (convertedBytes, mimeType) = await _pictureService.ValidateAndConvertPictureAsync(pngBytes, "image/png", "test.png");

        mimeType.Should().Be(MimeTypes.ImageWebp);
        convertedBytes.Should().NotBeNull();
        convertedBytes.Length.Should().BeGreaterThan(0);

        // Verify the converted bytes are valid WebP
        using var stream = new MemoryStream(convertedBytes);
        using var codec = SKCodec.Create(stream);
        codec.Should().NotBeNull();
        codec.EncodedFormat.Should().Be(SKEncodedImageFormat.Webp);
    }

    [Test]
    public async Task ValidateAndConvertPictureAsync_PreservesSvgWithoutConversion()
    {
        const string svgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\"><rect width=\"50\" height=\"50\" fill=\"red\"/></svg>";
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(svgContent);

        var (resultBytes, mimeType) = await _pictureService.ValidateAndConvertPictureAsync(svgBytes, "image/svg+xml", "test.svg");

        mimeType.Should().Be("image/svg+xml");
        resultBytes.Should().Equal(svgBytes);
    }
}

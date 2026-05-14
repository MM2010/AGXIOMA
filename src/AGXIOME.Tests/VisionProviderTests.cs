using AGXIOME.Core.Utils;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests;

public class VisionProviderTests
{
    [Fact]
    public void ImageUtil_DetectMimeType_Jpg()
    {
        Assert.Equal("image/jpeg", ImageUtil.DetectMimeType("photo.jpg"));
        Assert.Equal("image/jpeg", ImageUtil.DetectMimeType("photo.jpeg"));
    }

    [Fact]
    public void ImageUtil_DetectMimeType_Png()
    {
        Assert.Equal("image/png", ImageUtil.DetectMimeType("image.png"));
    }

    [Fact]
    public void ImageUtil_DetectMimeType_Webp()
    {
        Assert.Equal("image/webp", ImageUtil.DetectMimeType("image.webp"));
    }

    [Fact]
    public void ImageUtil_ValidateImageUrl_Valid()
    {
        Assert.True(ImageUtil.ValidateImageUrl("https://example.com/photo.png"));
        Assert.True(ImageUtil.ValidateImageUrl("http://example.com/photo.jpg"));
    }

    [Fact]
    public void ImageUtil_ValidateImageUrl_Invalid()
    {
        Assert.False(ImageUtil.ValidateImageUrl(""));
        Assert.False(ImageUtil.ValidateImageUrl("not-a-url"));
        Assert.False(ImageUtil.ValidateImageUrl("https://example.com/file.pdf"));
    }

    [Fact]
    public void ImageUtil_BuildOpenAIImageContent_Url()
    {
        var result = ImageUtil.BuildOpenAIImageContent("https://example.com/photo.png");
        // Verify it doesn't throw
        Assert.NotNull(result);
    }

    [Fact]
    public void ImageUtil_BuildAnthropicImageContent_Url()
    {
        var result = ImageUtil.BuildAnthropicImageContent("https://example.com/photo.png");
        Assert.NotNull(result);
    }

    [Fact]
    public void ImageUtil_BuildGeminiImageContent_Url()
    {
        var result = ImageUtil.BuildGeminiImageContent("https://example.com/photo.png");
        Assert.NotNull(result);
    }

    [Fact]
    public void MediaContent_Create_StoresData()
    {
        var media = new MediaContent(
            MediaType.Image,
            "base64data",
            "image/png",
            "A test image",
            MediaSource.Base64
        );

        Assert.Equal(MediaType.Image, media.MediaType);
        Assert.Equal("base64data", media.Data);
        Assert.Equal("image/png", media.MimeType);
        Assert.Equal("A test image", media.AltText);
        Assert.Equal(MediaSource.Base64, media.Source);
    }

    [Fact]
    public void AgentMessage_WithMedia_CreatesCorrectly()
    {
        var media = new List<MediaContent>
        {
            new MediaContent(MediaType.Image, "data", "image/png")
        };

        var msg = new AgentMessage("user", "Describe this image", Media: media);

        Assert.Equal("user", msg.Role);
        Assert.Equal("Describe this image", msg.Content);
        Assert.NotNull(msg.Media);
        Assert.Single(msg.Media);
    }
}

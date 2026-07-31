using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Tests.Services;

public class ImageDeliveryUrlsTests
{
    [Fact]
    public void CloudinaryUrlsReceivePurposeSpecificTransformations()
    {
        const string source =
            "https://res.cloudinary.com/goisland/image/upload/v123/goisland/experiences/8/photo.jpg";

        Assert.Contains(
            "/image/upload/f_auto,q_auto,c_fill,w_720,h_480/",
            ImageDeliveryUrls.ForCard(source));
        Assert.Contains(
            "/image/upload/f_auto,q_auto,c_limit,w_1600,h_1200/",
            ImageDeliveryUrls.ForDetail(source));
        Assert.Contains(
            "/image/upload/f_auto,q_auto,c_fill,w_240,h_180/",
            ImageDeliveryUrls.ForThumbnail(source));
    }

    [Fact]
    public void NonCloudinaryUrlIsPreservedForLegacyMigration()
    {
        const string source = "/uploads/experiences/8/photo.jpg";

        Assert.Equal(source, ImageDeliveryUrls.ForCard(source));
        Assert.Equal(source, ImageDeliveryUrls.ForDetail(source));
        Assert.Equal(source, ImageDeliveryUrls.ForThumbnail(source));
    }
}

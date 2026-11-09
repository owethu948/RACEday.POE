using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Image uploads: the API validates the file, stores it via IBlobStorageService and returns the URL.</summary>
public class FileTests : IClassFixture<RaceDayApiFactory>
{
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3, 4 };
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };

    private readonly RaceDayApiFactory _factory;

    public FileTests(RaceDayApiFactory factory) => _factory = factory;

    private FakeBlobStorageService Storage => (FakeBlobStorageService)_factory.Services.GetRequiredService<IBlobStorageService>();

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName = "image.png", string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    // ----------------------------------------------------------------- banner
    [Fact]
    public async Task UploadBanner_Organiser_StoresImageAndReturnsAzureUrl()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsync("/api/files/banner", FileContent(PngBytes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.Contains(".blob.core.windows.net/", body!.Url);
        Assert.Contains("/banners/", body.Url);
        Assert.EndsWith(".png", body.Url);
        Assert.Contains(Storage.Uploads, u => u.Folder == "banners" && u.ContentType == "image/png");
    }

    [Fact]
    public async Task UploadBanner_TrustsTheBytesNotTheClaimedContentType()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        // Real JPEG bytes, but the client labels them as a PNG file.
        var response = await organiser.Client.PostAsync("/api/files/banner", FileContent(JpegBytes, "pretend.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.EndsWith(".jpg", body!.Url);
    }

    [Fact]
    public async Task UploadBanner_Participant_Returns403()
    {
        var participant = await TestData.SignInParticipantAsync(_factory);

        var response = await participant.Client.PostAsync("/api/files/banner", FileContent(PngBytes));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/files/banner", FileContent(PngBytes));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NotAnImage_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");

        var response = await organiser.Client.PostAsync("/api/files/banner", FileContent(notAnImage, "evil.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NoFile_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsync("/api/files/banner", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_TooLarge_Returns413()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var big = new byte[(5 * 1024 * 1024) + 1];
        PngBytes.CopyTo(big, 0);

        var response = await organiser.Client.PostAsync("/api/files/banner", FileContent(big));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    // -------------------------------------------------------- profile picture
    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task UploadProfilePicture_AnyLoggedInUser_Succeeds(string role)
    {
        var user = await TestData.SignInAsync(_factory, role);

        var response = await user.Client.PostAsync("/api/files/profile-picture", FileContent(PngBytes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.Contains("/profiles/", body!.Url);
    }

    [Fact]
    public async Task UploadProfilePicture_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/files/profile-picture", FileContent(PngBytes));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------- URLs saved on records
    [Fact]
    public async Task Event_BannerUrl_IsSavedReturnedAndCanBeCleared()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var request = TestData.NewEvent();
        request.BannerUrl = "https://fakeaccount.blob.core.windows.net/raceday-images/banners/abc.png";

        var created = await TestData.CreateEventAsync(organiser.Client, request);
        Assert.Equal(request.BannerUrl, created.BannerUrl);

        var detail = await _factory.CreateClient().GetFromJsonAsync<EventDetailResponse>($"/api/events/{created.EventId}");
        Assert.Equal(request.BannerUrl, detail!.BannerUrl);

        request.BannerUrl = null;
        var update = await organiser.Client.PutAsJsonAsync($"/api/events/{created.EventId}", request);
        var updated = await update.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Null(updated!.BannerUrl);
    }

    [Fact]
    public async Task Event_BannerUrl_MustBeAUrl()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var request = TestData.NewEvent();
        request.BannerUrl = "javascript:alert(1)";

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_PictureUrl_IsSavedAndReturned()
    {
        var participant = await TestData.SignInParticipantAsync(_factory);
        var url = "https://fakeaccount.blob.core.windows.net/raceday-images/profiles/me.jpg";

        var update = await participant.Client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest
        {
            FullName = "Pic Tester",
            ProfilePictureUrl = url
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var me = await participant.Client.GetFromJsonAsync<ProfileResponse>("/api/users/me");
        Assert.Equal(url, me!.ProfilePictureUrl);
    }

    // ------------------------------------------------------- pure unit tests
    [Fact]
    public async Task AzureBlobStorageService_WithoutConnectionString_Throws()
    {
        var service = new AzureBlobStorageService(
            Microsoft.Extensions.Options.Options.Create(new AzureBlobStorageOptions { ConnectionString = "" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureBlobStorageService>.Instance);

        await Assert.ThrowsAsync<BlobStorageNotConfiguredException>(() =>
            service.UploadImageAsync(new MemoryStream(PngBytes), "image/png", "banners", ".png"));
    }

    [Fact]
    public void ImageSignature_RecognisesSupportedFormatsOnly()
    {
        Assert.Equal(".png", ImageSignature.Detect(PngBytes)!.Value.Extension);
        Assert.Equal(".jpg", ImageSignature.Detect(JpegBytes)!.Value.Extension);
        Assert.Equal(".gif", ImageSignature.Detect("GIF89a......"u8)!.Value.Extension);
        Assert.Equal(".webp", ImageSignature.Detect("RIFF\0\0\0\0WEBP"u8)!.Value.Extension);
        Assert.Null(ImageSignature.Detect("%PDF-1.7......"u8));
        Assert.Null(ImageSignature.Detect(ReadOnlySpan<byte>.Empty));
    }
}

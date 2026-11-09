using Azure;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Image uploads. The web front end never talks to Azure: it sends the file here, this API validates it,
/// stores it in Azure Blob Storage and returns the public URL to save on the event or profile.
/// </summary>
[Route("api/files")]
public class FilesController : ApiControllerBase
{
    /// <summary>Largest accepted image (5 MB).</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly IBlobStorageService _storage;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IBlobStorageService storage, ILogger<FilesController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Upload an event banner image.</summary>
    /// <remarks>Organiser only. JPEG, PNG, GIF or WebP up to 5 MB, sent as multipart/form-data in a field called <c>file</c>.</remarks>
    /// <response code="200">Stored; the body holds the public blob URL to put in <c>EventRequest.BannerUrl</c>.</response>
    /// <response code="400">No file, an empty file, or not a supported image.</response>
    /// <response code="413">The file is larger than 5 MB.</response>
    /// <response code="503">Image storage is not configured or Azure could not be reached.</response>
    [HttpPost("banner")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> UploadBanner(IFormFile? file, CancellationToken cancellationToken)
        => Upload(file, "banners", cancellationToken);

    /// <summary>Upload a profile picture.</summary>
    /// <remarks>Any logged-in user. Same rules as the banner upload. Save the returned URL with <c>PUT /api/users/me</c>.</remarks>
    /// <response code="200">Stored; the body holds the public blob URL to put in <c>UpdateProfileRequest.ProfilePictureUrl</c>.</response>
    /// <response code="400">No file, an empty file, or not a supported image.</response>
    /// <response code="413">The file is larger than 5 MB.</response>
    /// <response code="503">Image storage is not configured or Azure could not be reached.</response>
    [HttpPost("profile-picture")]
    [SessionAuthorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> UploadProfilePicture(IFormFile? file, CancellationToken cancellationToken)
        => Upload(file, "profiles", cancellationToken);

    private async Task<IActionResult> Upload(IFormFile? file, string folder, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Fail(400, "Choose an image to upload (multipart field name: file).");
        if (file.Length > MaxFileBytes)
            return Fail(413, "The image is too large. The maximum size is 5 MB.");

        // Decide the real type from the bytes, not from what the client claims.
        var header = new byte[12];
        int read;
        await using (var probe = file.OpenReadStream())
        {
            read = await probe.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        }

        var detected = ImageSignature.Detect(header.AsSpan(0, read));
        if (detected is null)
            return Fail(400, "Only JPEG, PNG, GIF or WebP images can be uploaded.");

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await _storage.UploadImageAsync(stream, detected.Value.ContentType, folder, detected.Value.Extension, cancellationToken);
            return Ok(new UploadResponse { Url = url });
        }
        catch (BlobStorageNotConfiguredException ex)
        {
            return Fail(503, ex.Message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob Storage rejected the upload");
            return Fail(503, "Image storage is temporarily unavailable. Please try again later.");
        }
    }
}

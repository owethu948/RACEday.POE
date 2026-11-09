namespace RaceDay.Api.Dtos;

/// <summary>Result of a successful image upload.</summary>
public class UploadResponse
{
    /// <summary>Public URL of the stored image (https://&lt;account&gt;.blob.core.windows.net/...).</summary>
    public string Url { get; set; } = string.Empty;
}

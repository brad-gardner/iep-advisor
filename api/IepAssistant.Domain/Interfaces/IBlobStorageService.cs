namespace IepAssistant.Domain.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null);
}

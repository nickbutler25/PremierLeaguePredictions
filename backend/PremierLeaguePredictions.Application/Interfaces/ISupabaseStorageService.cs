namespace PremierLeaguePredictions.Application.Interfaces;

/// <summary>
/// Uploads and deletes user avatar images in Supabase Storage.
/// </summary>
public interface ISupabaseStorageService
{
    /// <summary>
    /// Uploads an avatar image and returns its public URL.
    /// </summary>
    Task<string> UploadAvatarAsync(
        Guid userId,
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort delete of a previously uploaded avatar, given its public URL.
    /// No-op if the URL does not belong to our storage bucket (e.g. a Google-hosted photo).
    /// </summary>
    Task DeleteByPublicUrlAsync(string publicUrl, CancellationToken cancellationToken = default);
}

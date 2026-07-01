namespace ecomm.api.Features.Media;

/// <summary>Bound from the <c>Media</c> config section.</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Filesystem directory where uploads are written. Relative paths resolve against the
    /// content root (dev); prod sets an absolute persistent path e.g. <c>/var/www/ecomm/uploads</c>.</summary>
    public string UploadPath { get; set; } = "uploads";

    /// <summary>Prefix for returned URLs. Empty => root-relative (<c>/uploads/…</c>, same-origin in prod).
    /// Dev sets the API origin (<c>http://localhost:5080</c>) so cross-origin <c>ng serve</c> can load them.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Request path the files are served under (must match the Nginx <c>location</c> in prod).</summary>
    public string RequestPath { get; set; } = "/uploads";

    /// <summary>Max upload size in bytes (default 5 MB).</summary>
    public long MaxBytes { get; set; } = 5 * 1024 * 1024;
}

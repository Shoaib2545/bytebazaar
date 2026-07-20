namespace ByteBazaar.Domain.Entities;

/// <summary>
/// Admin-managed URL redirect. <see cref="FromPath"/> is a normalized, leading-slash storefront
/// path (query string excluded); the storefront middleware looks it up and issues a 301 when
/// <see cref="IsPermanent"/> is set, otherwise a 302.
/// </summary>
public class Redirect
{
    public Guid Id { get; set; }
    public string FromPath { get; set; } = string.Empty;
    public string ToPath { get; set; } = string.Empty;
    public bool IsPermanent { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

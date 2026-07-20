namespace ByteBazaar.Application.DTOs;

public class AdminRedirectDto
{
    public Guid Id { get; set; }
    public string FromPath { get; set; } = string.Empty;
    public string ToPath { get; set; } = string.Empty;
    public bool IsPermanent { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RedirectUpsertRequest
{
    public string FromPath { get; set; } = string.Empty;
    public string ToPath { get; set; } = string.Empty;
    public bool IsPermanent { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

/// <summary>What the storefront middleware needs to issue the redirect.</summary>
public class RedirectLookupDto
{
    public string ToPath { get; set; } = string.Empty;
    public bool IsPermanent { get; set; }
    public int StatusCode { get; set; }
}

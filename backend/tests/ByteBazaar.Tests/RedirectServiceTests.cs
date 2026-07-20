using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using ByteBazaar.Application.Validators;
using Xunit;

namespace ByteBazaar.Tests;

public class RedirectServiceTests
{
    private static RedirectUpsertRequest Request(string from, string to, bool permanent = true, bool active = true)
        => new() { FromPath = from, ToPath = to, IsPermanent = permanent, IsActive = active };

    [Theory]
    [InlineData("/old-page", "/old-page")]
    [InlineData("/Old-Page/", "/old-page")]
    [InlineData("old-page", "/old-page")]
    [InlineData("/old-page?utm=x", "/old-page")]
    [InlineData("/old-page#frag", "/old-page")]
    [InlineData("  /Old-Page  ", "/old-page")]
    [InlineData("/", "/")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    public void NormalizePath_IsCaseTrailingSlashAndQueryInsensitive(string? input, string expected)
        => Assert.Equal(expected, RedirectService.NormalizePath(input));

    [Fact]
    public async Task Create_NormalizesFromPath()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);

        var created = await service.CreateAsync(Request("/Old-Category/", "/category/laptops"));

        Assert.NotNull(created);
        Assert.Equal("/old-category", created!.FromPath);
        Assert.Equal("/category/laptops", created.ToPath);
    }

    [Fact]
    public async Task Create_RejectsDuplicateFromPath_AfterNormalization()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        await service.CreateAsync(Request("/old-page", "/new-page"));

        var duplicate = await service.CreateAsync(Request("/OLD-PAGE/", "/other"));

        Assert.Null(duplicate);
    }

    [Fact]
    public async Task Lookup_ReturnsPermanentRedirectWith301()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        await service.CreateAsync(Request("/old-page", "/new-page"));

        var result = await service.LookupAsync("/Old-Page/");

        Assert.NotNull(result);
        Assert.Equal("/new-page", result!.ToPath);
        Assert.True(result.IsPermanent);
        Assert.Equal(301, result.StatusCode);
    }

    [Fact]
    public async Task Lookup_ReturnsTemporaryRedirectWith302()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        await service.CreateAsync(Request("/promo", "/category/laptops", permanent: false));

        var result = await service.LookupAsync("/promo");

        Assert.NotNull(result);
        Assert.False(result!.IsPermanent);
        Assert.Equal(302, result.StatusCode);
    }

    [Fact]
    public async Task Lookup_IgnoresInactiveRules()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        await service.CreateAsync(Request("/disabled", "/new-page", active: false));

        Assert.Null(await service.LookupAsync("/disabled"));
        Assert.Empty(await service.GetActiveAsync());
        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task Lookup_ReturnsNull_WhenNoRuleMatches()
    {
        await using var db = TestDbFactory.Create();
        Assert.Null(await new RedirectService(db).LookupAsync("/nothing-here"));
    }

    [Fact]
    public async Task Update_RejectsCollisionWithAnotherRule()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        var first = await service.CreateAsync(Request("/a", "/x"));
        var second = await service.CreateAsync(Request("/b", "/y"));

        var (found, dto) = await service.UpdateAsync(second!.Id, Request("/A", "/z"));

        Assert.True(found);
        Assert.Null(dto);
        Assert.Equal("/a", (await service.GetAsync(first!.Id))!.FromPath);
    }

    [Fact]
    public async Task Update_AllowsKeepingItsOwnFromPath()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        var created = await service.CreateAsync(Request("/a", "/x"));

        var (found, dto) = await service.UpdateAsync(created!.Id, Request("/a", "/updated"));

        Assert.True(found);
        Assert.Equal("/updated", dto!.ToPath);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_ForUnknownId()
    {
        await using var db = TestDbFactory.Create();
        var (found, dto) = await new RedirectService(db).UpdateAsync(Guid.NewGuid(), Request("/a", "/b"));
        Assert.False(found);
        Assert.Null(dto);
    }

    [Fact]
    public async Task Delete_RemovesTheRule()
    {
        await using var db = TestDbFactory.Create();
        var service = new RedirectService(db);
        var created = await service.CreateAsync(Request("/a", "/x"));

        Assert.True(await service.DeleteAsync(created!.Id));
        Assert.False(await service.DeleteAsync(created.Id));
        Assert.Empty(await service.GetAllAsync());
    }
}

public class RedirectValidatorTests
{
    private readonly RedirectUpsertRequestValidator _validator = new();

    [Fact]
    public void RejectsRelativeFromPath()
    {
        var result = _validator.Validate(new RedirectUpsertRequest { FromPath = "old-page", ToPath = "/new" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsToPathThatIsNeitherPathNorAbsoluteUrl()
    {
        var result = _validator.Validate(new RedirectUpsertRequest { FromPath = "/old", ToPath = "new-page" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsSelfRedirect()
    {
        var result = _validator.Validate(new RedirectUpsertRequest { FromPath = "/old-page", ToPath = "/Old-Page/" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void AcceptsAbsoluteExternalUrl()
    {
        var result = _validator.Validate(new RedirectUpsertRequest { FromPath = "/old", ToPath = "https://example.com/new" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AcceptsValidInternalRedirect()
    {
        var result = _validator.Validate(new RedirectUpsertRequest { FromPath = "/old", ToPath = "/category/laptops" });
        Assert.True(result.IsValid);
    }
}

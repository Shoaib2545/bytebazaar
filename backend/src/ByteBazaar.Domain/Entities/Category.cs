namespace ByteBazaar.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();
    public List<AttributeDefinition> Attributes { get; set; } = new();
    public List<Product> Products { get; set; } = new();
}

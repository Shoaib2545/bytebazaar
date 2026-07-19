namespace ByteBazaar.Domain.Entities;

public class AttributeDefinition
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public AttributeType Type { get; set; } = AttributeType.Select;
    public List<string> Options { get; set; } = new();
    public bool IsFilterable { get; set; } = true;
    public bool IsRequired { get; set; }
    public FilterWidget FilterWidget { get; set; } = FilterWidget.Checkbox;
    public int SortOrder { get; set; }

    public Category? Category { get; set; }
}

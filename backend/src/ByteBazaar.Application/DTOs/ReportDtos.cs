namespace ByteBazaar.Application.DTOs;

public class SalesReportRowDto
{
    public string Period { get; set; } = string.Empty;
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
}

public class CategoryReportRowDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int Orders { get; set; }
    public int Units { get; set; }
    public decimal Revenue { get; set; }
}

public class BrandReportRowDto
{
    public string BrandName { get; set; } = string.Empty;
    public int Orders { get; set; }
    public int Units { get; set; }
    public decimal Revenue { get; set; }
}

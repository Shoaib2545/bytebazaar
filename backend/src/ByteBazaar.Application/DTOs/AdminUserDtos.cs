using ByteBazaar.Domain;

namespace ByteBazaar.Application.DTOs;

// ----- Customers (admin, read-only) -----

public class AdminCustomerListItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int OrdersCount { get; set; }
    public decimal TotalSpent { get; set; }
}

public class CustomerOrderSummaryDto
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
}

public class AdminCustomerDetailDto : AdminCustomerListItemDto
{
    public List<CustomerOrderSummaryDto> RecentOrders { get; set; } = new();
}

// ----- Staff management (Admin role only) -----

public class StaffUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class StaffCreateRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
}

public class StaffUpdateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

namespace ByteBazaar.Domain;

public enum AttributeType
{
    Select,
    MultiSelect,
    Number,
    Boolean,
    Text
}

public enum FilterWidget
{
    Checkbox,
    Radio,
    Range
}

public enum ProductStatus
{
    Draft,
    Active
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

public enum PaymentMethod
{
    COD
}

public enum CouponType
{
    Percent,
    Fixed
}

public enum BannerPlacement
{
    Hero,
    Strip
}

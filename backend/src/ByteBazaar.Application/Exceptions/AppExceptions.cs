namespace ByteBazaar.Application.Exceptions;

/// <summary>Maps to HTTP 400 in the API.</summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }
}

/// <summary>Maps to HTTP 404 in the API.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Maps to HTTP 409 in the API — e.g. confirming an order when stock ran out.</summary>
public class StockConflictException : Exception
{
    public StockConflictException(string message) : base(message)
    {
    }
}

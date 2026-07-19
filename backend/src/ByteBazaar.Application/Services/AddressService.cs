using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class AddressService
{
    private readonly IAppDbContext _db;

    public AddressService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AddressDto>> GetAddressesAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.FullName)
            .Select(a => new AddressDto
            {
                Id = a.Id,
                FullName = a.FullName,
                Phone = a.Phone,
                AddressLine = a.AddressLine,
                City = a.City,
                Region = a.Region,
                IsDefault = a.IsDefault
            })
            .ToListAsync(ct);
    }

    public async Task<AddressDto> CreateAsync(Guid userId, AddressUpsertRequest request, CancellationToken ct = default)
    {
        var address = new Address { Id = Guid.NewGuid(), UserId = userId };
        Apply(address, request);
        if (request.IsDefault)
            await ClearDefaultAsync(userId, address.Id, ct);
        _db.Addresses.Add(address);
        await _db.SaveChangesAsync(ct);
        return ToDto(address);
    }

    public async Task<AddressDto?> UpdateAsync(Guid userId, Guid id, AddressUpsertRequest request, CancellationToken ct = default)
    {
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (address is null) return null;
        Apply(address, request);
        if (request.IsDefault)
            await ClearDefaultAsync(userId, id, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(address);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (address is null) return false;
        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ClearDefaultAsync(Guid userId, Guid exceptId, CancellationToken ct)
    {
        var others = await _db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault && a.Id != exceptId)
            .ToListAsync(ct);
        foreach (var other in others)
            other.IsDefault = false;
    }

    private static void Apply(Address address, AddressUpsertRequest request)
    {
        address.FullName = request.FullName;
        address.Phone = request.Phone;
        address.AddressLine = request.AddressLine;
        address.City = request.City;
        address.Region = request.Region;
        address.IsDefault = request.IsDefault;
    }

    private static AddressDto ToDto(Address a) => new()
    {
        Id = a.Id,
        FullName = a.FullName,
        Phone = a.Phone,
        AddressLine = a.AddressLine,
        City = a.City,
        Region = a.Region,
        IsDefault = a.IsDefault
    };
}

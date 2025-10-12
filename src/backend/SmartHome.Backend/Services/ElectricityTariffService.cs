using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Tariffs;

namespace SmartHome.Backend.Services;

public class ElectricityTariffService : IElectricityTariffService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _factory;
    public ElectricityTariffService(IDbContextFactory<SmartHomeDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<ElectricityTariffDto>> GetAllAsync(CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ElectricityTariffs
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new ElectricityTariffDto(t.Id, t.Name, t.IsTimeOfUse, t.CostF1, t.CostF2, t.CostF3, t.IsDefault, t.ValidFromUtc, t.ValidToUtc))
            .ToListAsync(ct);
    }

    public async Task<ElectricityTariffDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ElectricityTariffs
            .Where(t => t.Id == id)
            .Select(t => new ElectricityTariffDto(t.Id, t.Name, t.IsTimeOfUse, t.CostF1, t.CostF2, t.CostF3, t.IsDefault, t.ValidFromUtc, t.ValidToUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ElectricityTariffDto> CreateAsync(ElectricityTariffCreateUpdateDto dto, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        if (dto.IsDefault)
        {
            var defaults = await ctx.ElectricityTariffs.Where(t => t.IsDefault).ToListAsync(ct);
            foreach (var d in defaults) d.IsDefault = false;
        }
        var entity = new ElectricityTariff
        {
            Name = dto.Name,
            IsTimeOfUse = dto.IsTimeOfUse,
            CostF1 = dto.CostF1,
            CostF2 = dto.CostF2,
            CostF3 = dto.CostF3,
            IsDefault = dto.IsDefault,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc
        };
        ctx.ElectricityTariffs.Add(entity);
        await ctx.SaveChangesAsync(ct);
        return new ElectricityTariffDto(entity.Id, entity.Name, entity.IsTimeOfUse, entity.CostF1, entity.CostF2, entity.CostF3, entity.IsDefault, entity.ValidFromUtc, entity.ValidToUtc);
    }

    public async Task<bool> UpdateAsync(int id, ElectricityTariffCreateUpdateDto dto, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var entity = await ctx.ElectricityTariffs.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;
        if (dto.IsDefault && !entity.IsDefault)
        {
            var defaults = await ctx.ElectricityTariffs.Where(t => t.IsDefault && t.Id != id).ToListAsync(ct);
            foreach (var d in defaults) d.IsDefault = false;
        }
        entity.Name = dto.Name;
        entity.IsTimeOfUse = dto.IsTimeOfUse;
        entity.CostF1 = dto.CostF1;
        entity.CostF2 = dto.CostF2;
        entity.CostF3 = dto.CostF3;
        entity.IsDefault = dto.IsDefault;
        entity.ValidFromUtc = dto.ValidFromUtc;
        entity.ValidToUtc = dto.ValidToUtc;
        await ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var entity = await ctx.ElectricityTariffs.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;
        ctx.ElectricityTariffs.Remove(entity);
        await ctx.SaveChangesAsync(ct);
        return true;
    }
}

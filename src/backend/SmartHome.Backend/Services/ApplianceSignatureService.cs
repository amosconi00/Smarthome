using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Appliances;
using SmartHome.Backend.Dto.Signatures;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using SmartHome.Backend.Notifiers;

namespace SmartHome.Backend.Services;

public class ApplianceSignatureService : IApplianceSignatureService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _factory;
    private readonly IMapper _mapper;
    private readonly IApplianceSignatureChangeNotifier _notifier;

    public ApplianceSignatureService(IDbContextFactory<SmartHomeDbContext> factory, IMapper mapper, IApplianceSignatureChangeNotifier notifier)
    {
        _factory = factory;
        _mapper = mapper;
        _notifier = notifier;
    }

    public async Task<IEnumerable<ApplianceSignatureDto>> GetAllAsync(CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ApplianceSignatures
            .AsNoTracking()
            .OrderBy(s => s.ApplianceId).ThenBy(s => s.Version)
            .ProjectTo<ApplianceSignatureDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ApplianceSignatureDto>> GetByApplianceAsync(int applianceId, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ApplianceSignatures.AsNoTracking()
            .Where(s => s.ApplianceId == applianceId)
            .OrderBy(s => s.Version)
            .ProjectTo<ApplianceSignatureDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<ApplianceSignatureDto?> GetDefaultAsync(int applianceId, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ApplianceSignatures.AsNoTracking()
            .Where(s => s.ApplianceId == applianceId && s.IsDefault)
            .ProjectTo<ApplianceSignatureDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApplianceSignatureDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ApplianceSignatures.AsNoTracking()
            .Where(s => s.Id == id)
            .ProjectTo<ApplianceSignatureDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApplianceSignatureDto?> CreateAsync(int applianceId, ApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var applianceExists = await ctx.Appliances.AnyAsync(a => a.Id == applianceId, ct);
        if (!applianceExists) return null;

        var lastVersion = await ctx.ApplianceSignatures
            .Where(s => s.ApplianceId == applianceId)
            .MaxAsync(s => (int?)s.Version, ct) ?? 0;

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        if (dto.IsDefault)
        {
            await ctx.ApplianceSignatures
                .Where(s => s.ApplianceId == applianceId && s.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsDefault, false), ct);
        }

        var entity = new ApplianceSignature
        {
            ApplianceId = applianceId,
            Version = lastVersion + 1,
            CreatedAt = DateTime.UtcNow,
            IsDefault = dto.IsDefault
        };
        _mapper.Map(dto, entity);
        entity.IsDefault = dto.IsDefault;

        ctx.ApplianceSignatures.Add(entity);
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _notifier.NotifyChanged(); // notify
        return _mapper.Map<ApplianceSignatureDto>(entity);
    }

    public async Task<bool> UpdateAsync(int id, ApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var entity = await ctx.ApplianceSignatures.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return false;

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        if (dto.IsDefault)
        {
            await ctx.ApplianceSignatures
                .Where(s => s.ApplianceId == entity.ApplianceId && s.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsDefault, false), ct);
            await ctx.Entry(entity).ReloadAsync(ct);
        }

        _mapper.Map(dto, entity);
        entity.IsDefault = dto.IsDefault;
        ctx.Entry(entity).Property(e => e.IsDefault).IsModified = true;

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _notifier.NotifyChanged(); // notify
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var sig = await ctx.ApplianceSignatures.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sig is null) return false;
        ctx.ApplianceSignatures.Remove(sig);
        await ctx.SaveChangesAsync(ct);
        _notifier.NotifyChanged(); // notify
        return true;
    }
}

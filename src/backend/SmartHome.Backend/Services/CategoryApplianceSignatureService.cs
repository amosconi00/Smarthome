using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.CategoryAppliances;
using SmartHome.Backend.Dto.Signatures;
using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace SmartHome.Backend.Services;

public class CategoryApplianceSignatureService : ICategoryApplianceSignatureService
{
    private readonly IDbContextFactory<SmartHomeDbContext> _factory;
    private readonly IMapper _mapper;

    public CategoryApplianceSignatureService(IDbContextFactory<SmartHomeDbContext> factory, IMapper mapper)
    {
        _factory = factory;
        _mapper = mapper;
    }

    public async Task<IEnumerable<CategoryApplianceSignatureDto>> GetAllAsync(CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.CategoryApplianceSignatures
            .AsNoTracking()
            .ProjectTo<CategoryApplianceSignatureDto>(_mapper.ConfigurationProvider)
            .OrderBy(s => s.CategoryApplianceId)
            .ToListAsync(ct);
    }

    public async Task<CategoryApplianceSignatureDto?> GetByIdAsync(int categoryApplianceId, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var sig = await ctx.CategoryApplianceSignatures.AsNoTracking().FirstOrDefaultAsync(s => s.CategoryApplianceId == categoryApplianceId, ct);
        return sig is null ? null : _mapper.Map<CategoryApplianceSignatureDto>(sig);
    }

    public async Task<CategoryApplianceSignatureDto?> CreateAsync(int categoryApplianceId, CategoryApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var exists = await ctx.CategoryApplianceSignatures.AnyAsync(s => s.CategoryApplianceId == categoryApplianceId, ct);
        if (exists) return null;
        var entity = new CategoryApplianceSignature
        {
            CategoryApplianceId = categoryApplianceId,
            CategoryAppliance = await ctx.CategoryAppliances.FirstAsync(a => a.Id == categoryApplianceId, ct)
        };
        _mapper.Map(dto, entity);
        ctx.CategoryApplianceSignatures.Add(entity);
        await ctx.SaveChangesAsync(ct);
        return _mapper.Map<CategoryApplianceSignatureDto>(entity);
    }

    public async Task<bool> UpdateAsync(int categoryApplianceId, CategoryApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var entity = await ctx.CategoryApplianceSignatures.FirstOrDefaultAsync(s => s.CategoryApplianceId == categoryApplianceId, ct);
        if (entity is null) return false;
        _mapper.Map(dto, entity);
        await ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int categoryApplianceId, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var sig = await ctx.CategoryApplianceSignatures.FirstOrDefaultAsync(s => s.CategoryApplianceId == categoryApplianceId, ct);
        if (sig is null) return false;
        ctx.CategoryApplianceSignatures.Remove(sig);
        await ctx.SaveChangesAsync(ct);
        return true;
    }
}

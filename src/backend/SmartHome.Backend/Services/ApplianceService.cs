using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Appliances;

namespace SmartHome.Backend.Services
{
    public class ApplianceService : IApplianceService
    {
        private readonly IDbContextFactory<SmartHomeDbContext> _contextFactory;
        private readonly IMapper _mapper;

        public ApplianceService(IDbContextFactory<SmartHomeDbContext> contextFactory, IMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ApplianceDto>> GetAllAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            // For backward compatibility keep return type ApplianceDto but project lightweight and then enrich if needed
            return await context.Appliances
                .OrderBy(a => a.Id)
                .ProjectTo<ApplianceDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ApplianceListItemDto>> GetAllListAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Appliances
                .OrderBy(a => a.Id)
                .ProjectTo<ApplianceListItemDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
        }

        public async Task<ApplianceDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Appliances
                .Where(a => a.Id == id)
                .ProjectTo<ApplianceDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ApplianceDto> CreateAsync(ApplianceCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Create appliance first
            var entity = _mapper.Map<Appliance>(dto);
            await context.Appliances.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken); // obtain Id

            // If appliance has a category and that category has a signature, clone it as default appliance signature
            if (entity.CategoryApplianceId.HasValue)
            {
                var categorySignature = await context.CategoryApplianceSignatures
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.CategoryApplianceId == entity.CategoryApplianceId.Value, cancellationToken);

                if (categorySignature is not null)
                {
                    var applianceSignature = new ApplianceSignature
                    {
                        ApplianceId = entity.Id,
                        Appliance = entity,
                        Name = "default",
                        Version = 1, // first signature for this appliance
                        CreatedAt = DateTime.UtcNow,
                        IsDefault = true,
                        OnDeltaMinW = categorySignature.OnDeltaMinW,
                        OnDeltaMaxW = categorySignature.OnDeltaMaxW,
                        OffDeltaMinW = categorySignature.OffDeltaMinW,
                        OffDeltaMaxW = categorySignature.OffDeltaMaxW,
                        MinOnSeconds = categorySignature.MinOnSeconds,
                        RefractorySeconds = categorySignature.RefractorySeconds
                    };

                    context.ApplianceSignatures.Add(applianceSignature);
                    // Add to navigation collection so mapper can include it without reloading
                    entity.Signatures.Add(applianceSignature);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            return _mapper.Map<ApplianceDto>(entity);
        }

        public async Task<bool> UpdateAsync(int id, ApplianceCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var exists = await context.Appliances.AnyAsync(a => a.Id == id, cancellationToken);
            if (!exists)
                return false;

            var entity = _mapper.Map<Appliance>(dto);
            entity.Id = id;
            context.Entry(entity).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                var stillExists = await context.Appliances.AnyAsync(a => a.Id == id, cancellationToken);
                return stillExists; // false -> not found
            }
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var stub = new Appliance { Id = id };
            context.Entry(stub).State = EntityState.Deleted;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                var exists = await context.Appliances.AnyAsync(a => a.Id == id, cancellationToken);
                return false;
            }
        }
    }
}

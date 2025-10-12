using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.PowerMeters;
using SmartHome.Backend.Notifiers;

namespace SmartHome.Backend.Services
{
    public class PowerMeterService : IPowerMeterService
    {
        private readonly IDbContextFactory<SmartHomeDbContext> _contextFactory;
        private readonly IMapper _mapper;
        private readonly IPowerMeterChangeNotifier _notifier; // added

        public PowerMeterService(IDbContextFactory<SmartHomeDbContext> contextFactory, IMapper mapper, IPowerMeterChangeNotifier notifier) // added param
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
            _notifier = notifier; // store
        }

        public async Task<IEnumerable<PowerMeterDto>> GetAllAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.PowerMeters
                .OrderBy(p => p.Id)
                .ProjectTo<PowerMeterDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
        }

        public async Task<PowerMeterDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.PowerMeters
                .Where(p => p.Id == id)
                .ProjectTo<PowerMeterDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PowerMeterDto> CreateAsync(PowerMeterCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = _mapper.Map<PowerMeter>(dto);
            await context.PowerMeters.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            _notifier.NotifyChanged(); // notify
            return _mapper.Map<PowerMeterDto>(entity);
        }

        public async Task<bool> UpdateAsync(int id, PowerMeterCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var exists = await context.PowerMeters.AnyAsync(p => p.Id == id, cancellationToken);
            if (!exists)
                return false;

            var entity = _mapper.Map<PowerMeter>(dto);
            entity.Id = id;
            context.Entry(entity).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                _notifier.NotifyChanged(); // notify
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                var stillExists = await context.PowerMeters.AnyAsync(p => p.Id == id, cancellationToken);
                return stillExists;
            }
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var stub = new PowerMeter { Id = id };
            context.Entry(stub).State = EntityState.Deleted;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                _notifier.NotifyChanged(); // notify
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }
    }
}

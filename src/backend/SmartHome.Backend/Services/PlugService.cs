using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Plugs;
using SmartHome.Backend.Notifiers;

namespace SmartHome.Backend.Services
{
    public class PlugService : IPlugService
    {
        private readonly IDbContextFactory<SmartHomeDbContext> _contextFactory;
        private readonly IMapper _mapper;
        private readonly IPlugChangeNotifier _notifier; // added

        public PlugService(IDbContextFactory<SmartHomeDbContext> contextFactory, IMapper mapper, IPlugChangeNotifier notifier) // added param
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
            _notifier = notifier; // store
        }

        public async Task<IEnumerable<PlugDto>> GetAllAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Plugs
                .OrderBy(p => p.Id)
                .ProjectTo<PlugDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
        }

        public async Task<PlugDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Plugs
                .Where(p => p.Id == id)
                .ProjectTo<PlugDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PlugDto> CreateAsync(PlugCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = _mapper.Map<Plug>(dto);
            await context.Plugs.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            _notifier.NotifyChanged(); // notify
            return _mapper.Map<PlugDto>(entity);
        }

        public async Task<bool> UpdateAsync(int id, PlugCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var exists = await context.Plugs.AnyAsync(p => p.Id == id, cancellationToken);
            if (!exists)
                return false;

            var entity = _mapper.Map<Plug>(dto);
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
                var stillExists = await context.Plugs.AnyAsync(p => p.Id == id, cancellationToken);
                return stillExists;
            }
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var stub = new Plug { Id = id };
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

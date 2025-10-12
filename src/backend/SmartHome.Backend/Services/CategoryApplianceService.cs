using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.CategoryAppliances;

namespace SmartHome.Backend.Services
{
    public class CategoryApplianceService : ICategoryApplianceService
    {
        private readonly IDbContextFactory<SmartHomeDbContext> _contextFactory;
        private readonly IMapper _mapper;

        public CategoryApplianceService(IDbContextFactory<SmartHomeDbContext> contextFactory, IMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CategoryApplianceDto>> GetAllAsync(CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CategoryAppliances
                .OrderBy(c => c.Id)
                .ProjectTo<CategoryApplianceDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
        }

        public async Task<CategoryApplianceDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CategoryAppliances
                .Where(c => c.Id == id)
                .ProjectTo<CategoryApplianceDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<CategoryApplianceDto> CreateAsync(CategoryApplianceCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = _mapper.Map<CategoryAppliance>(dto);
            await context.CategoryAppliances.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return _mapper.Map<CategoryApplianceDto>(entity);
        }

        public async Task<bool> UpdateAsync(int id, CategoryApplianceCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var exists = await context.CategoryAppliances.AnyAsync(c => c.Id == id, cancellationToken);
            if (!exists)
                return false;

            var entity = _mapper.Map<CategoryAppliance>(dto);
            entity.Id = id;
            context.Entry(entity).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                var stillExists = await context.CategoryAppliances.AnyAsync(c => c.Id == id, cancellationToken);
                return stillExists;
            }
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var stub = new CategoryAppliance { Id = id };
            context.Entry(stub).State = EntityState.Deleted;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }
    }
}

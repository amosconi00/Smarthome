using AutoMapper;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.CategoryAppliances;

namespace SmartHome.Backend.Mapping
{
    public class CategoryApplianceProfile : Profile
    {
        public CategoryApplianceProfile()
        {
            CreateMap<CategoryAppliance, CategoryApplianceDto>();
            CreateMap<CategoryApplianceCreateUpdateDto, CategoryAppliance>();
        }
    }
}

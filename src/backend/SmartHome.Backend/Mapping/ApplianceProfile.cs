using AutoMapper;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Appliances;
using System.Linq;

namespace SmartHome.Backend.Mapping
{
    public class ApplianceProfile : Profile
    {
        public ApplianceProfile()
        {
            CreateMap<Appliance, ApplianceDto>()
                .ForCtorParam("SignatureVersion", opt => opt.MapFrom(src => src.Signatures.Where(s => s.IsDefault).Select(s => (int?)s.Version).FirstOrDefault() ?? 0))
                .ForCtorParam("Signatures", opt => opt.MapFrom(src => src.Signatures));

            CreateMap<Appliance, ApplianceListItemDto>()
                .ForCtorParam("SignatureVersion", opt => opt.MapFrom(src => src.Signatures.Where(s => s.IsDefault).Select(s => (int?)s.Version).FirstOrDefault() ?? 0));

            CreateMap<ApplianceCreateUpdateDto, Appliance>();
        }
    }
}

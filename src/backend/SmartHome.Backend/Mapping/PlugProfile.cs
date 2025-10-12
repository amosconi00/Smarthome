using AutoMapper;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Plugs;

namespace SmartHome.Backend.Mapping
{
    public class PlugProfile : Profile
    {
        public PlugProfile()
        {
            CreateMap<Plug, PlugDto>();
            CreateMap<PlugCreateUpdateDto, Plug>();
        }
    }
}

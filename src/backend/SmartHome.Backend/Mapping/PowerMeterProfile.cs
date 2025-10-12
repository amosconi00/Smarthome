using AutoMapper;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.PowerMeters;

namespace SmartHome.Backend.Mapping
{
    public class PowerMeterProfile : Profile
    {
        public PowerMeterProfile()
        {
            CreateMap<PowerMeter, PowerMeterDto>();
            CreateMap<PowerMeterCreateUpdateDto, PowerMeter>();
        }
    }
}

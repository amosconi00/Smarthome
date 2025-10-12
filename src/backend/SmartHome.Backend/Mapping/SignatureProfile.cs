using AutoMapper;
using SmartHome.Backend.Dal.Entity;
using SmartHome.Backend.Dto.Appliances;
using SmartHome.Backend.Dto.CategoryAppliances;
using SmartHome.Backend.Dto.Signatures;

namespace SmartHome.Backend.Mapping;

public class SignatureProfile : Profile
{
    public SignatureProfile()
    {
        CreateMap<ApplianceSignature, ApplianceSignatureDto>();
        CreateMap<CategoryApplianceSignature, CategoryApplianceSignatureDto>();

        CreateMap<ApplianceSignatureCreateUpdateDto, ApplianceSignature>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.ApplianceId, o => o.Ignore())
            .ForMember(d => d.Appliance, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore());
        CreateMap<CategoryApplianceSignatureCreateUpdateDto, CategoryApplianceSignature>()
            .ForMember(d => d.CategoryApplianceId, o => o.Ignore())
            .ForMember(d => d.CategoryAppliance, o => o.Ignore());
    }
}

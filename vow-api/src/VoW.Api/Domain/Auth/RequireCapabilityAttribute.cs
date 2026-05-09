using Microsoft.AspNetCore.Authorization;

namespace VoW.Api.Domain.Auth;

public sealed class RequireCapabilityAttribute : AuthorizeAttribute
{
    public Capability RequiredCapability { get; }

    public RequireCapabilityAttribute(Capability capability)
        : base(CapabilityMapper.ToClaimValue(capability))
    {
        RequiredCapability = capability;
    }
}

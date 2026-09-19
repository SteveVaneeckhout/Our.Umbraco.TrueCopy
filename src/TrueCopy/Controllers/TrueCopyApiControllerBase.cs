using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Filters;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;
using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Controllers
{
    [ApiController]
    [BackOfficeRoute("truecopy/api/v{version:apiVersion}")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
    [MapToApi(Constants.ApiName)]
    [JsonOptionsName(UmbConstants.JsonOptionsNames.BackOffice)]
    public class TrueCopyApiControllerBase : ControllerBase
    {
    }
}

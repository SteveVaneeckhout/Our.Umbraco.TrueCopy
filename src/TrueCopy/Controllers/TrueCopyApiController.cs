using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Our.Umbraco.TrueCopy.Resources;
using Our.Umbraco.TrueCopy.Services;
using Our.Umbraco.TrueCopy.ViewModels;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Our.Umbraco.TrueCopy.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "TrueCopy")]
    public class TrueCopyApiController : TrueCopyApiControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly ITrueCopyService _trueCopyService;

        public TrueCopyApiController(
            IAuthorizationService authorizationService,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            ITrueCopyService trueCopyService)
        {
            _authorizationService = authorizationService;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _trueCopyService = trueCopyService;
        }

        /// <summary>
        ///     Copies a document and repoints the internal links in the copies at the copies.
        /// </summary>
        /// <remarks>
        ///     Note there is deliberately no <c>ProducesResponseType</c> for 401 or 403 on this - or any
        ///     - action. Umbraco 17's Swashbuckle filter adds the 401 itself; on Umbraco 18 declaring
        ///     either breaks OpenAPI generation, and this action is kept the same on both branches.
        /// </remarks>
        [HttpPost("copy")]
        [ProducesResponseType<TrueCopyResultModel>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Copy(
            CancellationToken cancellationToken,
            TrueCopyRequestModel request)
        {
            // The controller's section policy only says the caller may be in the Content section. Copying
            // is a content write, so the individual documents are authorized too - the same two checks,
            // against the same permissions, that core's own CopyDocumentController makes.
            //
            // ActionLetter, not ActionAlias: in Umbraco 17 the two read backwards from their names.
            // ActionCopy.ActionLetter is "Umb.Document.Duplicate", the permission the user actually holds,
            // while ActionCopy.ActionAlias is the legacy "copy". Authorizing on the alias silently fails
            // every check, because no user is ever granted a permission by that name.

            // Everything below reports in the caller's own backoffice language. These strings reach the
            // editor as ProblemDetails, which core renders generically from the response body, so there
            // is no client-side hook to localize them at - the server has to have done it already.
            CultureInfo culture = CurrentUserCulture();

            if (!await IsAuthorized(ActionCopy.ActionLetter, request.SourceId))
            {
                return Forbidden(culture, "ForbiddenDuplicate");
            }

            if (!await IsAuthorized(ActionNew.ActionLetter, request.TargetParentId))
            {
                return Forbidden(culture, "ForbiddenCreate");
            }

            Guid userKey = CurrentUserKey();
            if (userKey == Guid.Empty)
            {
                return Forbidden(culture, "ForbiddenNoUser");
            }

            Attempt<TrueCopyResultModel?, ContentEditingOperationStatus> attempt =
                await _trueCopyService.CopyAsync(request, userKey);

            if (attempt.Success)
            {
                return Ok(attempt.Result);
            }

            return attempt.Status switch
            {
                ContentEditingOperationStatus.NotFound or ContentEditingOperationStatus.ParentNotFound
                    => NotFound(new ProblemDetails
                    {
                        Title = TrueCopyText.Get("NotFoundTitle", culture),
                        Detail = TrueCopyText.Get("NotFoundDetail", culture),
                        Status = StatusCodes.Status404NotFound,
                    }),
                ContentEditingOperationStatus.NotAllowed or ContentEditingOperationStatus.ParentInvalid
                    => BadRequest(new ProblemDetails
                    {
                        Title = TrueCopyText.Get("NotAllowedTitle", culture),
                        Detail = TrueCopyText.Get("NotAllowedDetail", culture),
                        Status = StatusCodes.Status400BadRequest,
                    }),
                // The status name stays in the detail untranslated: it is the one thing that makes an
                // otherwise unexplained failure actionable in a support report.
                _ => BadRequest(new ProblemDetails
                {
                    Title = TrueCopyText.Get("FailedTitle", culture),
                    Detail = TrueCopyText.Get("FailedDetail", culture, attempt.Status),
                    Status = StatusCodes.Status400BadRequest,
                }),
            };
        }

        /// <summary>
        ///     A bare <c>Forbid()</c> sends no body, which leaves an editor with a failed copy and no idea
        ///     which of the two permissions they are missing.
        /// </summary>
        private ObjectResult Forbidden(CultureInfo culture, string detailKey)
            => StatusCode(
                StatusCodes.Status403Forbidden,
                new ProblemDetails
                {
                    Title = TrueCopyText.Get("ForbiddenTitle", culture),
                    Detail = TrueCopyText.Get(detailKey, culture),
                    Status = StatusCodes.Status403Forbidden,
                });

        private async Task<bool> IsAuthorized(string permission, Guid? contentKey)
        {
            AuthorizationResult result = await _authorizationService.AuthorizeResourceAsync(
                User,
                ContentPermissionResource.WithKeys(permission, contentKey),
                AuthorizationPolicies.ContentPermissionByResource);

            return result.Succeeded;
        }

        private Guid CurrentUserKey()
            => _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key ?? Guid.Empty;

        /// <summary>The caller's backoffice UI language, or the invariant culture when there is no user.</summary>
        private CultureInfo CurrentUserCulture()
            => TrueCopyText.CultureFor(_backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Language);
    }
}

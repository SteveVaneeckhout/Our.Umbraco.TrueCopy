using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Our.Umbraco.TrueCopy.Rewriting;
using Our.Umbraco.TrueCopy.Services;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Our.Umbraco.TrueCopy.Composers;

public class TrueCopyApiComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<ITrueCopyService, TrueCopyService>();
        builder.Services.AddSingleton<TrueCopyOperationAccessor>();

        // Registered for every copy in the installation, not just ours. The handler's first act is to
        // check whether a TrueCopy operation is running and return if not, so Umbraco's own Copy is
        // untouched - see TrueCopyOperationAccessor.
        builder.AddNotificationHandler<ContentCopiedNotification, TrueCopyKeyMapHandler>();

        // Ordered: the first rewriter that claims an editor alias handles it. A consuming site can
        // Append its own for a custom editor.
        builder.WithCollectionBuilder<LinkRewriterCollectionBuilder>()
            .Append<ContentPickerRewriter>()
            .Append<MultiNodeTreePickerRewriter>()
            .Append<MultiUrlPickerRewriter>()
            .Append<RichTextRewriter>()
            .Append<BlockEditorRewriter>();

        builder.Services.AddSingleton<IOperationIdHandler, TrueCopyOperationIdHandler>();

        // Umbraco 17 generates OpenAPI with Swashbuckle; the document is served at
        // /umbraco/swagger/truecopy/swagger.json. See
        // https://docs.umbraco.com/umbraco-cms/17.latest/tutorials/creating-a-backoffice-api
        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            options.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "True Copy Backoffice API",
                Version = "1.0",
            });

            options.OperationFilter<TrueCopyOperationSecurityFilter>();
        });
    }

    /// <summary>Marks every operation in this package's document as requiring backoffice authentication.</summary>
    public class TrueCopyOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }

    /// <summary>
    ///     Names operations HTTP method + action (<c>PostCopy</c>), which is what the generated TypeScript
    ///     client's function name (<c>postCopy</c>) is derived from. The Umbraco 18 line gets the same
    ///     name from its own OpenAPI generator, so the client stays identical across both lines.
    /// </summary>
    public class TrueCopyOperationIdHandler : OperationIdHandler
    {
        public TrueCopyOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
            : base(apiVersioningOptions)
        {
        }

        protected override bool CanHandle(
            ApiDescription apiDescription,
            ControllerActionDescriptor controllerActionDescriptor)
            => controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(
                "Our.Umbraco.TrueCopy.Controllers",
                StringComparison.Ordinal) is true;

        public override string Handle(ApiDescription apiDescription)
        {
            string method = apiDescription.HttpMethod ?? "Get";
            return $"{char.ToUpperInvariant(method[0])}{method[1..].ToLowerInvariant()}"
                   + apiDescription.ActionDescriptor.RouteValues["action"];
        }
    }
}

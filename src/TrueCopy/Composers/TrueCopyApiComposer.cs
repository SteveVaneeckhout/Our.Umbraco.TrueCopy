using Microsoft.Extensions.DependencyInjection;
using Our.Umbraco.TrueCopy.Rewriting;
using Our.Umbraco.TrueCopy.Services;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using UmbConstants = Umbraco.Cms.Core.Constants;

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

        builder.AddBackOfficeOpenApiDocument(
            Constants.ApiName,
            document => document
                .WithTitle("True Copy Backoffice API")
                .WithBackOfficeAuthentication()
                .WithJsonOptions(UmbConstants.JsonOptionsNames.BackOffice)
                .ConfigureOpenApiOptions(options =>
                    options.AddDocumentTransformer((doc, _, _) =>
                    {
                        doc.Info.Version = "1.0";
                        return Task.CompletedTask;
                    })));
    }
}

using Microsoft.Extensions.Logging;
using Our.Umbraco.TrueCopy.Rewriting;
using Our.Umbraco.TrueCopy.ViewModels;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Our.Umbraco.TrueCopy.Services;

/// <summary>
///     Copy, then fix the links.
/// </summary>
public sealed class TrueCopyService : ITrueCopyService
{
    /// <summary>How many external references the report will list before it stops.</summary>
    private const int MaxReportedExternalReferences = 200;

    private readonly IContentEditingService _contentEditingService;
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly LinkRewriterCollection _rewriters;
    private readonly TrueCopyOperationAccessor _operationAccessor;
    private readonly ILogger<TrueCopyService> _logger;

    public TrueCopyService(
        IContentEditingService contentEditingService,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IUserIdKeyResolver userIdKeyResolver,
        LinkRewriterCollection rewriters,
        TrueCopyOperationAccessor operationAccessor,
        ILogger<TrueCopyService> logger)
    {
        _contentEditingService = contentEditingService;
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _userIdKeyResolver = userIdKeyResolver;
        _rewriters = rewriters;
        _operationAccessor = operationAccessor;
        _logger = logger;
    }

    public async Task<Attempt<TrueCopyResultModel?, ContentEditingOperationStatus>> CopyAsync(
        TrueCopyRequestModel request,
        Guid userKey)
    {
        // The accessor is what tells TrueCopyKeyMapHandler that the copy about to happen is ours. Every
        // other copy in the installation sees no operation here and is left completely alone.
        using TrueCopyOperationAccessor.Scope scope = _operationAccessor.Begin();

        Attempt<IContent?, ContentEditingOperationStatus> copyAttempt = await _contentEditingService.CopyAsync(
            request.SourceId,
            request.TargetParentId,
            request.RelateToOriginal,
            request.IncludeDescendants,
            userKey);

        if (!copyAttempt.Success || copyAttempt.Result is null)
        {
            return Attempt.FailWithStatus<TrueCopyResultModel?, ContentEditingOperationStatus>(
                copyAttempt.Status, null);
        }

        IContent rootCopy = copyAttempt.Result;
        TrueCopyOperation operation = scope.Operation;

        RewriteResult rewrite = await RewriteCopiesAsync(operation, userKey);

        return Attempt.SucceedWithStatus<TrueCopyResultModel?, ContentEditingOperationStatus>(
            ContentEditingOperationStatus.Success,
            new TrueCopyResultModel
            {
                RootCopyId = rootCopy.Key,
                RootCopyName = rootCopy.Name,
                CopiedCount = operation.KeyMap.Count,
                DocumentsChangedCount = rewrite.DocumentsChanged,
                RewrittenLinkCount = rewrite.RewrittenLinks,
                ExternalReferenceCount = rewrite.ExternalReferences.Count,
                ExternalReferencesTruncated = rewrite.ExternalReferences.Count > MaxReportedExternalReferences,
                ExternalReferences = MapExternalReferences(rewrite.ExternalReferences),
            });
    }

    private async Task<RewriteResult> RewriteCopiesAsync(TrueCopyOperation operation, Guid userKey)
    {
        var context = new RewriteContext(
            operation.KeyMap,
            operation.IdMap,
            _rewriters,
            CreateNestedEditorAliasResolver());

        var changed = new List<IContent>();

        foreach (Guid copyKey in operation.KeyMap.Values)
        {
            IContent? copy = _contentService.GetById(copyKey);
            if (copy is null)
            {
                continue;
            }

            if (RewriteDocument(copy, context))
            {
                changed.Add(copy);
            }
        }

        if (changed.Count > 0)
        {
            // Saving is fine here, unlike ContentDashboard's ownership transfer: these documents were
            // created seconds ago, so stamping UpdateDate and WriterId destroys no signal.
            int writerId = await _userIdKeyResolver.GetAsync(userKey);
            _contentService.Save(changed, writerId);
        }

        return new RewriteResult(changed.Count, context.RewrittenLinkCount, context.ExternalReferences);
    }

    private bool RewriteDocument(IContent copy, RewriteContext context)
    {
        context.BeginDocument(copy.Key, copy.Name);
        var changed = false;

        foreach (IProperty property in copy.Properties)
        {
            string editorAlias = property.PropertyType.PropertyEditorAlias;
            context.BeginProperty(property.Alias);

            // ToList: SetValue writes back into this same collection.
            foreach (IPropertyValue propertyValue in property.Values.ToList())
            {
                // Only the draft values matter. ContentService.Copy sets Published = false and clears the
                // publish infos, so a fresh copy has no published values to rewrite.
                if (propertyValue.EditedValue is not string value || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string? rewritten = SafeRewrite(editorAlias, value, context, copy, property.Alias);
                if (rewritten is null)
                {
                    continue;
                }

                copy.SetValue(property.Alias, rewritten, propertyValue.Culture, propertyValue.Segment);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    ///     A rewriter that throws must not cost the whole copy. The copy itself has already happened and
    ///     is valid; one property we could not parse means one link an editor has to fix by hand.
    /// </summary>
    private string? SafeRewrite(
        string editorAlias,
        string value,
        RewriteContext context,
        IContent copy,
        string propertyAlias)
    {
        try
        {
            return _rewriters.Rewrite(editorAlias, value, context);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "TrueCopy could not rewrite {EditorAlias} property {PropertyAlias} on {DocumentKey}; left unchanged.",
                editorAlias,
                propertyAlias,
                copy.Key);
            return null;
        }
    }

    /// <summary>
    ///     Resolves the editor alias of a nested block property from its element type, for values written
    ///     by a version of Umbraco that did not persist <c>editorAlias</c> alongside them.
    /// </summary>
    private Func<Guid, string, string?> CreateNestedEditorAliasResolver()
    {
        var cache = new Dictionary<(Guid, string), string?>();

        return (contentTypeKey, propertyAlias) =>
        {
            if (cache.TryGetValue((contentTypeKey, propertyAlias), out string? cached))
            {
                return cached;
            }

            string? alias = _contentTypeService.Get(contentTypeKey)?
                .CompositionPropertyTypes
                .FirstOrDefault(propertyType => propertyType.Alias == propertyAlias)?
                .PropertyEditorAlias;

            cache[(contentTypeKey, propertyAlias)] = alias;
            return alias;
        };
    }

    private IEnumerable<ExternalReferenceModel> MapExternalReferences(IReadOnlyList<ExternalReference> references)
    {
        ExternalReference[] reported = references.Take(MaxReportedExternalReferences).ToArray();

        var targetNames = reported
            .Select(reference => reference.TargetKey)
            .Distinct()
            .ToDictionary(key => key, key => _contentService.GetById(key)?.Name);

        return reported.Select(reference => new ExternalReferenceModel
        {
            DocumentId = reference.DocumentKey,
            DocumentName = reference.DocumentName,
            PropertyAlias = reference.PropertyAlias,
            TargetId = reference.TargetKey,
            TargetName = targetNames.GetValueOrDefault(reference.TargetKey),
        });
    }

    private record RewriteResult(
        int DocumentsChanged,
        int RewrittenLinks,
        IReadOnlyList<ExternalReference> ExternalReferences);
}

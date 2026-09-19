namespace Our.Umbraco.TrueCopy.ViewModels;

/// <summary>What to copy, and where to.</summary>
public class TrueCopyRequestModel
{
    /// <summary>The document to copy.</summary>
    public required Guid SourceId { get; set; }

    /// <summary>The document to copy it under. Null means the content root.</summary>
    public Guid? TargetParentId { get; set; }

    /// <summary>Copy the whole subtree. Without this only the one page is copied.</summary>
    public bool IncludeDescendants { get; set; }

    /// <summary>Record Umbraco's usual "relate to original" relation for each copied node.</summary>
    public bool RelateToOriginal { get; set; }
}

/// <summary>A link in a copy that points at something the copy did not include.</summary>
public class ExternalReferenceModel
{
    /// <summary>The copy holding the link.</summary>
    public required Guid DocumentId { get; set; }

    public string? DocumentName { get; set; }

    /// <summary>The property the link sits in.</summary>
    public required string PropertyAlias { get; set; }

    /// <summary>What it points at - still the original target, which is correct.</summary>
    public required Guid TargetId { get; set; }

    public string? TargetName { get; set; }
}

/// <summary>What the copy did.</summary>
public class TrueCopyResultModel
{
    /// <summary>The root of the new copy.</summary>
    public required Guid RootCopyId { get; set; }

    public string? RootCopyName { get; set; }

    /// <summary>How many documents were copied, the root included.</summary>
    public required int CopiedCount { get; set; }

    /// <summary>How many of those copies had at least one link rewritten.</summary>
    public required int DocumentsChangedCount { get; set; }

    /// <summary>How many individual links were repointed at a copy.</summary>
    public required int RewrittenLinkCount { get; set; }

    /// <summary>Links left pointing outside the copy. Correct as they stand, but worth seeing.</summary>
    public required IEnumerable<ExternalReferenceModel> ExternalReferences { get; set; }

    /// <summary>Whether <see cref="ExternalReferences" /> was capped.</summary>
    public required bool ExternalReferencesTruncated { get; set; }

    /// <summary>Total external references found, whether or not they were all listed.</summary>
    public required int ExternalReferenceCount { get; set; }
}

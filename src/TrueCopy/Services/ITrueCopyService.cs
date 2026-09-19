using Our.Umbraco.TrueCopy.ViewModels;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Our.Umbraco.TrueCopy.Services;

public interface ITrueCopyService
{
    /// <summary>
    ///     Copies a document the way Umbraco does, then repoints every internal link in the copies that
    ///     pointed at something the copy included.
    /// </summary>
    Task<Attempt<TrueCopyResultModel?, ContentEditingOperationStatus>> CopyAsync(
        TrueCopyRequestModel request,
        Guid userKey);
}

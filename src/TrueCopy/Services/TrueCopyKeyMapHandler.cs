using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Our.Umbraco.TrueCopy.Services;

/// <summary>
///     Collects the original-to-copy key map for a TrueCopy operation.
/// </summary>
/// <remarks>
///     <c>ContentService.Copy</c> raises one <c>ContentCopiedNotification</c> per copied node - the root
///     and every descendant - and the scoped publisher dispatches them together once the copy's scope has
///     completed. So by the time the copy call returns, this handler has seen every pair, which is
///     precisely the map the rewrite needs and the reason TrueCopy does not have to re-derive it from
///     <c>relateDocumentOnCopy</c> relations (which only exist when the editor ticked "relate to
///     original", and which accumulate across every copy ever made).
///     <para>
///         Both overloads are implemented and recording is idempotent, so it makes no difference whether
///         Umbraco delivers one notification or the whole batch.
///     </para>
/// </remarks>
public sealed class TrueCopyKeyMapHandler : INotificationHandler<ContentCopiedNotification>
{
    private readonly TrueCopyOperationAccessor _operationAccessor;

    public TrueCopyKeyMapHandler(TrueCopyOperationAccessor operationAccessor)
        => _operationAccessor = operationAccessor;

    public void Handle(ContentCopiedNotification notification)
        => _operationAccessor.Current?.Record(notification.Original, notification.Copy);

    public void Handle(IEnumerable<ContentCopiedNotification> notifications)
    {
        // The one line that keeps every other copy in the installation unaffected.
        TrueCopyOperation? operation = _operationAccessor.Current;
        if (operation is null)
        {
            return;
        }

        foreach (ContentCopiedNotification notification in notifications)
        {
            operation.Record(notification.Original, notification.Copy);
        }
    }
}

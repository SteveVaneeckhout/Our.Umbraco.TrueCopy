using Umbraco.Cms.Core.Models;

namespace Our.Umbraco.TrueCopy.Services;

/// <summary>
///     The original-to-copy mapping accumulated while one TrueCopy operation runs.
/// </summary>
public sealed class TrueCopyOperation
{
    private readonly Dictionary<Guid, Guid> _keyMap = [];
    private readonly Dictionary<int, int> _idMap = [];

    public IReadOnlyDictionary<Guid, Guid> KeyMap => _keyMap;

    /// <summary>Ids as well as keys, for the pre-v7 <c>{localLink:1234}</c> form in old rich text.</summary>
    public IReadOnlyDictionary<int, int> IdMap => _idMap;

    /// <summary>
    ///     Records one copied node. Idempotent, so it does not matter whether Umbraco delivers the
    ///     notifications one at a time or as a batch.
    /// </summary>
    public void Record(IContent original, IContent copy)
    {
        _keyMap[original.Key] = copy.Key;
        _idMap[original.Id] = copy.Id;
    }
}

/// <summary>
///     Holds the TrueCopy operation, if any, that the current call stack is running inside.
/// </summary>
/// <remarks>
///     <see cref="TrueCopyKeyMapHandler" /> is registered globally and therefore sees every copy in the
///     installation, including ones started from Umbraco's own Copy menu item. This is how it tells the
///     two apart: no operation on the accessor means the copy is not ours, and the handler returns
///     without doing anything. That is what keeps the built-in Copy behaving exactly as it always did.
///     <para>
///         <c>AsyncLocal</c> rather than a scoped service because the notification handler is resolved
///         from a different scope than the request, but runs on the same logical call stack.
///     </para>
/// </remarks>
public sealed class TrueCopyOperationAccessor
{
    private static readonly AsyncLocal<TrueCopyOperation?> CurrentOperation = new();

    public TrueCopyOperation? Current => CurrentOperation.Value;

    /// <summary>Starts an operation for the duration of the returned scope.</summary>
    public Scope Begin()
    {
        var operation = new TrueCopyOperation();
        CurrentOperation.Value = operation;
        return new Scope(operation);
    }

    public sealed class Scope : IDisposable
    {
        public Scope(TrueCopyOperation operation) => Operation = operation;

        public TrueCopyOperation Operation { get; }

        public void Dispose() => CurrentOperation.Value = null;
    }
}

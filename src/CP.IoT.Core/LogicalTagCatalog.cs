// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.Core;

/// <summary>Thread-safe in-memory implementation of <see cref="ILogicalTagCatalog"/>.</summary>
public sealed class LogicalTagCatalog : ILogicalTagCatalog, IDisposable
{
    /// <summary>Tag dictionary keyed by name using ordinal comparison.</summary>
    private readonly Dictionary<string, LogicalTag> _tags = new(StringComparer.Ordinal);

    /// <summary>Reader/writer lock protecting <see cref="_tags"/>.</summary>
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    /// <summary>Tracks whether this instance has been disposed.</summary>
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<LogicalTagChangedEventArgs>? Changed;

    /// <inheritdoc/>
    public bool TryAdd(LogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        ThrowIfDisposed();

        var added = false;
        _lock.EnterWriteLock();

        try
        {
            if (!_tags.ContainsKey(tag.Name))
            {
                _tags.Add(tag.Name, tag);
                added = true;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        if (added)
        {
            OnChanged(LogicalTagChangeKind.Added, tag);
        }

        return added;
    }

    /// <inheritdoc/>
    public void Upsert(LogicalTag tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        ThrowIfDisposed();

        LogicalTagChangeKind kind;
        _lock.EnterWriteLock();

        try
        {
            kind = _tags.ContainsKey(tag.Name) ? LogicalTagChangeKind.Updated : LogicalTagChangeKind.Added;
            _tags[tag.Name] = tag;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        OnChanged(kind, tag);
    }

    /// <inheritdoc/>
    public bool TryGet(string name, out LogicalTag? tag)
    {
        _ = LogicalTag.Required(name, nameof(name));
        ThrowIfDisposed();

        _lock.EnterReadLock();

        try
        {
            return _tags.TryGetValue(name, out tag);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public bool TryRemove(string name, out LogicalTag? tag)
    {
        _ = LogicalTag.Required(name, nameof(name));
        ThrowIfDisposed();

        _lock.EnterWriteLock();

        try
        {
            if (!_tags.TryGetValue(name, out tag))
            {
                return false;
            }

            _ = _tags.Remove(name);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        OnChanged(LogicalTagChangeKind.Removed, tag!);
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<LogicalTag> List()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();

        try
        {
            return _tags.Values.OrderBy(static tag => tag.Name, StringComparer.Ordinal).ToArray();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _disposed = true;
    }

    /// <summary>Raises the <see cref="Changed"/> event.</summary>
    /// <param name="kind">The kind of change that occurred.</param>
    /// <param name="tag">The affected logical tag.</param>
    private void OnChanged(LogicalTagChangeKind kind, LogicalTag tag) =>
        Changed?.Invoke(this, new LogicalTagChangedEventArgs(kind, tag));

    /// <summary>Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.</summary>
    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(LogicalTagCatalog));
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Templar.Generators;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(IEnumerable<T> items)
    {
        _items = items.ToArray();
    }

    public int Count => _items?.Length ?? 0;

    public bool Equals(EquatableArray<T> other)
    {
        return AsSpan().SequenceEqual(other.AsSpan());
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in AsSpan())
        {
            hash = unchecked(hash * 31 + item.GetHashCode());
        }
        return hash;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private ReadOnlySpan<T> AsSpan()
    {
        return _items is null ? ReadOnlySpan<T>.Empty : _items.AsSpan();
    }
}

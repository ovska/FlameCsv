﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlameCsv.Tests.Utilities;

internal static class SyncAsyncEnumerable
{
    public static SyncAsyncEnumerable<T> Create<T>(IEnumerable<T> inner) => new(inner);
}

internal sealed class SyncAsyncEnumerable<T>(IEnumerable<T> inner) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new Enumerator(inner.GetEnumerator(), cancellationToken);
    }

    public sealed class Enumerator(IEnumerator<T> inner, CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled<bool>(cancellationToken);

            return new(inner.MoveNext());
        }
    }
}
using System.Collections.ObjectModel;

namespace FlameCsv.Tests;

/// <summary>
/// A collection that disposes its items when disposed.
/// </summary>
public sealed class DisposableCollection : Collection<IDisposable?>, IDisposable
{
    public void Dispose()
    {
        foreach (var item in this)
        {
            try
            {
                item?.Dispose();
            }
            catch
            {
                // ignored
            }
        }
    }
}

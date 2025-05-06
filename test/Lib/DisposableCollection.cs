using System.Collections.ObjectModel;

namespace FlameCsv.Tests;

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

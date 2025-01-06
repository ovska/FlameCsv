using System.Collections.ObjectModel;

namespace FlameCsv.Tests.Utilities;

internal sealed class DisposableCollection : Collection<IDisposable?>, IDisposable
{
    public void Dispose()
    {
        foreach (var item in this)
        {
            item?.Dispose();
        }
    }
}

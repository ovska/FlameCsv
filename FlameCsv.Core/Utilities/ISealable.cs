namespace FlameCsv.Utilities;

internal interface ISealable
{
    bool IsReadOnly { get; }
    bool MakeReadOnly();
}

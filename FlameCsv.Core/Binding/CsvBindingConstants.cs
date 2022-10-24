using System.Reflection;

namespace FlameCsv.Binding;

public static class CsvBindingConstants
{
    /// <summary>
    /// Binding flags used by the library to look for binding attributes in properties and fields.
    /// </summary>
    public const BindingFlags MemberLookupFlags = BindingFlags.Instance | BindingFlags.Public;
}

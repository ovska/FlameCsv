using System.Reflection;

namespace FlameCsv.Binding;

public static class CsvBindingConstants
{
    /// <summary>
    /// Binding flags used to look for properties and fields (public instance members).
    /// </summary>
    public const BindingFlags MemberLookupFlags = BindingFlags.Instance | BindingFlags.Public;
}

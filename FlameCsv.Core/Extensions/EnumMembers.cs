using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace FlameCsv.Extensions;

internal static class EnumMembers<TEnum> where TEnum : struct, Enum
{
    public readonly struct EnumValue
    {
        public TEnum Value { get; init; }
        public string Name { get; init; }
        public string? MemberName { get; init; }

        public void Deconstruct(out TEnum value, out string name, out string? memberName)
        {
            value = Value;
            name = Name;
            memberName = MemberName;
        }
    }

    private static EnumValue[]? _value;

    public static ReadOnlySpan<EnumValue> Value => _value ?? Initialize();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static EnumValue[] Initialize()
    {
        var fields = typeof(TEnum).GetFields();
        var result = Enum.GetNames<TEnum>()
            .Select(name => new EnumValue
            {

                Value = Enum.Parse<TEnum>(name),
                Name = name,
                MemberName = fields.Single(f => f.Name.Equals(name)).GetCustomAttribute<EnumMemberAttribute>()?.Value
            })
            .ToArray();

        return Interlocked.CompareExchange(ref _value, result, null) ?? result;
    }
}

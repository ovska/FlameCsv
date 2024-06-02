using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Text;

/// <summary>
/// Parser factory for non-flags enums.
/// </summary>
internal sealed class EnumTextConverterFactory : CsvConverterFactory<char>
{
    public static EnumTextConverterFactory Instance { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool CanConvert(Type type)
    {
        return type.IsEnum;
    }

    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        return GetParserType(type).CreateInstance<CsvConverter<char>>(options);
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = Messages.StructFactorySuppressionMessage)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumTextConverter<>).MakeGenericType(resultType);
}

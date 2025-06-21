using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Generators;

partial class TypeMapGenerator
{
    public static void WriteConverterType(IndentedTextWriter writer, string token, IMemberModel member)
    {
        if (member.IsIgnored)
        {
            writer.Write($"global::FlameCsv.Binding.CsvIgnored.Converter<{token}, {member.Type.FullyQualifiedName}>");
        }
        else if (member.OverriddenConverter is { IsFactory: false, WrapInNullable: false } converter)
        {
            writer.Write(converter.ConverterType.FullyQualifiedName);
        }
        else
        {
            writer.Write($"global::FlameCsv.CsvConverter<{token}, {member.Type.FullyQualifiedName}>");
        }
    }

    public static void WriteConverter(IndentedTextWriter writer, string token, IMemberModel member)
    {
        if (member.IsIgnored)
        {
            writer.Write($"global::FlameCsv.Binding.CsvIgnored.Converter<{token}, {member.Type.FullyQualifiedName}>()");
            return;
        }

        bool wrapInNullable =
            member.OverriddenConverter?.WrapInNullable ?? member.Type.SpecialType == SpecialType.System_Nullable_T;

        bool builtinConversion = false;

        if (wrapInNullable)
        {
            writer.Write("options.Aot.GetOrCreateNullable<");
            writer.Write(member.Type.FullyQualifiedName.AsSpan()[..^1]); // trim out the nullability question mark
            writer.Write(">(static options => ");
        }

        if (member.OverriddenConverter is not { } converter)
        {
            builtinConversion = TryGetBuiltinConversion(token, member, out string? builtinMethodName);

            if (builtinConversion)
            {
                writer.Write($"options.Aot.GetOrCreate<{member.Type.FullyQualifiedName}>(static options => ");
                writer.Write($"global::FlameCsv.Converters.ConverterCreationExtensions.{builtinMethodName}<");
                writer.Write(member.Type.FullyQualifiedName);
                writer.Write(">(options)");
            }
            else
            {
                writer.Write(
                    member.Type.IsEnumOrNullableEnum ? "options.Aot.GetOrCreateEnum<" : "options.Aot.GetConverter<"
                );

                Range range = member.Type.SpecialType == SpecialType.System_Nullable_T ? (..^1) : (..);
                writer.Write(member.Type.FullyQualifiedName.AsSpan()[range]);

                writer.Write(">()");
            }
        }
        else
        {
            if (converter.IsFactory)
            {
                writer.Write($"(global::FlameCsv.CsvConverter<{token}, {member.Type.FullyQualifiedName}>)");
            }

            writer.Write("new ");
            writer.Write(converter.ConverterType.FullyQualifiedName);
            writer.Write(converter.ConstructorArguments == ConstructorArgumentType.Options ? "(options)" : "()");

            if (converter.IsFactory)
            {
                writer.Write(".Create(typeof(");
                writer.Write(member.Type.FullyQualifiedName);
                writer.Write("), options)");
            }
        }

        if (wrapInNullable || builtinConversion)
        {
            if (member.OverriddenConverter?.WrapInNullable ?? false)
            {
                // explicit converter override, don't cache this one
                writer.Write(", canCache: false");
            }
            else if (member.Type.SpecialType == SpecialType.System_Nullable_T)
            {
                writer.Write(", canCache: true");
            }

            writer.Write(")");
        }
    }

    private static void WriteDefaultInstance(IndentedTextWriter writer, TypeMapModel typeMap)
    {
        writer.WriteLine("/// <summary>");
        writer.WriteLine("/// Returns a thread-safe instance of the typemap with default options.");
        writer.WriteLine("/// </summary>");
        writer.WriteLine(
            $"public static {typeMap.TypeMap.FullyQualifiedName} Default {{ get; }} = new {typeMap.TypeMap.FullyQualifiedName}();"
        );
        writer.WriteLine();
    }

    private static bool TryGetBuiltinConversion(
        string token,
        IMemberModel member,
        [NotNullWhen(true)] out string? methodName
    )
    {
        var status = member.Convertability;

        if (token == "char")
        {
            if ((status & BuiltinConvertable.Both) == BuiltinConvertable.Both)
            {
                methodName = "CreateUtf16";
                return true;
            }

            methodName = null;
            return false;
        }

        if (token != "byte" || (status & BuiltinConvertable.Utf8Any) == 0)
        {
            methodName = null;
            return false;
        }

        bool nativeParse = (status & BuiltinConvertable.Utf8Parsable) == BuiltinConvertable.Utf8Parsable;
        bool nativeFormat = (status & BuiltinConvertable.Utf8Formattable) == BuiltinConvertable.Utf8Formattable;

        methodName = (nativeParse, nativeFormat) switch
        {
            (true, true) => "CreateUtf8",
            (true, false) => "CreateUtf8Parsable",
            (false, true) => "CreateUtf8Formattable",
            (false, false) => "CreateUtf8Transcoded",
        };

        return true;
    }
}

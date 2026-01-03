using FlameCsv.SourceGen.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct EnumValueModel : IComparable<EnumValueModel>
{
    public string Name { get; }
    public string? ExplicitName { get; }
    public BigInteger Value { get; }
    public string DisplayName => ExplicitName ?? Name;

    public EnumValueModel(IFieldSymbol enumValue, bool isUnsigned, List<Diagnostic> diagnostics)
    {
        Name = enumValue.Name;
        Value = isUnsigned
            ? new BigInteger(Convert.ToUInt64(enumValue.ConstantValue))
            : new BigInteger(Convert.ToInt64(enumValue.ConstantValue));

        foreach (var attribute in enumValue.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.Serialization.EnumMemberAttribute")
            {
                continue;
            }

            if (attribute.TryGetNamedArgument("Value", out var value) && value.Value is string explicitName)
            {
                ExplicitName = explicitName;

                if (!HasValidExplicitName)
                {
                    Location? location = (attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax)
                        ?.ArgumentList?.Arguments.FirstOrDefault(a =>
                            a.NameEquals?.Name.Identifier.ValueText == "Value"
                        )
                        ?.GetLocation();

                    diagnostics.Add(
                        Diagnostics.EnumInvalidExplicitName(
                            enumValue.ContainingSymbol,
                            enumValue,
                            location ?? attribute.GetLocation(),
                            explicitName,
                            explicitName.Length == 0
                                ? "value must not be empty"
                                : "value must not start with a digit, plus, or minus"
                        )
                    );
                }
            }

            break;
        }
    }

    public int CompareTo(EnumValueModel other)
    {
        int cmp = Value.CompareTo(other.Value);
        if (cmp == 0)
            cmp = string.CompareOrdinal(Name, other.Name); // fields cannot have the same name
        return cmp;
    }

    public bool HasValidExplicitName => ExplicitName is { Length: > 0 } value && !value[0].IsAsciiNumeric();
};

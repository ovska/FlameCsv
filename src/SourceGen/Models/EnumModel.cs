using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal sealed record EnumModel
{
    public static bool TryGet(
        ISymbol converterSymbol,
        AttributeData attributeData,
        CancellationToken cancellationToken,
        out Diagnostic[] diagnostics,
        [NotNullWhen(true)] out EnumModel? model
    )
    {
        List<Diagnostic> diagList = PooledList<Diagnostic>.Acquire();

        if (
            converterSymbol is INamedTypeSymbol converterType
            && attributeData.AttributeClass
                is {
                    TypeArguments: [
                        { } tokenType,
                        INamedTypeSymbol { CanBeReferencedByName: true, EnumUnderlyingType: not null } enumType,
                    ]
                }
        )
        {
            if (tokenType.SpecialType is not (SpecialType.System_Byte or SpecialType.System_Char))
            {
                diagList.Add(Diagnostics.EnumUnsupportedToken(converterSymbol, attributeData, tokenType));
            }

            Diagnostics.EnsurePartial(converterType, cancellationToken, diagList);
            Diagnostics.CheckIfFileScoped(converterType, cancellationToken, diagList);
            Diagnostics.CheckIfFileScoped(enumType, cancellationToken, diagList);

            model = new EnumModel(
                enumType: enumType,
                isByte: tokenType.SpecialType == SpecialType.System_Byte,
                converterType: converterType,
                cancellationToken,
                diagList
            );

            if (diagList.Count == 0)
            {
                diagnostics = [];
                PooledList<Diagnostic>.Release(diagList);
                return true;
            }
        }

        model = null;
        diagnostics = [.. diagList];
        PooledList<Diagnostic>.Release(diagList);
        return false;
    }

    public bool IsByte { get; }
    public string Token => IsByte ? "byte" : "char";

    public string ConverterTypeName { get; }
    public TypeRef EnumType { get; }
    public TypeRef UnderlyingType { get; }
    public EquatableArray<BigInteger> UniqueValues { get; }
    public EquatableArray<EnumValueModel> Values { get; }
    public bool HasFlagsAttribute { get; }
    public bool ContiguousFromZero { get; }
    public int ContiguousFromZeroCount { get; }
    public EquatableArray<NestedType> WrappingTypes { get; }
    public bool HasExplicitNames { get; }
    public bool HasNegativeValues { get; }

    /// <summary>
    /// Whether the typemap is in the global namespace.
    /// </summary>
    public bool InGlobalNamespace { get; }

    /// <summary>
    /// Namespace of the typemap.
    /// </summary>
    /// <seealso cref="InGlobalNamespace"/>
    public string Namespace { get; }

    private EnumModel(
        INamedTypeSymbol enumType,
        bool isByte,
        ITypeSymbol converterType,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics
    )
    {
        IsByte = isByte;
        ConverterTypeName = converterType.Name;
        EnumType = new TypeRef(enumType);
        UnderlyingType = new TypeRef(enumType.EnumUnderlyingType!);

        HasFlagsAttribute = enumType.GetAttributes().Any(a => a is { AttributeClass.Name: "FlagsAttribute" });

        List<EnumValueModel> values = PooledList<EnumValueModel>.Acquire();
        HashSet<BigInteger> uniqueValues = PooledSet<BigInteger>.Acquire();

        bool isUnsigned =
            enumType.EnumUnderlyingType?.SpecialType
                is SpecialType.System_Byte
                    or SpecialType.System_UInt16
                    or SpecialType.System_UInt32
                    or SpecialType.System_UInt64
                    or SpecialType.System_UIntPtr;

        bool hasExplicitNames = false;

        // loop over the enum's values
        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } enumValue)
            {
                var value = new EnumValueModel(enumValue, isUnsigned, diagnostics);
                values.Add(value);
                uniqueValues.Add(value.Value);

                hasExplicitNames |= value.HasValidExplicitName;
            }
        }

        values.Sort();
        Values = values.ToEquatableArrayAndFree();
        UniqueValues = uniqueValues.ToEquatableArrayAndFree();
        Array.Sort(UniqueValues.UnsafeArray!);

        int contiguousCount = 0;
        bool isContiguous = true;

        foreach (ref readonly var value in Values)
        {
            if (value.Value != contiguousCount)
            {
                isContiguous = false;
                break;
            }

            contiguousCount++;
        }

        ContiguousFromZero = isContiguous;
        ContiguousFromZeroCount = contiguousCount;

        WrappingTypes = NestedType.Create(converterType, cancellationToken, diagnostics);
        InGlobalNamespace = converterType.ContainingNamespace.IsGlobalNamespace;
        Namespace = converterType.ContainingNamespace.ToDisplayString();
        HasExplicitNames = Values.AsImmutableArray().Any(v => !string.IsNullOrEmpty(v.ExplicitName));
        HasNegativeValues = UniqueValues.AsImmutableArray().Any(v => v < BigInteger.Zero);

        if (HasExplicitNames)
        {
            CheckExplicitNameDuplicates(enumType, diagnostics);
        }

        if (HasFlagsAttribute)
        {
            CheckFlags(enumType, diagnostics);
        }
    }

    private void CheckExplicitNameDuplicates(INamedTypeSymbol enumType, List<Diagnostic> diagnostics)
    {
        HashSet<string>? handled = null; // report only one diagnostic per duplicate name

        foreach (ref readonly var value in Values)
        {
            if (!value.HasValidExplicitName)
            {
                // If the explicit name is null or invalid, we don't need to check for duplicates
                continue;
            }

            foreach (ref readonly var innerValue in Values)
            {
                if (value == innerValue)
                {
                    continue;
                }

                if (value.ExplicitName == innerValue.Name || value.ExplicitName == innerValue.ExplicitName)
                {
                    if (!(handled ??= PooledSet<string>.Acquire()).Add(value.ExplicitName!))
                    {
                        // Already handled this explicit name
                        continue;
                    }

                    string invalidName = value.Name;
                    Location? location = GetMemberLocation(enumType, invalidName);

                    diagnostics.Add(
                        Diagnostics.EnumDuplicateName(
                            enumType,
                            value.Name,
                            location ?? enumType.Locations.FirstOrDefault(),
                            value.ExplicitName!
                        )
                    );
                }
            }
        }

        PooledSet<string>.Release(handled);
    }

    private void CheckFlags(INamedTypeSymbol enumType, List<Diagnostic> diagnostics)
    {
        ulong loneBits = 0;

        foreach (ref readonly var value in Values)
        {
            var uint64 = value.Value.AsUInt64Bits();

            if (HasOnlyOneBitSet(uint64))
            {
                loneBits |= uint64;
            }
        }

        foreach (ref readonly var value in Values)
        {
            var uint64 = value.Value.AsUInt64Bits();

            if (uint64 != 0 && !HasOnlyOneBitSet(uint64) && (uint64 & ~loneBits) != 0)
            {
                // This value has a bit set that is also set in another value
                diagnostics.Add(
                    Diagnostics.EnumUnsupportedFlag(enumType, value.Name, GetMemberLocation(enumType, value.Name))
                );
            }
        }

        static bool HasOnlyOneBitSet(ulong value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }
    }

    private static Location? GetMemberLocation(INamedTypeSymbol enumType, string invalidName)
    {
        return enumType
            .GetMembers()
            .FirstOrDefault(m => m is IFieldSymbol f && f.Name == invalidName)
            ?.DeclaringSyntaxReferences.FirstOrDefault()
            ?.GetSyntax()
            .GetLocation();
    }
}

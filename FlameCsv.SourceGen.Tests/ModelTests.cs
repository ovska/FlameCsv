using FlameCsv.Attributes;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen.Tests;

public static class ModelTests
{
    private static MetadataReference CoreAssembly { get; } =
        MetadataReference.CreateFromFile(typeof(CsvTypeMapAttribute<,>).Assembly.Location);

    [Fact]
    public static void Test_AnalysisCollector()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_AnalysisCollector),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    class First;
                    class Second;
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var first = compilation.GetTypeByMetadataName("First")!;
        var second = compilation.GetTypeByMetadataName("Second")!;
        var objType = compilation.GetTypeByMetadataName("System.Object")!;

        AnalysisCollector collector = new(objType);

        collector.AddProxy(first, null);
        collector.AddProxy(second, null);

        collector.Free(out var diagnostics, out var ignoredHeaders, out var proxy);

        Assert.Equal([Descriptors.MultipleTypeProxies.Id], diagnostics.Select(d => d.Id));
        Assert.Empty(ignoredHeaders);
        Assert.Null(proxy);
    }

    [Fact]
    public static void Test_TypeRef()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_TypeRef),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    enum TestEnum { A, B, C }

                    class TestClass : AbstractClass
                    {
                        public int Id { get; set; }
                        public string? Name { get; set; }
                    }

                    abstract class AbstractClass { }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var enumType = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<EnumDeclarationSyntax>()
                .Single(),
            cancellationToken: TestContext.Current.CancellationToken)!;
        var enumRef = new TypeRef(enumType);
        Assert.Equal(enumRef, new TypeRef(enumType));
        Assert.True(enumRef.IsEnumOrNullableEnum);
        Assert.True(enumRef.IsValueType);
        Assert.False(enumRef.IsAbstract);
        Assert.Equal("global::TestEnum", enumRef.FullyQualifiedName);

        var nullableEnum = semanticModel.Compilation.GetTypeByMetadataName("System.Nullable`1")!.Construct(enumType);
        var nullableEnumRef = new TypeRef(nullableEnum);
        Assert.Equal(nullableEnumRef, new TypeRef(nullableEnum));
        Assert.True(nullableEnumRef.IsEnumOrNullableEnum);

        var classType = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(c => c.Identifier.Text == "TestClass"),
            cancellationToken: TestContext.Current.CancellationToken)!;
        var classRef = new TypeRef(classType);
        Assert.Equal(classRef, new TypeRef(classType));
        Assert.False(classRef.IsEnumOrNullableEnum);
        Assert.False(classRef.IsValueType);
        Assert.False(classRef.IsAbstract);
        Assert.Equal("global::TestClass", new TypeRef(classType).FullyQualifiedName);

        Assert.NotEqual(new TypeRef(enumType), new TypeRef(classType));

        var baseType = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(c => c.Identifier.Text == "AbstractClass"),
            cancellationToken: TestContext.Current.CancellationToken)!;
        var baseRef = new TypeRef(baseType);
        Assert.Equal(baseRef, new TypeRef(baseType));
        Assert.True(baseRef.IsAbstract);
    }

    [Fact]
    public static void Test_ParameterModel()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_ParameterModel),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using FlameCsv.Attributes;

                    void Func(
                        [CsvHeader("A", "_a")] int a,
                        [CsvOrder(1)] float b,
                        in long c,
                        ref string d,
                        bool b = true) { }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var method = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<LocalFunctionStatementSyntax>()
                .Single(),
            cancellationToken: TestContext.Current.CancellationToken)!;

        // get token symbol for System.Char
        var charSymbol = compilation.GetTypeByMetadataName("System.Char")!;
        var flameSymbols = GetFlameSymbols(compilation, charSymbol);
        AnalysisCollector collector = new(charSymbol);

        var parameters = ParameterModel.Create(
            charSymbol,
            charSymbol,
            method,
            in flameSymbols,
            ref collector);

        Assert.Equal([Descriptors.RefConstructorParameter.Id], collector.Diagnostics.Select(d => d.Id));

        (string name, bool hasDefaultValue, object? defaultValue, RefKind refKind, string[] aliases, int? order)[]
            expected
                =
                [
                    ("a", false, null, RefKind.None, ["_a"], null),
                    ("b", false, null, RefKind.None, [], 1),
                    ("c", false, null, RefKind.In, [], null),
                    ("d", false, null, RefKind.Ref, [], null),
                    ("b", true, true, RefKind.None, [], null),
                ];

        for (int i = 0; i < parameters.Length; i++)
        {
            Assert.Equal(i, parameters[i].ParameterIndex);
            Assert.Equal(expected[i].name, parameters[i].HeaderName);
            Assert.Equal(expected[i].hasDefaultValue, parameters[i].HasDefaultValue);
            Assert.Equal(expected[i].defaultValue, parameters[i].DefaultValue);
            Assert.Equal(expected[i].refKind, parameters[i].RefKind);
            Assert.Equal(expected[i].aliases.ToEquatableArray(), parameters[i].Aliases);
            Assert.Equal(expected[i].order, parameters[i].Order);
        }

        // equality
        Assert.Equal(
            parameters,
            ParameterModel.Create(
                charSymbol,
                charSymbol,
                method,
                in flameSymbols,
                ref collector));

        collector.Free(out _, out _, out _);
    }

    [Fact]
    public static void Test_PropertyModel()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_ParameterModel),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    interface ISomething
                    {
                        object? Explicit { get; set; }
                    }

                    class TestClass : ISomething
                    {
                        public int this[int index] => index;
                        public int Id { get; set; }
                        public required int Required { get; set; }
                        public ref int RefProperty { get => ref StaticField; set { } }
                        public bool GetOnly { get; }
                        public bool SetOnly { set { } }
                        public bool InitOnly { get; init; }
                        public const int ConstField = 1;
                        public static int StaticField = 2;
                        object? ISomething.Explicit { get; set; }
                        public int Field;
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var charSymbol = compilation.GetTypeByMetadataName("System.Char")!;

        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(),
            cancellationToken: TestContext.Current.CancellationToken)!;

        var flameSymbols = GetFlameSymbols(compilation, classSymbol);
        AnalysisCollector collector = new(classSymbol);

        var models = GetProperties(in flameSymbols, ref collector);
        Assert.Equal(models, GetProperties(in flameSymbols, ref collector));

        // @formatter:off
        (string name, bool canRead, bool canWrite, bool isRequired, bool isExplicit, bool isProperty)[] expected =
        [
            (name: "Id", canRead: true, canWrite: true, isRequired: false, isExplicit: false, isProperty: true),
            (name: "Required", canRead: true, canWrite: true, isRequired: true, isExplicit: false, isProperty: true),
            (name: "GetOnly", canRead: false, canWrite: true, isRequired: false, isExplicit: false, isProperty: true),
            (name: "SetOnly", canRead: true, canWrite: false, isRequired: false, isExplicit: false, isProperty: true),
            (name: "InitOnly", canRead: true, canWrite: true, isRequired: true, isExplicit: false, isProperty: true),
            (name: "ISomething_Explicit", canRead: true, canWrite: true, isRequired: false, isExplicit: true, isProperty: true),
            (name: "Field", canRead: true, canWrite: true, isRequired: false, isExplicit: false, isProperty: false),
        ];
        // @formatter:on

        for (int i = 0; i < models.Length; i++)
        {
            Assert.Equal(expected[i].name, models[i].Identifier);
            Assert.Equal(expected[i].canRead, models[i].CanRead);
            Assert.Equal(expected[i].canWrite, models[i].CanWrite);
            Assert.Equal(expected[i].isRequired, models[i].IsRequired);
            Assert.Equal(expected[i].isExplicit, models[i].ExplicitInterfaceOriginalDefinitionName is not null);
            Assert.Equal(expected[i].isProperty, models[i].IsProperty);

            if (expected[i].isExplicit)
            {
                Assert.Equal("global::ISomething", models[i].ExplicitInterfaceOriginalDefinitionName);
            }
        }

        collector.Free(out _, out _, out _);

        EquatableArray<PropertyModel> GetProperties(in FlameSymbols symbols, ref AnalysisCollector collector)
        {
            List<PropertyModel> list = [];

            foreach (var member in classSymbol.GetMembers().Where(m => !m.IsStatic))
            {
                PropertyModel? model = member switch
                {
                    IPropertySymbol propertySymbol => PropertyModel.TryCreate(
                        charSymbol,
                        propertySymbol,
                        in symbols,
                        ref collector),
                    IFieldSymbol fieldSymbol => PropertyModel.TryCreate(
                        charSymbol,
                        fieldSymbol,
                        in symbols,
                        ref collector),
                    _ => null
                };

                if (model is not null)
                {
                    list.Add(model);
                }
            }

            return list.ToEquatableArray();
        }
    }

    [Fact]
    public static void Test_ConverterModel()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_TypeRef),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using System;
                    using FlameCsv;
                    using FlameCsv.Attributes;

                    class TestClass
                    {
                        [CsvConverterAttribute<EmptyCtor>] public object Empty { get; set; }
                        [CsvConverterAttribute<OptionsCtor>] public object Options { get; set; }
                        [CsvConverterAttribute<InvalidCtor>] public object Invalid { get; set; }
                        [CsvConverterAttribute<Factory>] public object Factory { get; set; }
                        [CsvConverterAttribute<NotConstructible>] public object IsAbstract { get; set; }
                        [CsvConverterAttribute<IntConverter>] public int? Wrappable { get; set; }
                        public object None { get; set; }
                    }

                    class OptionsCtor(CsvOptions<char> _) : CsvConverter<char, object>
                    {
                        public override bool TryFormat(Span<char> destination, object value, out int charsWritten) => throw null;
                        public override bool TryParse(ReadOnlySpan<char> source, out object value) => throw null;
                    }

                    class EmptyCtor : CsvConverter<char, object>
                    {
                        public override bool TryFormat(Span<char> destination, object value, out int charsWritten) => throw null;
                        public override bool TryParse(ReadOnlySpan<char> source, out object value) => throw null;
                    }

                    class InvalidCtor(object _) : CsvConverter<char, object>
                    {
                        public override bool TryFormat(Span<char> destination, object value, out int charsWritten) => throw null;
                        public override bool TryParse(ReadOnlySpan<char> source, out object value) => throw null;
                    }

                    class Factory : CsvConverterFactory<char>
                    {
                        public override bool CanConvert(Type type) => throw null;
                        public override CsvConverter<char> Create(Type type, CsvOptions<char> options) => throw null;
                    }

                    class IntConverter : CsvConverter<char, int>
                    {
                        public override bool TryFormat(Span<char> destination, int value, out int charsWritten) => throw null;
                        public override bool TryParse(ReadOnlySpan<char> source, out int value) => throw null;
                    }

                    abstract class NotConstructible : CsvConverterFactory<char>
                    {
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var charSymbol = compilation.GetTypeByMetadataName("System.Char")!;
        var objectSymbol = compilation.GetTypeByMetadataName("System.Object")!;
        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(s => s.Identifier.Text == "TestClass"),
            cancellationToken: TestContext.Current.CancellationToken)!;

        var flameSymbols = GetFlameSymbols(compilation, classSymbol);
        AnalysisCollector collector = new(classSymbol);

        List<ConverterModel> models = [];

        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var model = ConverterModel.Create(charSymbol, member, objectSymbol, in flameSymbols, ref collector);

            if (member.Name == "None")
            {
                Assert.Null(model);
                continue;
            }

            Assert.NotNull(model);
            models.Add(model);
        }

        Assert.Equal(6, models.Count);

        (ConstructorArgumentType argType, bool isFactory, bool isAbstract, bool wrap)[] expected =
        [
            (argType: ConstructorArgumentType.Empty, isFactory: false, isAbstract: false, wrap: false),
            (argType: ConstructorArgumentType.Options, isFactory: false, isAbstract: false, wrap: false),
            (argType: ConstructorArgumentType.Invalid, isFactory: false, isAbstract: false, wrap: false),
            (argType: ConstructorArgumentType.Empty, isFactory: true, isAbstract: false, wrap: false),
            (argType: ConstructorArgumentType.Empty, isFactory: true, isAbstract: true, wrap: false),
            (argType: ConstructorArgumentType.Empty, isFactory: false, isAbstract: false, wrap: true),
        ];

        for (int i = 0; i < models.Count; i++)
        {
            Assert.Equal(expected[i].argType, models[i].ConstructorArguments);
            Assert.Equal(expected[i].isFactory, models[i].IsFactory);
        }

        // need to do this here as we jam more diagnostics into the collector in the equality check below
        Assert.Equal(
            [Descriptors.NoCsvFactoryConstructor.Id, Descriptors.CsvConverterAbstract.Id],
            collector.Diagnostics.Select(d => d.Id));

        // equality
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            Assert.Equal(
                ConverterModel.Create(charSymbol, member, objectSymbol, in flameSymbols, ref collector),
                ConverterModel.Create(charSymbol, member, objectSymbol, in flameSymbols, ref collector));
        }

        collector.Free(out _, out _, out _);
    }

    [Fact]
    public static void Test_AssemblyModel()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_AssemblyModel),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using System;
                    using FlameCsv;
                    using FlameCsv.Binding;
                    using FlameCsv.Attributes;

                    [assembly: CsvHeaderAttribute("_prop", "prop_", MemberName = "Prop", TargetType = typeof(AssemblyTest.Target))]
                    [assembly: CsvIgnoredIndexesAttribute(new int[] { 1 }, TargetType = typeof(AssemblyTest.Target))]
                    [assembly: CsvTypeProxyAttribute(typeof(object), TargetType = typeof(AssemblyTest.Target))]
                    [assembly: CsvIgnoredIndexesAttribute(IgnoredIndexes = new int[] { 1 }, TargetType = typeof(object))]

                    namespace AssemblyTest
                    {
                        class Target
                        {
                            public int Prop { get; set; }
                        }
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(s => s.Identifier.Text == "Target"),
            cancellationToken: TestContext.Current.CancellationToken)!;
        Assert.NotNull(classSymbol);

        var flameSymbols = GetFlameSymbols(compilation, classSymbol);
        AnalysisCollector collector = new(classSymbol);
        ConstructorModel? ctorModel = null;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            var model = AttributeConfiguration.TryCreate(
                classSymbol,
                isOnAssembly: true,
                attr,
                in flameSymbols,
                ref collector);

            if (model is not null)
            {
                collector.TargetAttributes.Add(model.Value);
            }
            else
            {
                ctorModel ??= ConstructorModel.TryParseConstructorAttribute(
                    isOnAssembly: true,
                    classSymbol,
                    attr,
                    in flameSymbols);
            }
        }

        Assert.Single(collector.TargetAttributes.WrittenSpan.ToArray());
        Assert.Equal("Prop", collector.TargetAttributes.WrittenSpan[0].MemberName);
        Assert.Equal("_prop", collector.TargetAttributes.WrittenSpan[0].HeaderName);
        Assert.Equal(["prop_"], collector.TargetAttributes.WrittenSpan[0].Aliases.Select(x => x.Value?.ToString()));
        Assert.NotNull(collector.TargetAttributes.WrittenSpan[0].Attribute.GetLocation());

        Assert.Single(collector.IgnoredIndexes);
        Assert.Equal(1, collector.IgnoredIndexes.Single());

        Assert.Single(collector.Proxies);
        Assert.Equal(SpecialType.System_Object, collector.Proxies[0].SpecialType);

        collector.Free(out _, out _, out _);
    }

    [Fact]
    public static void Test_NestedType()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_NestedType),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    partial class TestClass
                    {
                        partial abstract class Nested1
                        {
                            partial struct Nested2
                            {
                                partial ref struct Nested3
                                {
                                    partial readonly struct Nested4
                                    {
                                        partial class Target;
                                    }
                                }
                            }
                        }
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var nestedSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Single(s => s.Identifier.Text == "Target"),
            cancellationToken: TestContext.Current.CancellationToken)!;

        var actual = NestedType.Parse(nestedSymbol, CancellationToken.None, []);

        EquatableArray<NestedType> expected =
        [
            new()
            {
                IsReadOnly = false,
                IsRefLikeType = false,
                IsAbstract = false,
                IsValueType = false,
                Name = "TestClass"
            },
            new()
            {
                IsReadOnly = false,
                IsRefLikeType = false,
                IsAbstract = true,
                IsValueType = false,
                Name = "Nested1"
            },
            new()
            {
                IsReadOnly = false,
                IsRefLikeType = false,
                IsAbstract = false,
                IsValueType = true,
                Name = "Nested2"
            },
            new()
            {
                IsReadOnly = false,
                IsRefLikeType = true,
                IsAbstract = false,
                IsValueType = true,
                Name = "Nested3"
            },
            new()
            {
                IsReadOnly = true,
                IsRefLikeType = false,
                IsAbstract = false,
                IsValueType = true,
                Name = "Nested4"
            },
        ];

        Assert.Equal(expected, actual);
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Test_Convertability(bool charToken)
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_NestedType),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using System;

                    class Target
                    {
                        public Guid Native1 { get; set; }
                        public TimeSpan Native2 { get; set; }
                        public DateTimeOffset Native3 { get; set; }
                        public int Native4 { get; set; }
                        public object None1 { get; set; }
                        public Neither None2 { get; set; }
                        public Utf8Both Both8 { get; set; }
                        public Utf8Format Format8 { get; set; }
                        public Utf8Parse Parse8 { get; set; }
                        public Utf16Both Both16 { get; set; }
                    }

                    abstract class Neither;
                    abstract class Utf8Both : IUtf8SpanFormattable, IUtf8SpanParsable<Utf8Both>;
                    abstract class Utf8Format : IUtf8SpanFormattable, ISpanParsable<Utf8Format>;
                    abstract class Utf8Parse : IUtf8SpanParsable<Utf8Parse>, ISpanFormattable;
                    abstract class Utf16Both : ISpanParsable<Utf16Both>, ISpanFormattable;
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Single(s => s.Identifier.Text == "Target"),
            cancellationToken: TestContext.Current.CancellationToken)!;

        var symbols = GetFlameSymbols(compilation, classSymbol, isChar: charToken);
        AnalysisCollector collector = new(classSymbol);
        List<PropertyModel> models = [];

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var model = PropertyModel.TryCreate(symbols.TokenType, property, in symbols, ref collector);
            Assert.NotNull(model);
            models.Add(model);
        }

        IEnumerable<(string nameof, BuiltinConvertable status)> expected =
        [
            ("Native1", BuiltinConvertable.Native),
            ("Native2", BuiltinConvertable.Native),
            ("Native3", BuiltinConvertable.Native),
            ("Native4", BuiltinConvertable.Native),
            ("None1", BuiltinConvertable.None),
            ("None2", BuiltinConvertable.None),
        ];

        if (charToken)
        {
            expected =
            [
                ..expected,
                ("Both8", BuiltinConvertable.None),
                ("Format8", BuiltinConvertable.Parsable),
                ("Parse8", BuiltinConvertable.Formattable),
                ("Both16", BuiltinConvertable.Both)
            ];
        }
        else
        {
            expected =
            [
                ..expected,
                ("Both8", BuiltinConvertable.Utf8Both),
                ("Format8", BuiltinConvertable.Utf8Formattable | BuiltinConvertable.Parsable),
                ("Parse8", BuiltinConvertable.Utf8Parsable | BuiltinConvertable.Formattable),
                ("Both16", BuiltinConvertable.Both)
            ];
        }

        Assert.Equal(expected, models.Select(m => (m.Identifier, m.Convertability)));
    }

    // ReSharper disable once UnusedParameter.Local
    private static FlameSymbols GetFlameSymbols(Compilation compilation, ITypeSymbol arg, bool isChar = true)
    {
        return new FlameSymbols(
#if SOURCEGEN_USE_COMPILATION
            compilation,
#endif
            tokenType: compilation.GetTypeByMetadataName(isChar ? "System.Char" : "System.Byte")!,
            arg);
    }
}

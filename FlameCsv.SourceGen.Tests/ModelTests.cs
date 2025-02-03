using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen.Tests;

public static class ModelTests
{
    private static MetadataReference CoreAssembly { get; } =
        MetadataReference.CreateFromFile(typeof(Binding.CsvTypeMapAttribute<,>).Assembly.Location);

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
                    """)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var enumType = semanticModel.GetDeclaredSymbol(
            semanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Single())!;
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
                .SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(c => c.Identifier.Text == "TestClass"))!;
        var classRef = new TypeRef(classType);
        Assert.Equal(classRef, new TypeRef(classType));
        Assert.False(classRef.IsEnumOrNullableEnum);
        Assert.False(classRef.IsValueType);
        Assert.False(classRef.IsAbstract);
        Assert.Equal("global::TestClass", new TypeRef(classType).FullyQualifiedName);

        Assert.NotEqual(new TypeRef(enumType), new TypeRef(classType));

        var baseType = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(c => c.Identifier.Text == "AbstractClass"))!;
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
                    using FlameCsv.Binding.Attributes;

                    void Func(
                        [CsvField("A")] int a,
                        [CsvField(Order = 1)] float b,
                        in long c,
                        ref string d,
                        bool b = true) { }
                    """)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var method = semanticModel.GetDeclaredSymbol(
            semanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single())!;

        // get token symbol for System.Char
        var charSymbol = compilation.GetTypeByMetadataName("System.Char")!;
        var flameSymbols = new FlameSymbols(compilation);
        List<Diagnostic>? diagnostics = null;

        var parameters = ParameterModel.Create(charSymbol, method.Parameters, in flameSymbols, ref diagnostics);
        Assert.Equal(
            parameters,
            ParameterModel.Create(charSymbol, method.Parameters, in flameSymbols, ref diagnostics));
        Assert.Null(diagnostics);

        (string name, bool hasDefaultValue, object? defaultValue, RefKind refKind, string[] names, int order)[] expected
            =
            [
                ("a", false, null, RefKind.None, ["A"], 0),
                ("b", false, null, RefKind.None, [], 1),
                ("c", false, null, RefKind.In, [], 0),
                ("d", false, null, RefKind.Ref, [], 0),
                ("b", true, true, RefKind.None, [], 0),
            ];

        for (int i = 0; i < parameters.Length; i++)
        {
            Assert.Equal(i, parameters[i].ParameterIndex);
            Assert.Equal(expected[i].name, parameters[i].Identifier);
            Assert.Equal(expected[i].hasDefaultValue, parameters[i].HasDefaultValue);
            Assert.Equal(expected[i].defaultValue, parameters[i].DefaultValue);
            Assert.Equal(expected[i].refKind, parameters[i].RefKind);
            Assert.Equal(expected[i].names.ToEquatableArray(), parameters[i].Names);
            Assert.Equal(expected[i].order, parameters[i].Order);
        }
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
                        public ref int RefProperty { get => ref StaticField; set { } }
                        public bool GetOnly { get; }
                        public bool SetOnly { set { } }
                        public bool InitOnly { get; init; }
                        public const int ConstField = 1;
                        public static int StaticField = 2;
                        object? ISomething.Explicit { get; set; }
                        public int Field;
                    }
                    """)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var charSymbol = compilation.GetTypeByMetadataName("System.Char")!;

        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single())!;

        var flameSymbols = new FlameSymbols(compilation);

        var models = GetProperties(in flameSymbols);
        Assert.Equal(models, GetProperties(in flameSymbols));

        // @formatter:off
        (string name, bool canRead, bool canWrite, bool isRequired, bool isExplicit, bool isProperty)[] expected=
        [
            (name: "Id", canRead: true, canWrite: true, isRequired: false, isExplicit: false, isProperty: true),
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

        EquatableArray<PropertyModel> GetProperties(in FlameSymbols symbols)
        {
            List<PropertyModel> list = [];
            List<Diagnostic>? diagnostics = [];

            foreach (var member in classSymbol.GetMembers().Where(m => !m.IsStatic))
            {
                PropertyModel? model = member switch
                {
                    IPropertySymbol propertySymbol => PropertyModel.TryCreate(
                        charSymbol,
                        propertySymbol,
                        in symbols,
                        CancellationToken.None,
                        ref diagnostics),
                    IFieldSymbol fieldSymbol => PropertyModel.TryCreate(charSymbol, fieldSymbol, in symbols),
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
                    using FlameCsv.Binding.Attributes;

                    class TestClass
                    {
                        [CsvConverterAttribute<char, EmptyCtor>] public object Empty { get; set; }
                        [CsvConverterAttribute<char, OptionsCtor>] public object Options { get; set; }
                        [CsvConverterAttribute<char, InvalidCtor>] public object Invalid { get; set; }
                        [CsvConverterAttribute<char, Factory>] public object Factory { get; set; }
                        [CsvConverterAttribute<char, NotConstructible>] public object IsAbstract { get; set; }
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

                    abstract class NotConstructible : CsvConverterFactory<char>
                    {
                    }
                    """)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var charSymbol = compilation.GetTypeByMetadataName("System.Char")!;
        var objectSymbol = compilation.GetTypeByMetadataName("System.Object")!;
        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(s => s.Identifier.Text == "TestClass"))!;

        var flameSymbols = new FlameSymbols(compilation);

        List<ConverterModel> models = [];

        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var model = ConverterModel.Create(charSymbol, member, objectSymbol, in flameSymbols);

            if (member.Name == "None")
            {
                Assert.Null(model);
                continue;
            }

            Assert.NotNull(model);
            Assert.Equal(model, ConverterModel.Create(charSymbol, member, objectSymbol, in flameSymbols));
            models.Add(model);
        }

        Assert.Equal(5, models.Count);

        (ConstructorArgumentType argType, bool isFactory, bool isAbstract)[] expected =
        [
            (argType: ConstructorArgumentType.Empty, isFactory: false, isAbstract: false),
            (argType: ConstructorArgumentType.Options, isFactory: false, isAbstract: false),
            (argType: ConstructorArgumentType.Invalid, isFactory: false, isAbstract: false),
            (argType: ConstructorArgumentType.Empty, isFactory: true, isAbstract: false),
            (argType: ConstructorArgumentType.Empty, isFactory: true, isAbstract: true),
        ];

        for (int i = 0; i < models.Count; i++)
        {
            Assert.Equal(expected[i].argType, models[i].ConstructorArguments);
            Assert.Equal(expected[i].isFactory, models[i].IsFactory);

            List<Diagnostic>? diagnostics = null;

            models[i].TryAddDiagnostics(target: classSymbol, tokenType: charSymbol, ref diagnostics);

            if (models[i].ConstructorArguments == ConstructorArgumentType.Invalid)
            {
                Assert.NotNull(diagnostics);
                Assert.Single(diagnostics);
                Assert.Equal("FLAMESG202", diagnostics[0].Id);
            }
            else if (models[i].ConverterType.IsAbstract)
            {
                Assert.NotNull(diagnostics);
                Assert.Single(diagnostics);
                Assert.Equal("FLAMESG203", diagnostics[0].Id);
            }
            else
            {
                Assert.Null(diagnostics);
            }
        }
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
                    using FlameCsv.Binding.Attributes;

                    [assembly: FlameCsv.Binding.Attributes.CsvAssemblyTypeFieldAttribute(typeof(AssemblyTest.Target), "Prop", "_prop")]
                    [assembly: FlameCsv.Binding.Attributes.CsvAssemblyTypeAttribute(typeof(AssemblyTest.Target), IgnoredHeaders = new string[] { "value" })]
                    [assembly: FlameCsv.Binding.Attributes.CsvAssemblyTypeAttribute(typeof(AssemblyTest.Target), CreatedTypeProxy = typeof(object))]
                    [assembly: FlameCsv.Binding.Attributes.CsvAssemblyTypeAttribute(typeof(object), IgnoredHeaders = new string[] { "test" })]

                    namespace AssemblyTest
                    {
                        class Target
                        {
                            public int Prop { get; set; }
                        }
                    }
                    """)
            ],
            [CoreAssembly, Basic.Reference.Assemblies.Net90.References.SystemRuntime],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        var classSymbol = semanticModel.GetDeclaredSymbol(
            semanticModel
                .SyntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(s => s.Identifier.Text == "Target"))!;
        Assert.NotNull(classSymbol);

        var flameSymbols = new FlameSymbols(compilation);

        List<TargetAttributeModel>? targetAttributeModels = null;
        List<string>? ignoredHeaders = null;
        List<ProxyData>? proxies = null;

        AssemblyReader.Read(
            classSymbol,
            compilation.Assembly,
            ref flameSymbols,
            CancellationToken.None,
            ref targetAttributeModels,
            ref ignoredHeaders,
            ref proxies);

        Assert.NotNull(targetAttributeModels);
        Assert.NotNull(ignoredHeaders);
        Assert.NotNull(proxies);

        Assert.Single(targetAttributeModels);
        Assert.Equal("Prop", targetAttributeModels[0].MemberName);
        Assert.Equal(["_prop"], targetAttributeModels[0].Names);

        Assert.Single(ignoredHeaders);
        Assert.Equal("value", ignoredHeaders[0]);

        Assert.Single(proxies);
        Assert.Equal("object", proxies[0].Type.FullyQualifiedName);

        Assert.Equal(targetAttributeModels.ToEquatableArray(), targetAttributeModels.ToEquatableArray());
    }
}

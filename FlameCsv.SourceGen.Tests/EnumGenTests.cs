using Basic.Reference.Assemblies;
using FlameCsv.Attributes;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Tests;

public static class EnumGenTests
{
    private static MetadataReference CoreAssembly { get; } =
        MetadataReference.CreateFromFile(typeof(CsvTypeMapAttribute<,>).Assembly.Location);

    [Fact]
    public static void Test_EnumGenerator()
    {
        var compilation = CSharpCompilation.Create(
            nameof(Test_EnumGenerator),
            [
                CSharpSyntaxTree.ParseText(
                    """
                    [FlameCsv.Attributes.CsvEnumConverter<byte, TestEnum>]
                    partial class EnumConverter;
                    
                    [System.Flags]
                    enum TestEnum
                    {
                        Dog,
                        Cat,
                        d0G,
                        Bird,
                        Fish,
                        Rabbit,
                        Elephant,
                        Crocodile,
                        [global::System.Runtime.Serialization.EnumMember(Value = "Zebra Animal!")]
                        Zebra,
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken)
            ],
            [CoreAssembly, ..Net90.References.All],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));

        var enumConverter = compilation.GetTypeByMetadataName("EnumConverter")!;
        var attribute = enumConverter.GetAttributes().Single();

        Assert.True(
            EnumModel.TryGet(
                enumConverter,
                attribute,
                TestContext.Current.CancellationToken,
                out var diagnostics,
                out var model));
        Assert.Empty(diagnostics);

        EnumConverterGenerator generator = new();
        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, true));

        // Run the generator once
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
    }
}

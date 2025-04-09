global using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
global using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;
global using RDC = System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute;
global using System.Numerics;

[assembly: System.Reflection.Metadata.MetadataUpdateHandlerAttribute(typeof(FlameCsv.Utilities.HotReloadService))]

[assembly:
    System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL3050",
        Scope = "namespaceanddescendants",
        Target = "~N:FastExpressionCompiler")]

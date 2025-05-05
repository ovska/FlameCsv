global using System.Numerics;
global using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
global using RDC = System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute;
global using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

[assembly: CLSCompliant(true)]

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(FlameCsv.Utilities.HotReloadService))]

[assembly: System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
    "ReflectionAnalysis",
    "IL3050",
    Scope = "namespaceanddescendants",
    Target = "~N:FastExpressionCompiler"
)]

global using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
global using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;
global using RDC = System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute;
global using System.Numerics;

using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly: System.Reflection.Metadata.MetadataUpdateHandlerAttribute(typeof(FlameCsv.Utilities.HotReloadService))]

// TODO: pragma filter these when building nuget package
#if true
[assembly: InternalsVisibleTo("FlameCsv.Console")]
[assembly: InternalsVisibleTo("FlameCsv.Benchmark")]
[assembly: InternalsVisibleTo("FlameCsv.Tests")]

[assembly: DebuggerDisplay(
    @"\{ StreamPipeReader, Buffered: {_bufferedBytes}, Stream completed: {_isStreamCompleted} \}",
    TargetTypeName = "System.IO.Pipelines.StreamPipeReader, System.IO.Pipelines, Version=6.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51")]
[assembly: DebuggerDisplay(
    @"\{ ReadResult, Buffer: {Buffer.Length}, IsCompleted: {IsCompleted} \}",
    Target = typeof(System.IO.Pipelines.ReadResult))]

#endif

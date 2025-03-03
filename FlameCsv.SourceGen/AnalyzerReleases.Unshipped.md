; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules


 Rule ID    | Category | Severity | Notes       
------------|----------|----------|-------------
 FLAMESG101 | Design   | Error    | File-scoped TypeMap
 FLAMESG102 | Design   | Error    | No ctor found
 FLAMESG103 | Design   | Error    | Invalid ctor param ref kind
 FLAMESG104 | Design   | Error    | Ref-like ctor param
 FLAMESG105 | Design   | Error    | No members to read
 FLAMESG106 | Design   | Error    | No members to write
 FLAMESG201 | Usage    | Error    | Conflicting attributes
 FLAMESG202 | Usage    | Error    | Target member not found
 FLAMESG203 | Usage    | Error    | No matching constructor
 FLAMESG204 | Usage    | Error    | Ignored parameter without default value
 FLAMESG205 | Usage    | Error    | Multiple type proxies
 FLAMESG206 | Usage    | Error    | No CsvConverterFactory ctor
 FLAMESG207 | Usage    | Error    | Abstract CsvConverter

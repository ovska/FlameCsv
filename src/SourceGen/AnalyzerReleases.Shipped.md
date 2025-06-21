; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.0

### New Rules

 Rule ID    | Category | Severity | Notes       
------------|----------|----------|-------------
 FLAMESG101 | Design   | Error    | File-scoped type
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
 FLAMESG206 | Usage    | Error    | No CsvConverter ctor
 FLAMESG207 | Usage    | Error    | Abstract CsvConverter

## Release 0.2.0

### New Rules

 Rule ID    | Category | Severity | Notes       
------------|----------|----------|-------------
 FLAMESG100 | Design   | Error    | Not a partial type
 FLAMESG501 | Usage    | Error    | Invalid converter token for enum generator
 FLAMESG502 | Usage    | Error    | Invalid EnumMemberAttribute
 FLAMESG503 | Usage    | Error    | Enum had Flags-attribute

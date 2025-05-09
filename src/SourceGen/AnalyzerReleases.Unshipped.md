; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; ### Changed Rules

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FLAMESG208 | Design  | Error    | Assembly attribute had no TargetType
FLAMESG209 | Design  | Error    | Type/Assembly attribute had no MemberName
FLAMESG210 | Design  | Error  | Conflicting index attributes
FLAMESG211 | Design  | Error  | Gap in index attributes

### Removed Rules

 Rule ID    | Category | Severity | Notes       
------------|----------|----------|-------------
FLAMESG503 | Usage    | Error    | Enum had Flags-attribute

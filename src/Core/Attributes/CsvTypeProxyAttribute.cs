using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the type used when instantiating the target type.
/// </summary>
[PublicAPI]
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    AllowMultiple = true, // type and assembly need AllMultiple = true
    Inherited = false
)]
public sealed class CsvTypeProxyAttribute : CsvConfigurationAttribute
{
    /// <summary>
    /// Type that will be created in place of the target type. Must be assignable to the target type.
    /// </summary>
    /// <remarks>
    /// Intended to be used for reading interfaces and abstract classes.<br/>
    /// Has no effect when writing.
    /// </remarks>
    [DAM(Messages.ReflectionBound)]
    public Type CreatedTypeProxy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvTypeProxyAttribute"/> class.
    /// </summary>
    /// <param name="createdTypeProxy">
    /// Type that will be created in place of the target type. Must be assignable to the target type.
    /// </param>
    public CsvTypeProxyAttribute([DAM(Messages.ReflectionBound)] Type createdTypeProxy)
    {
        ArgumentNullException.ThrowIfNull(createdTypeProxy);
        CreatedTypeProxy = createdTypeProxy;
    }
}

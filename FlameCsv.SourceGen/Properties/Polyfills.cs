#pragma warning disable IDE0161 // Convert to file-scoped namespace
using System.ComponentModel;

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullAttribute : Attribute
    {
        /// <summary>Initializes the attribute with a field or property member.</summary>
        /// <param name="member">
        /// The field or property member that is promised to be not-null.
        /// </param>
        public MemberNotNullAttribute(string member) => Members = [member];

        /// <summary>Initializes the attribute with the list of field and property members.</summary>
        /// <param name="members">
        /// The list of field and property members that are promised to be not-null.
        /// </param>
        public MemberNotNullAttribute(params string[] members) => Members = members;

        /// <summary>Gets field or property member names.</summary>
        public string[] Members { get; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : Attribute;
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal sealed class CompilerFeatureRequiredAttribute { public CompilerFeatureRequiredAttribute(string featureName) { } }

    /// <summary>Specifies that a type has required members or that a member is required.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class RequiredMemberAttribute : Attribute
    { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
    internal sealed class CollectionBuilderAttribute : Attribute
    {
        /// <summary>Initialize the attribute to refer to the <paramref name="methodName"/> method on the <paramref name="builderType"/> type.</summary>
        /// <param name="builderType">The type of the builder to use to construct the collection.</param>
        /// <param name="methodName">The name of the method on the builder to use to construct the collection.</param>
        /// <remarks>
        /// <paramref name="methodName"/> must refer to a static method that accepts a single parameter of
        /// type <see cref="ReadOnlySpan{T}"/> and returns an instance of the collection being built containing
        /// a copy of the data from that span.  In future releases of .NET, additional patterns may be supported.
        /// </remarks>
        public CollectionBuilderAttribute(Type builderType, string methodName)
        {
            BuilderType = builderType;
            MethodName = methodName;
        }

        /// <summary>Gets the type of the builder to use to construct the collection.</summary>
        public Type BuilderType { get; }

        /// <summary>Gets the name of the method on the builder to use to construct the collection.</summary>
        /// <remarks>This should match the metadata name of the target method. For example, this might be ".ctor" if targeting the type's constructor.</remarks>
        public string MethodName { get; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    internal sealed class SetsRequiredMembersAttribute { }
}

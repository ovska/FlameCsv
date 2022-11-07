using System.Reflection;

namespace FlameCsv.Binding;

internal readonly record struct HeaderBindingCandidate(string Value, MemberInfo Member, int Order);

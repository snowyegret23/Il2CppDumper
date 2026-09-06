using System;

namespace Il2CppDumper
{
    internal enum VariableIndexKind
    {
        Type,
        TypeDefinition,
        GenericContainer,
        Parameter
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class VariableIndexAttribute : Attribute
    {
        public VariableIndexKind Kind { get; }

        public VariableIndexAttribute(VariableIndexKind kind)
        {
            Kind = kind;
        }
    }
}

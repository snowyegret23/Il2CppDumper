using System;

namespace Il2CppDumper
{
    internal enum VariableIndexKind
    {
        Type,
        TypeDefinition,
        GenericContainer,
        Parameter,
        Event,
        Property,
        NestedType,
        Interface,
        Method,
        GenericParameter,
        Field,
        DefaultValueData,
        GenericInst,
        MethodSpec,
        MethodPointer,
        Invoker,
        AdjustorThunk
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

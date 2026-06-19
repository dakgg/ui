
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
public class RequiredReferenceAttribute : Attribute
{
    public RequiredReferenceAttribute(bool required = true)
    {
        Required = required;
    }

    public bool Required { get; private set; }
}
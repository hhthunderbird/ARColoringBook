using System;
namespace Felina.ARTextureMapping.DI
{
    [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property )]
    public class InjectAttribute : Attribute { }
}
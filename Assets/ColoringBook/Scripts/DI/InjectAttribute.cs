using System;
namespace Felina.ARColoringBook.DI
{
    [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property )]
    public class InjectAttribute : Attribute { }
}
namespace Line.Framework.Types;

public interface IHasCreateInfo;

public interface IHasCreateInfo<T> : IHasCreateInfo
{
    T CreationInfo { get; }
}

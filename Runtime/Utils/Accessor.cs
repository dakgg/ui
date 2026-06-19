// Extracted from ServiceLocatorGlobal.cs (which is game-specific and not part of
// this package). ServiceLocator.SetAccessor<T> depends on this type.
public class Accessor<T>
{
    public T Data { get; private set; }
    public bool IsValid => Data != null;
    public void SetData(T data) => Data = data;
}

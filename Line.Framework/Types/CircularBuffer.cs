using System.Collections;

namespace Line.Framework.Types;

public class CircularBuffer<T> : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator()
    {
        for (long i = 0; i < Count; i++)
        {
            yield return Buffer[i];
        }
    }

    private readonly Object _lock = new();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private LongArray<T> Buffer;
    public long Size
    {
        get;
        set
        {
            if (value < 0)
                throw new InvalidDataException($"Buffer Size cannot be {value}");
            field = value;
            Reset();
        }
    }
    private long AddPtr = 0;
    private bool Circulated = false;
    public long Count
    {
        get => Circulated ? Size : AddPtr;
    }
    public T this[long i]
    {
        get
        {
            void throwOutsideTheBounds() =>
                throw new ArgumentOutOfRangeException(
                    nameof(i),
                    i,
                    "Index was outside the bounds of the array."
                );
            if (i < 0)
                throwOutsideTheBounds();
            if (Circulated)
            {
                if (i >= Size)
                    throwOutsideTheBounds();
                var tg = (AddPtr + i) % Size;
                return Buffer[tg];
            }
            if (i >= Count)
                throwOutsideTheBounds();
            return Buffer[i];
        }
    }

    public void Add(T value)
    {
        if (Size <= 0)
            throw new InvalidOperationException("Cannot add to a buffer of size 0.");
        lock (_lock)
        {
            Buffer[AddPtr] = value;
            AddPtr++;
            if (AddPtr >= Size)
            {
                AddPtr = 0;
                Circulated = true;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            Buffer = new(Size);
            AddPtr = 0;
            Circulated = false;
        }
    }

    public CircularBuffer(long Size)
    {
        this.Size = Size;
    }
}

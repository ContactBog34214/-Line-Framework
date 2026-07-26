namespace Line.Framework.IO;

public interface ITouchDevice{
    Dictionary<ulong,ICursor> Touches{get;}
    ICursor GetTouch(ulong Id);
}
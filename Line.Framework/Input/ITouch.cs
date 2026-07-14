namespace Line.Framework.Input;

public interface ITouchDevice{
    Dictionary<ulong,ICursor> Touches{get;}
    ICursor GetTouch(ulong Id);
}
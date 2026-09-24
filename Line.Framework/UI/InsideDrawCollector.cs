using System.Collections.Concurrent;
using Line.Framework.Graphics;

namespace Line.Framework.UI;

public sealed class InsideDrawCollector(UIDrawCollector collector, UIWidget widget) : UIDrawCollector
{
    private UIDrawCollector _mainCollector = collector;
    private UIWidget _mainWidget = widget;
    public override List<DrawCommand> Verts { get; }=[];
    public void ChangeMainWidget(UIWidget widget) => _mainWidget = widget;
    public void ChangeMainCollector(UIDrawCollector collector) => _mainCollector = collector;
    public Vertex[] Results { get; private set; } = [];
    private ConcurrentDictionary< UIWidget,List<Vertex>> C { get; }= [];
    public override void DrawVertex(IEnumerable<Vertex> v, UIWidget source)
    {
        int count = v.Count();
        bool exist=C.TryGetValue(source,out var verts);
        verts ??= [];
        List<Vertex> group = [];
        foreach (var i in v)
        {
            group.Add(i);
            if(group.Count>=3)
                verts.AddRange(group);
            else continue;
            group.Clear();
        }
        if (!exist) C.TryAdd(source,verts);
    }
    private List<Vertex> vertices=[];
    public Vertex[] Done(bool buildDrawCommands=false)
    {
        Verts.Clear();
        vertices.Clear();
        foreach (var i in C.OrderBy(c => c.Key?.Index ?? int.MaxValue).Select(c => c.Value))
        {
            if(buildDrawCommands)
                Verts.Add(new()
                {
                    Source = _mainWidget,
                    Vert = [..i],
                    Z=_mainWidget.Index,
                });
            vertices.AddRange(i);
        }
        Results = [.. vertices];
        if(vertices.Capacity>vertices.Count*1.2+200)
            vertices.TrimExcess();
        if(Verts.Capacity>Verts.Count*1.2+200)
            Verts.TrimExcess();
        C.Clear();
        return Results;
    }
    public void Submit()
    {
        _mainCollector.DrawVertex(Done(false),_mainWidget);
        Clear();
    }
    public override void Clear()
    {
        base.Clear();
        C.Clear();
        vertices.Clear();
        Verts.Clear();
        if(vertices.Capacity>vertices.Count*1.2+200)
            vertices.TrimExcess();
        if(Verts.Capacity>Verts.Count*1.2+200)
            Verts.TrimExcess();
    }
}

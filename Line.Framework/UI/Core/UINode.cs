using Line.Framework;

namespace Line.Framework.UI;

public abstract class UINode : IDisposable, IName
{
    public string Name { get; set; }

    //对外的加点料
    private UINode _parent;
    public UINode Parent
    {
        get => _parent;
        set => SetParent(value);
    }

    //对外的只读
    private protected readonly List<UINode> _children = [];
    public List<UINode> Children
    {
        get => _children;
    }
    internal nint NodeTreeVersion { get; set; } = 0;

    internal void AddNodeTreeVersion()
    {
        _parent?.AddNodeTreeVersion();
        NodeTreeVersion++;
    }

    public virtual void SetParent(UINode value)
    {
        if (value == _parent)
        {
            return;
        }
        //解除旧绑定
        _parent?.AddNodeTreeVersion();
        _parent?._children.Remove(this);

        //新绑定
        _parent = value;
        _parent?._children.Add(this);
        _parent?.AddNodeTreeVersion();
    }

    public List<UINode> FindChildren(string name)
    {
        List<UINode> tmp = [];
        foreach (UINode i in _children)
        {
            if (name == i.Name)
            {
                tmp.Add(i);
            }
        }
        return tmp;
    }

    public virtual void Dispose()
    {
        Parent = null;
        //删除children
        List<UINode> tmp = [];
        tmp.AddRange(_children);
        foreach (UINode i in tmp)
        {
            i.Dispose();
        }
    }

    public float Z { get; set; } = 0;

    public UINode FindRoot()
    {
        if (this.Parent != null)
        {
            return FindRoot(this.Parent);
        }
        else if (this.Parent is UIScreen)
        {
            return this.Parent;
        }
        else
        {
            return null;
        }
    }

    public static UINode FindRoot(UINode widget)
    {
        if (widget.Parent != null)
        {
            return FindRoot(widget.Parent);
        }
        else if (widget is UIScreen)
        {
            return widget;
        }
        else
        {
            return null;
        }
    }
}

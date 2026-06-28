namespace Line.Framework.UI;

public abstract class UINode : IDisposable
{
    public string name { get; set; }

    //对外的加点料
    private UINode _parent;
    public UINode parent
    {
        get => _parent;
        set => SetParent(value);
    }

    //对外的只读
    private protected  readonly List<UINode> _children = [];
    public List<UINode> children
    {
        get => _children;
    }

    public virtual void SetParent(UINode value)
    {
        if (value == _parent)
        {
            return;
        }
        //解除旧绑定
        if (_parent != null)
        {
            _parent._children.Remove(this);
        }
        //新绑定
        _parent = value;
        if (_parent != null)
        {
            _parent._children.Add(this);
        }
    }

    public List<UINode> FindChildren(string name)
    {
        List<UINode> tmp = [];
        foreach (UINode i in _children)
        {
            if (name == i.name)
            {
                tmp.Add(i);
            }
        }
        return tmp;
    }

    public virtual void Dispose()
    {
        parent = null;
        //删除children
        List<UINode> tmp = [];
        tmp.AddRange(_children);
        foreach (UINode i in tmp)
        {
            i.Dispose();
        }
        DisposeHook?.Invoke();
    }

    public Action DisposeHook;
    public float Z { get; set; } = 0;
    public UINode FindRoot()
    {
        if (this.parent != null)
        {
            return FindRoot(this.parent);
        }
        else if (this.parent is UIScreen)
        {
            return this.parent;
        }
        else
        {
            return null;
        }
    }
    public static UINode FindRoot(UINode widget)
    {
        if (widget.parent != null)
        {
            return FindRoot(widget.parent);
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

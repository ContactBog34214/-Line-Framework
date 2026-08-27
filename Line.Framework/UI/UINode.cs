using Line.Framework;
using Line.Framework.Types;

namespace Line.Framework.UI;

public abstract class UINode : IDisposable, IName, IIndexable
{
    public string Name { get; set; }

    //对外的加点料
    private UINode _parent;

    /// <summary>
    /// 父节点
    /// </summary>
    public UINode Parent
    {
        get => _parent;
        set => SetParent(value);
    }

    //对外的只读
    private protected readonly List<UINode> _children = new();

    /// <summary>
    /// 子节点
    /// </summary>
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

    /// <summary>
    /// 设置父节点
    /// </summary>
    /// <param name="父节点"></param>
    public virtual void SetParent(UINode value)
    {
        if (value == _parent)
            return;

        //解除旧绑定
        var op = _parent;
        if (op != null)
            lock (op._children)
                try
                {
                    _parent._children.Remove(this);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
        _parent?.AddNodeTreeVersion();

        //新绑定
        _parent = value;
        if (_parent != null)
            lock (_parent._children)
                _parent._children.Add(this);
        _parent?.AddNodeTreeVersion();

        foreach (var i in _children.OrderBy(c => c?.Index ?? -100))
        {
            if (i == null)
                continue;
            i?.SetParent(this);
        }
    }

    /// <summary>
    /// 寻找指定名称的子节点
    /// </summary>
    /// <param name="名称"></param>
    /// <returns>子节点</returns>
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

    public float Index
    {
        get;
        set { field = value; }
    } = 0;

    /// <summary>
    /// 寻找根节点
    /// </summary>
    /// <returns>根节点</returns>
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

    /// <summary>
    /// 寻找根节点
    /// </summary>
    /// <param name="节点"></param>
    /// <returns>根节点</returns>
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

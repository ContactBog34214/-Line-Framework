namespace Line.Framework.UI;

public class UINode : IDisposable
{
    public string name;

    //对外的加点料
    private UINode _parent;
    public UINode parent
    {
        get => _parent;
        set => ParentSetter(value);
    }

    //对外的只读
    private protected List<UINode> _children = [];
    public List<UINode> children
    {
        get => _children;
    }

    private void ParentSetter(UINode value)
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

    public void Dispose()
    {
        parent = null;
        //删除children
        List<UINode> tmp = _children;
        foreach (UINode i in tmp)
        {
            i.Dispose();
        }
        _children = null;
    }
}

using Line.Framework.IO;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.Sandbox;

public class InsideSandbox : UIScreen
{
    protected internal readonly UISandbox MainSandbox;
    protected internal UIScreen Scr
    {
        get; set
        {
            if (field == value) return;
            field?.OnRender -= OR;
            field?.OnUpdate -= OU;
            field = value;
            field?.OnRender += OR;
            field?.OnUpdate += OU;
        }
    }
    protected internal InsideSandbox(UISandbox sandbox) : base(FindRoot(sandbox) as UIScreen, 0, 0)
    {
        MainSandbox = sandbox;
        s = new(() => MainSandbox.s);
        p = new(() => MainSandbox.p);
        o = new(() => MainSandbox.o);
        OR = a => OnRender?.Invoke(a);
        OU = a => OnUpdate?.Invoke(a);
    }
    protected virtual SandCompositor Compositor { get; } = new();
    public override InputManager InputManager => MainSandbox.IM;
    public override event Action<double> OnRender;
    public override event Action<double> OnUpdate;
    public override bool TextInput
    {
        get
        {
            if (!MainSandbox.AllowGetTextInputState) throw new InvalidOperationException("Cannot get TextInputState");
            return Scr == null ? window.TextInput : Scr.TextInput;
        }
        set
        {
            if (!MainSandbox.AllowSetTextInputState) throw new InvalidOperationException($"Cannot set TextInputState {value}");
            if (Scr == null)
                window.TextInput = value;
            else
                Scr.TextInput = value;
        }
    }
    public override async Task RendererContext(RendererContextArgs args) { }
    public override void Dispose()
    {
        if (!MainSandbox.AllowDispose) throw new InvalidOperationException("Sandbox Root cannot dispose");
        base.Dispose();
    }
}
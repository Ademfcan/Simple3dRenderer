// The binding interface, simplified to only execute the action.
public interface IInputBinding
{
    void Execute(float deltaTime);
}

// An action that takes delta time.
public class ActionBinding : IInputBinding
{
    private readonly Action<float> _action;

    public ActionBinding(Action<float> action)
    {
        _action = action;
    }

    public void Execute(float deltaTime) => _action(deltaTime);
}

// An action that toggles a boolean value.
public class ToggleBinding : IInputBinding
{
    private readonly Func<bool> _getter;
    private readonly Action<bool> _setter;

    public ToggleBinding(Func<bool> getter, Action<bool> setter)
    {
        _getter = getter;
        _setter = setter;
    }

    // deltaTime is unused here but required to match the interface.
    public void Execute(float deltaTime) => _setter(!_getter());
}
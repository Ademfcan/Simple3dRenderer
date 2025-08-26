using SDL;
using System;
using System.Collections.Generic;

public class InputManager
{
    public bool IsRunning { get; private set; } = true;
    
    // Use a Dictionary for O(1) average lookup time.
    // The value is a List to allow multiple bindings for a single key.
    private readonly Dictionary<SDL_Keycode, List<IInputBinding>> _bindings = new();

    // Bind a key to an action that takes delta time.
    public void BindAction(SDL_Keycode key, Action<float> action)
    {
        AddBinding(key, new ActionBinding(action));
    }

    // Bind a key to toggle a boolean property.
    public void BindToggle(SDL_Keycode key, Func<bool> getter, Action<bool> setter)
    {
        AddBinding(key, new ToggleBinding(getter, setter));
    }
    
    // Helper method to add any binding to the dictionary.
    private void AddBinding(SDL_Keycode key, IInputBinding binding)
    {
        // If the key hasn't been used yet, create a new list for it.
        if (!_bindings.ContainsKey(key))
        {
            _bindings[key] = new List<IInputBinding>();
        }
        _bindings[key].Add(binding);
    }

    // The main input loop, now much more efficient.
    public void HandleInput(float deltaTime)
    {
        unsafe
        {
            SDL_Event e;
            while (SDL3.SDL_PollEvent(&e) != false)
            {
                switch ((SDL_EventType)e.type)
                {
                    case SDL_EventType.SDL_EVENT_QUIT:
                        IsRunning = false;
                        break;
                    
                    case SDL_EventType.SDL_EVENT_KEY_DOWN:
                        // Try to find the key in our dictionary.
                        if (_bindings.TryGetValue(e.key.key, out var bindingList))
                        {
                            // If found, execute all associated bindings.
                            foreach (var binding in bindingList)
                            {
                                binding.Execute(deltaTime);
                            }
                        }
                        break;
                }
            }
        }
    }

    // Special action for quitting the application. No changes needed here.
    public void BindQuit(SDL_Keycode key)
    {
        BindAction(key, (deltatime) => IsRunning = false);
    }
}
namespace Simple3dRenderer.Rendering // Or a more general namespace
{
    /// <summary>
    /// A generic, reusable wrapper class that allows a struct (value type)
    /// to be treated like a reference type. This is essential for sharing
    /// a single, mutable struct instance between multiple objects.
    /// </summary>
    public sealed class StateWrapper<T>
    {
        public T State;
    }
}
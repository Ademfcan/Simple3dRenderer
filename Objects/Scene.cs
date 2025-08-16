using System.Numerics;
using SDL;

namespace Simple3dRenderer.Objects
{
    public class Scene(Camera camera, List<Mesh> objects, SDL_Color backgroundColor = default, Vector3? ambientLight = null)
    {
        public Camera camera = camera;
        public List<Mesh> objects = objects;
        public SDL_Color backgroundColor = backgroundColor;
        public Vector3 ambientLight = ambientLight ?? new(1,1,1);
    }
}

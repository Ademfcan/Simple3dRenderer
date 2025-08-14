using System.Numerics;
using SDL;
using Simple3dRenderer.Lighting;
using Simple3dRenderer.Rendering;

namespace Simple3dRenderer.Objects
{
    public class Scene(Camera camera, List<Mesh> objects, List<PerspectiveLight> lights, SDL_Color backgroundColor = default, Vector3? ambientLight = null)
    {
        public Camera camera = camera;
        public List<Mesh> objects = objects;
        public List<PerspectiveLight> lights = lights;
        public SDL_Color backgroundColor = backgroundColor;
        public Vector3 ambientLight = ambientLight ?? new(1,1,1);
    }
}

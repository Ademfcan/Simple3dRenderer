using System.Numerics;
using SDL;
using Simple3dRenderer.Textures;
using Simple3dRenderer.Objects;
using System.Data;


namespace Simple3dRenderer.Shaders
{
    public class TextureShader()
    {
        public Texture? texture = null;

        public SDL_Color TexturedShader(
            Vertex v0, Vertex v1, Vertex v2,
            float w0, float w1, float w2)
        {
            if (texture == null)
            {
                throw new ConstraintException("The texture must not be null!");
            }

            // Interpolate attributes
            Vector2 p = Vertex.InterpolateUV(v0, v1, v2, w0, w1, w2);

            // Sample texture using UV
            float u = p.X;
            float v = p.Y;

            // Clamp or wrap UVs
            u = Math.Clamp(u, 0, 1);
            v = Math.Clamp(v, 0, 1);

            int texX = (int)(u * (texture.width - 1));
            int texY = (int)(v * (texture.height - 1));

            return texture.pixels[texY, texX];
        }
    }

}
namespace Simple3dRenderer.Textures
{
    public interface ITextured
    {
        public Texture? GetTexture();
        public void SetTexture(Texture texture);
    }
}
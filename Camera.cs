using System.Numerics;

namespace Simple3dRenderer
{
    public struct Camera(int HRes, int VRes, int Fov, float NearClip = 0.1f, float FarClip = 1e4f, Vector3 Position = default, Quaternion Rotation = default)
    {
        public Vector3 Position = Position;   // Camera world position
        public Quaternion Rotation = Rotation;   // Rotation in radians: (Pitch, Yaw, Roll)

        public float Fov = Fov;
        public int HRes = HRes;
        public int VRes = VRes;        // Field of view in degrees
        public float AspectRatio = (float)HRes / VRes;  // Screen width / height
        public float NearClip = NearClip;
        public float FarClip = FarClip;


        public Matrix4x4 getViewMatrix()
        {
            // Inverse rotation and translation to move world around camera
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(Rotation));
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(-Position);

            // View matrix is inverse of camera transform
            // NOTE: row major means left to right order
            return translationMatrix * rotationMatrix;
        }

        public Matrix4x4 getProjectionMatrix()
        {
            float fovRadians = MathF.PI * Fov / 180f;
            return Matrix4x4.CreatePerspectiveFieldOfView(
                fovRadians,
                AspectRatio,
                NearClip,
                FarClip);
                
        }
    }
}
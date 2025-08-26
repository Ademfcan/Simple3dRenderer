using System;
using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Extensions;

namespace Simple3dRenderer.Rendering
{
    /// <summary>
    /// Represents the characteristics of a rendering viewport, defining the projection
    /// parameters and the screen-space dimensions. This class inherits from WorldObject
    /// to manage the camera's position, rotation, and scale in world space.
    /// </summary>
    public abstract class Viewport : WorldObject
    {
        #region Fields

        private readonly int _hRes;
        private readonly int _vRes;

        private Matrix4x4? _cachedViewMatrix;
        private Matrix4x4? _cachedProjectionMatrix;
        private Matrix<float>? _cachedWorldToClipMatrix;

        #endregion

        #region Properties

        public int Width => _hRes;
        public int Height => _vRes;
        public float FieldOfViewDegrees { get; private set; }
        public float AspectRatio { get; private set; }
        public float NearPlane { get; private set; }
        public float FarPlane { get; private set; }

        #endregion

        #region Constructor

        protected Viewport(
            int hRes, int vRes,
            float fovDegrees,
            float nearPlane,
            float farPlane)
        {
            _hRes = hRes;
            _vRes = vRes;
            FieldOfViewDegrees = fovDegrees;
            AspectRatio = (float)_hRes / _vRes;
            NearPlane = nearPlane;
            FarPlane = farPlane;
            
            // Subscribe to the base class events to invalidate matrices whenever the transform changes.
            OnPositionUpdate += _ => InvalidateViewMatrices();
            OnRotationUpdate += _ => InvalidateViewMatrices();
            OnScaleUpdate += _ => InvalidateViewMatrices();
        }

        #endregion

        #region Public Methods

        public void SetFieldOfView(float fovDegrees)
        {
            FieldOfViewDegrees = fovDegrees;
            InvalidateProjectionMatrices();
        }

        public void SetNearFarPlanes(float nearPlane, float farPlane)
        {
            NearPlane = nearPlane;
            FarPlane = farPlane;
            InvalidateProjectionMatrices();
        }

        public Matrix<float> GetWorldToClipMatrix()
        {
            if (_cachedWorldToClipMatrix == null)
            {
                _cachedWorldToClipMatrix = GetProjectionMatrix().ToMathNet() * GetViewMatrix().ToMathNet();
            }
            return _cachedWorldToClipMatrix;
        }

        #endregion

        #region Private Helper Methods

        // Invalidates matrices that depend on the camera's transform (View)
        private void InvalidateViewMatrices()
        {
            _cachedViewMatrix = null;
            _cachedWorldToClipMatrix = null;
        }

        // Invalidates matrices that depend on projection parameters
        private void InvalidateProjectionMatrices()
        {
            _cachedProjectionMatrix = null;
            _cachedWorldToClipMatrix = null;
        }

        private Matrix4x4 GetViewMatrix()
        {
            return _cachedViewMatrix ??= CalculateViewMatrix();
        }

        /// <summary>
        /// Calculates the view matrix from the camera's TRS (Translate, Rotate, Scale).
        /// The view matrix is the inverse of the camera's world matrix.
        /// </summary>
        private Matrix4x4 CalculateViewMatrix()
        {
            // // The view matrix transforms the world to be relative to the camera's position and orientation.
            // // It is the mathematical INVERSE of the camera's own world transform matrix.

            // 1. Create the inverse of the camera's scale.
            var invScale = new Vector3(1.0f / Scale.X, 1.0f / Scale.Y, 1.0f / Scale.Z);
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(invScale);

            // 2. Create the inverse of the camera's rotation (using the conjugate of the quaternion).
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(Rotation));

            // 3. Create the inverse of the camera's translation.
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(-Position);

            // The correct order to combine these inverse operations is Undo Translate -> Undo Rotation -> Undo Scale.
            // row major so S * R * T is S then R then T
            return translationMatrix * rotationMatrix * scaleMatrix;

            //             // Define the "forward" direction based on the camera's rotation.
            // // -Vector3.UnitZ is forward in a right-handed coordinate system.
            // Vector3 forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
            // Vector3 target = Position + forward;

            // // Define the "up" direction based on the camera's rotation.
            // Vector3 up = Vector3.Transform(Vector3.UnitY, Rotation);

            // // Create the view matrix using the built-in, stable LookAt function.
            // return Matrix4x4.CreateLookAt(Position, target, up);
        }

        private Matrix4x4 GetProjectionMatrix()
        {
            return _cachedProjectionMatrix ??= CalculateProjectionMatrix();
        }

        private Matrix4x4 CalculateProjectionMatrix()
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI * FieldOfViewDegrees / 180f,
                AspectRatio,
                NearPlane,
                FarPlane);
        }

        #endregion
    }
}
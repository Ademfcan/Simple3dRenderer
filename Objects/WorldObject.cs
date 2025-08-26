using System;
using System.Numerics;
using System.Collections.Generic;

namespace Simple3dRenderer.Rendering
{
    /// <summary>
    /// Represents an object in world space with a position, rotation, scale, and direction.
    /// This class provides a mechanism to link multiple WorldObjects together, so they share the same spatial state.
    /// It also provides events that are triggered when the object's transform is updated.
    /// </summary>
    public abstract class WorldObject
    {
        #region Fields

        private Vector3 _position = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;
        private Vector3 _direction;

        private readonly List<WorldObject> _linkedObjects = new List<WorldObject>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the world-space position of the object.
        /// </summary>
        public Vector3 Position => _position;

        /// <summary>
        /// Gets the world-space rotation (orientation) of the object.
        /// </summary>
        public Quaternion Rotation => _rotation;

        /// <summary>
        /// Gets the world-space scale of the object.
        /// </summary>
        public Vector3 Scale => _scale;

        /// <summary>
        /// Gets the forward direction vector of the object, derived from its rotation.
        /// </summary>
        public Vector3 Direction => _direction;

        #endregion

        #region Events

        /// <summary>
        /// Action invoked when the position of the object is updated.
        /// The new position is passed as a parameter.
        /// </summary>
        public event Action<Vector3> OnPositionUpdate;

        /// <summary>
        /// Action invoked when the rotation of the object is updated.
        /// The new rotation is passed as a parameter.
        /// </summary>
        public event Action<Quaternion> OnRotationUpdate;

        /// <summary>
        /// Action invoked when the scale of the object is updated.
        /// The new scale is passed as a parameter.
        /// </summary>
        public event Action<Vector3> OnScaleUpdate;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="WorldObject"/> class.
        /// </summary>
        protected WorldObject()
        {
            _direction = Vector3.Transform(-Vector3.UnitZ, _rotation);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the world-space position of the object and all linked objects.
        /// </summary>
        /// <param name="position">The new <see cref="Vector3"/> world position.</param>
        public virtual void SetPosition(Vector3 position) => SetPosition(position, true);

        /// <summary>
        /// Sets the world-space rotation (orientation) of the object and all linked objects.
        /// </summary>
        /// <param name="rotation">The new <see cref="Quaternion"/> rotation.</param>
        public virtual void SetRotation(Quaternion rotation) => SetRotation(rotation, true);

        /// <summary>
        /// Sets the world-space scale of the object and all linked objects.
        /// </summary>
        /// <param name="scale">The new <see cref="Vector3"/> scale.</param>
        public virtual void SetScale(Vector3 scale) => SetScale(scale, true);

        /// <summary>
        /// Links this WorldObject to another, synchronizing their positions, rotations, and scales.
        /// </summary>
        /// <param name="other">The <see cref="WorldObject"/> to link to.</param>
        /// <param name="bidirectional">A <see cref="bool"/> to determine if connection should be bidirectional.</param>
        public void Link(WorldObject other, bool bidirectional = true)
        {
            if (other != null && !_linkedObjects.Contains(other))
            {
                _linkedObjects.Add(other);

                if (bidirectional)
                    other.Link(this, false);
            }
        }

        #endregion

        #region Private Methods

        // propagate bool to avoid ping pongs

        private void SetPosition(Vector3 position, bool propagate)
        {
            _position = position;
            OnPositionUpdate?.Invoke(_position);

            if (propagate)
            {
                foreach (var linkedObject in _linkedObjects)
                    linkedObject.SetPosition(position, false);
            }
        }

        private void SetRotation(Quaternion rotation, bool propagate)
        {
            _rotation = rotation;
            _direction = Vector3.Transform(-Vector3.UnitZ, rotation);
            OnRotationUpdate?.Invoke(_rotation);

            if (propagate)
            {
                foreach (var linkedObject in _linkedObjects)
                    linkedObject.SetRotation(rotation, false);
            }
        }

        private void SetScale(Vector3 scale, bool propagate)
        {
            _scale = scale;
            OnScaleUpdate?.Invoke(_scale);

            if (propagate)
            {
                foreach (var linkedObject in _linkedObjects)
                    linkedObject.SetScale(scale, false);
            }
        }

        #endregion
    }
}
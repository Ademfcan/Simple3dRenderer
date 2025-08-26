using System;
using System.Numerics;
using SDL; // Assuming you are using an SDL2# wrapper

namespace Simple3dRenderer.Input
{
    /// <summary>
    /// Manages mouse input for first-person camera-style rotation using SDL.
    /// This class captures relative mouse motion to create a rotation quaternion.
    /// </summary>
    public static class MouseLookController
    {
        private static float _totalYaw = 0.0f;
        private static float _totalPitch = 0.0f;

        /// <summary>
        /// The sensitivity of the mouse movement. Higher values result in faster rotation.
        /// </summary>
        public static float MouseSensitivity { get; set; } = 0.005f;

        /// <summary>
        /// Enables or disables relative mouse mode.
        /// When enabled, the cursor is hidden and mouse motion is tracked continuously.
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        public unsafe static void SetRelativeMouseMode(SDL_Window* window, bool enable)
        {
            // SDL_TRUE is typically 1, SDL_FALSE is 0
            var result = SDL3.SDL_SetWindowRelativeMouseMode(window, enable);
            if (!result)
            {
                // You may want to add more robust error handling here
                Console.WriteLine($"Error setting relative mouse mode: {SDL3.SDL_GetError()}");
            }
        }

        /// <summary>
        /// Updates the rotation based on the latest mouse movement.
        /// This should be called once per frame.
        /// </summary>
        /// <returns>A new Quaternion representing the updated orientation.</returns>
        public unsafe static Quaternion UpdateAndGetRotation()
        {
            // Get the change in mouse position since the last call. [1]
            float deltaX;
            float deltaY;
            SDL3.SDL_GetRelativeMouseState(&deltaX, &deltaY);

            // Adjust the total yaw (horizontal rotation) based on the x delta.
            _totalYaw -= deltaX * MouseSensitivity; // The sign can be flipped depending on desired direction

            // Adjust the total pitch (vertical rotation) based on the y delta.
            _totalPitch -= deltaY * MouseSensitivity;

            // Clamp the pitch to prevent the camera from flipping upside down.
            // This restricts vertical look to straight up and straight down.
            const float maxPitch = MathF.PI / 2.0f - 0.001f; // Just under 90 degrees
            _totalPitch = Math.Clamp(_totalPitch, -maxPitch, maxPitch);

            // Create rotations from the yaw and pitch angles.
            // Yaw is around the world's Y-axis.
            // Pitch is around the local X-axis.
            Quaternion yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _totalYaw);
            Quaternion pitchRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, _totalPitch);

            // Combine the rotations: apply yaw first, then pitch.
            // This creates the final orientation.
            return yawRotation * pitchRotation;
        }

        public static float GetTotalYaw()
        {
            return _totalYaw;
        }
    }
}
﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace SampleBase
{
    public class Camera2
    {
        private float _fov = 1f;
        private float _near = 1f;
        private float _far = 1000f;

        private Matrix4x4 _viewMatrix;
        private Matrix4x4 _projectionMatrix;

        private Vector3 _position = new Vector3(0, 3, 0);
        private Vector3 _lookDirection = new Vector3(0, -.3f, -1f);
        private float _moveSpeed = 5.0f;

        private float _yaw;
        private float _pitch;

        private Vector2 _previousMousePos;
        private float _windowWidth;
        private float _windowHeight;

        public event Action<Matrix4x4> ProjectionChanged;
        public event Action<Matrix4x4> ViewChanged;

        public Camera2(float width, float height)
        {
            _windowWidth = width;
            _windowHeight = height;
            UpdatePerspectiveMatrix();
            UpdateViewMatrix();
        }

        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

        public Vector3 Position { get => _position; set { _position = value; UpdateViewMatrix(); } }

        public float FarDistance { get => _far; set { _far = value; UpdatePerspectiveMatrix(); } }
        public float FieldOfView => _fov;
        public float NearDistance { get => _near; set { _near = value; UpdatePerspectiveMatrix(); } }

        public float AspectRatio => _windowWidth / _windowHeight;

        public float Yaw { get => _yaw; set { _yaw = value; UpdateViewMatrix(); } }
        public float Pitch { get => _pitch; set { _pitch = value; UpdateViewMatrix(); } }

        public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
        public Vector3 Forward => GetLookDir();

        public void Update(float deltaSeconds)
        {
            float sprintFactor = InputTracker.GetKey(Key.ControlLeft)
                ? 0.1f
                : InputTracker.GetKey(Key.ShiftLeft)
                    ? 2.5f
                    : 1f;
            Vector3 motionDir = Vector3.Zero;
            if (InputTracker.GetKey(Key.A))
            {
                motionDir += -Vector3.UnitX;
            }
            if (InputTracker.GetKey(Key.D))
            {
                motionDir += Vector3.UnitX;
            }
            if (InputTracker.GetKey(Key.W))
            {
                motionDir += -Vector3.UnitZ;
            }
            if (InputTracker.GetKey(Key.S))
            {
                motionDir += Vector3.UnitZ;
            }
            if (InputTracker.GetKey(Key.Space))
            {
                motionDir -= Vector3.UnitY;
            }
            if (InputTracker.GetKey(Key.ShiftLeft))
            {
                motionDir += Vector3.UnitY;
            }

            if (motionDir != Vector3.Zero)
            {
                Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
                motionDir = Vector3.Transform(motionDir, lookRotation);
                _position += motionDir * MoveSpeed * sprintFactor * deltaSeconds;
                UpdateViewMatrix();
            }

            Vector2 mouseDelta = InputTracker.MousePosition - _previousMousePos;
            _previousMousePos = InputTracker.MousePosition;

            if (InputTracker.GetMouseButton(MouseButton.Left) || InputTracker.GetMouseButton(MouseButton.Right))
            {
                Yaw += -mouseDelta.X * 0.01f;
                Pitch += -mouseDelta.Y * 0.01f;
                Pitch = Clamp(Pitch, -1.55f, 1.55f);

                UpdateViewMatrix();
            }
        }

        private float Clamp(float value, float min, float max)
        {
            return value > max
                ? max
                : value < min
                    ? min
                    : value;
        }

        public void WindowResized(float width, float height)
        {
            _windowWidth = width;
            _windowHeight = height;
            UpdatePerspectiveMatrix();
        }

        private void UpdatePerspectiveMatrix()
        {
            _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(_fov, _windowWidth / _windowHeight, _near, _far);
            ProjectionChanged?.Invoke(_projectionMatrix);
        }

        private void UpdateViewMatrix()
        {
            Vector3 lookDir = GetLookDir();
            _lookDirection = lookDir;
            _viewMatrix = Matrix4x4.CreateLookAt(_position, _position + _lookDirection, Vector3.UnitY);
            ViewChanged?.Invoke(_viewMatrix);
        }

        private Vector3 GetLookDir()
        {
            Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
            Vector3 lookDir = Vector3.Transform(-Vector3.UnitZ, lookRotation);
            return lookDir;
        }

        public CameraInfo GetCameraInfo() => new CameraInfo
        {
            CameraPosition_WorldSpace = _position,
            CameraLookDirection = _lookDirection
        };
    }

    public class Camera
    {
        private float _fov = 1f;
        private float _near = 1f;
        private float _far = 1000f;

        private float _windowWidth;
        private float _windowHeight;

        public event Action<Matrix4x4> ProjectionChanged;
        public event Action<Matrix4x4> ViewChanged;

        private Vector2 _previousMousePos;
        private Matrix4x4 _viewMatrix;
        private Matrix4x4 _projectionMatrix;
        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;
        private Vector3 _position = new Vector3(0, 3, 0);

        Quaternion curRot = Quaternion.Identity;
        float dist = 12;

        public Vector3 Position { get => _position; set { _position = value; UpdateViewMatrix(); } }

        public float FarDistance { get => _far; set { _far = value; UpdatePerspectiveMatrix(); } }
        public float FieldOfView => _fov;
        public float NearDistance { get => _near; set { _near = value; UpdatePerspectiveMatrix(); } }

        public float AspectRatio => _windowWidth / _windowHeight;

        public Camera(float width, float height)
        {
            _windowWidth = width;
            _windowHeight = height;
            UpdatePerspectiveMatrix();
            UpdateViewMatrix();
        }

        public void Update(float deltaSeconds)
        {
            Vector2 mouseDelta = InputTracker.MousePosition - _previousMousePos;
            _previousMousePos = InputTracker.MousePosition;

            if (InputTracker.GetMouseButton(MouseButton.Left))
            {
                curRot = curRot * Quaternion.CreateFromAxisAngle(Vector3.UnitY, mouseDelta.X * 0.02f) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitX, mouseDelta.Y * 0.02f);
            }
            else if(InputTracker.GetMouseButton(MouseButton.Right))
            {
                float ld = MathF.Log2(dist);
                ld += mouseDelta.Y * 0.002f;
                dist = MathF.Pow(2, ld);
            }

            UpdateViewMatrix();
        }

        public void WindowResized(float width, float height)
        {
            _windowWidth = width;
            _windowHeight = height;
            UpdatePerspectiveMatrix();
        }

        private void UpdatePerspectiveMatrix()
        {
            _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(_fov, _windowWidth / _windowHeight, _near, _far);
            ProjectionChanged?.Invoke(_projectionMatrix);
        }

        private void UpdateViewMatrix()
        {
            Vector3 dirVec = Vector3.Transform(Vector3.UnitZ, curRot);
            Matrix4x4 invView = Matrix4x4.CreateFromQuaternion(curRot) * Matrix4x4.CreateTranslation(dirVec * dist);
            Matrix4x4.Invert(invView, out this._viewMatrix);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CameraInfo
    {
        public Vector3 CameraPosition_WorldSpace;
        private float _padding1;
        public Vector3 CameraLookDirection;
        private float _padding2;
    }
}

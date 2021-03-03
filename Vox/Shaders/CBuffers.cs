using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Vox.Shaders
{
    namespace DepthDownScale
    {
        public struct Subsample
        {
            public float ddx;
            public float ddy;
            Vector2 filler;
        }
    }

    namespace Depth
    {
        public struct Transform
        {
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public Matrix4x4 Model;
            public Vector4 LightPos;
        }

        public struct Material
        {
            public Vector4 DiffuseColor;
        }
    }

    namespace Blit
    {
        public struct Transform
        {
            public Matrix4x4 MWP;
        }
    }

    namespace Inst
    {
        public struct Transform
        {
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public Matrix4x4 Model;
            public Vector4 LightPos;
        }

    }
}

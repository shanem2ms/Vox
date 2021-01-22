using ShaderGen;
using System.Numerics;
using Vox;

[assembly: ShaderSet("Depth", "Vox.Shaders.Depth.VS", "Vox.Shaders.Depth.FS")]



namespace Vox.Shaders
{
    public class Depth
    {
        public struct Transform
        {
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public Matrix4x4 Model;
            public Vector4 LightPos;
        }

        Transform t = new Transform();

        public struct VertexInput
        {
            [PositionSemantic] public Vector3 Position;
            [TextureCoordinateSemantic] public Vector2 UV;
            [TextureCoordinateSemantic] public Vector3 Color;
            [TextureCoordinateSemantic] public Vector3 Normal;
        }


        public struct FragmentInput
        {
            [SystemPositionSemantic]
            public Vector4 Position;
            [TextureCoordinateSemantic]
            public Vector2 Depth;
            [TextureCoordinateSemantic]
            public Vector3 VtxColor;
        }


        [VertexShader]
        public FragmentInput VS(VertexInput input)
        {
            FragmentInput output;
            Vector4 v4Pos = new Vector4(input.Position, 1);
            output.VtxColor = input.Color;
            output.Position = Vector4.Transform(v4Pos, (t.Projection * t.View * t.Model));
            output.Depth = new Vector2(output.Position.Z, output.Position.W);
            return output;
        }

        [FragmentShader]
        public Vector4 FS(FragmentInput input)
        {

            float v = ((input.Depth.X / input.Depth.Y) + 1) / 2;
            float c = input.VtxColor.X + ShaderBuiltins.Floor(input.VtxColor.Y * 256) + ShaderBuiltins.Floor(input.VtxColor.Z * 256) * 256;
            return new Vector4(v, 1, c, 1);
        }
    }
}

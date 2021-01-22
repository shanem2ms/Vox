using ShaderGen;
using System.Numerics;

[assembly: ShaderSet("Blit", "Vox.Shaders.Blit.VS", "Vox.Shaders.Blit.FS")]


namespace Vox.Shaders
{
    public class Blit
    {

        public Texture2DResource Texture;
        public SamplerResource Sampler;

        public struct Transform
        {
            public Matrix4x4 MWP;
        }

        Transform t = new Transform();

        public struct VertexInput
        {
            [PositionSemantic] public Vector3 Position;
            [TextureCoordinateSemantic] public Vector3 UVW;
        }


        public struct FragmentInput
        {
            [SystemPositionSemantic]
            public Vector4 Position;
            [TextureCoordinateSemantic]
            public Vector2 fsUV;
        }


        [VertexShader]
        public FragmentInput VS(VertexInput input)
        {
            FragmentInput output;
            output.Position = Vector4.Transform(new Vector4(input.Position, 1), t.MWP);
            output.fsUV = new Vector2(input.UVW.X, input.UVW.Y);
            return output;
        }

        [FragmentShader]
        public Vector4 FS(FragmentInput input)
        {
            return ShaderBuiltins.Sample(Texture, Sampler, input.fsUV);
        }
    }
}

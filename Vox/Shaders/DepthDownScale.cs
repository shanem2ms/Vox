using ShaderGen;
using System.Numerics;
using static ShaderGen.ShaderBuiltins;

[assembly: ShaderSet("DepthDownScale", "Vox.Shaders.DepthDownScale.VS", "Vox.Shaders.DepthDownScale.FS")]


namespace Vox.Shaders
{
    public class DepthDownScale
    {

        public Texture2DResource Texture;
        public SamplerResource Sampler;

        public struct Subsample
        {
            public float ddx;
            public float ddy;
            Vector2 filler;
        }

        Subsample ss = new Subsample();

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
            output.Position = new Vector4((input.Position - new Vector3(0.5f, 0.5f, 0)) * 2, 1);
            output.fsUV = new Vector2(input.UVW.X, input.UVW.Y);
            return output;
        }

        Vector3 Decode(float c)
        {
            return new Vector3(Mod(c, 1), Mod(c / 256, 1), (c / (256 * 256)));
        }

        float Encode(Vector3 c)
        {
            return c.X + Floor(c.Y * 256) + Floor(c.Z * 256) * 256;
        }

        [FragmentShader]
        public Vector4 FS(FragmentInput input)
        {
            float x = ss.ddx * 0.5f;
            float y = ss.ddy * 0.5f;
            Vector4 v0 = Sample(Texture, Sampler, input.fsUV + new Vector2(-x, -y));
            Vector4 v1 = Sample(Texture, Sampler, input.fsUV + new Vector2(x, -y));
            Vector4 v2 = Sample(Texture, Sampler, input.fsUV + new Vector2(-x, y));
            Vector4 v3 = Sample(Texture, Sampler, input.fsUV + new Vector2(x, y));

            Vector3 c0 = Decode(v0.Z);
            Vector3 c1 = Decode(v1.Z);
            Vector3 c2 = Decode(v2.Z);
            Vector3 c3 = Decode(v3.Z);
            Vector3 cavg = (c0 + c1 + c2 + c3) * 0.25f;
            float vzavg = Encode(cavg);

            float maxdepth = Max(Max(Max(v0.X, v1.X), v2.X), v3.X);
            float mindepth = Min(Min(Min(v0.Y, v1.Y), v2.Y), v3.Y);
            float mask = (v0.W + v1.W + v2.W + v3.W) * 0.25f;
            return new Vector4(maxdepth, mindepth, vzavg, mask);
        }
    }
}

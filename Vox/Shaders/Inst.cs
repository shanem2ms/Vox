using ShaderGen;
using System.Numerics;
using Vox;

[assembly: ShaderSet("Inst", "Vox.Shaders.Inst.VS", "Vox.Shaders.Inst.FS")]



namespace Vox.Shaders
{
    public class Inst
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
            [TextureCoordinateSemantic] public Vector3 Normal;
            [TextureCoordinateSemantic] public Vector4 InstData0;
            [TextureCoordinateSemantic] public Vector4 InstData1;
        }


        public struct FragmentInput
        {
            [SystemPositionSemantic]
            public Vector4 Position;
            [TextureCoordinateSemantic]
            public Vector3 fsNormal;                                                                                                             
            [TextureCoordinateSemantic]
            public Vector3 fsColor;
            [TextureCoordinateSemantic]
            public Vector3 fsEyePos;
            [TextureCoordinateSemantic]
            public Vector3 fsLightVec;
        }


        [VertexShader]
        public FragmentInput VS(VertexInput input)
        {
            FragmentInput output;
            Vector4 v4Pos = new Vector4(input.Position * 
                input.InstData0.W * 0.5f + new Vector3(input.InstData0.X, input.InstData0.Y, input.InstData0.Z), 1);
            output.fsNormal = input.Normal;
            output.fsColor = new Vector3(input.InstData1.X, input.InstData1.Y, input.InstData1.Z);
            output.Position = Vector4.Transform(v4Pos, (t.Projection * t.View * t.Model));
            Vector4 eyePos = Vector4.Transform(v4Pos, t.View * t.Model);
            output.fsEyePos = new Vector3(eyePos.X, eyePos.Y, eyePos.Z);
            Vector4 eyeLightPos = Vector4.Transform(t.LightPos, t.View);
            output.fsLightVec = Vector3.Normalize(new Vector3(t.LightPos.X, t.LightPos.Y, t.LightPos.Z) - output.fsEyePos);
            return output;
        }

        static Vector3 Reflect(Vector3 i, Vector3 n)
        {
            return i - 2 * n * Vector3.Dot(i, n);
        }
        [FragmentShader]
        public Vector4 FS(FragmentInput input)
        {
            Vector3 eye = Vector3.Normalize(-input.fsEyePos);
            Vector3 nrm = Vector3.Normalize(input.fsNormal);
            Vector3 reflected = Vector3.Normalize(Reflect(-input.fsLightVec, nrm));
            float diff = ShaderBuiltins.Clamp(Vector3.Dot(nrm, input.fsLightVec), 0, 1);
            float ambient = 0.2f;
            float specular = 0.75f;
            Vector4 specvec = new Vector4(0, 0, 0, 0);
            if (Vector3.Dot(input.fsEyePos, nrm) < 0)
            {
                specvec = new Vector4(0.5f, 0.5f, 0.5f, 1.0f) * (float)ShaderBuiltins.Pow(ShaderBuiltins.Clamp(Vector3.Dot(reflected, eye), 0, 100000), 16.0f) *
                    specular;
            }

            float mul = ambient + (1 - ambient) * diff;
            return (new Vector4(input.fsColor, 1) * mul) + specvec;
        }
    }
}

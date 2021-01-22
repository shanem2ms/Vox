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

        [FragmentShader]
        public Vector4 FS(FragmentInput input)
        {
            /*
                vec3 Eye = normalize(-fsin_eyePos);
                vec3 Reflected = normalize(reflect(-fsin_lightVec, fsin_normal));
                vec4 IAmbient = vec4(0.1f, 0.1f, 0.1f, 1.0f);
                float diff = clamp(dot(fsin_normal, fsin_lightVec), 0.f, 100000);
                vec4 IDiffuse = vec4(diff, diff, diff, diff);
                float specular = 0.75f;
                vec4 ISpecular = vec4(0.0f, 0.0f, 0.0f, 0.0f);
                if (dot(fsin_eyePos, fsin_normal) < 0.0)
                {
                    ISpecular = (vec4(0.5f, 0.5f, 0.5f, 1.0f) * pow(clamp(dot(Reflected, Eye), 0.0f, 100000), 16.0f)) * specular;
                }

                fsout_color = (IAmbient + IDiffuse) * vec4(fsin_color, 1.0f) + ISpecular;
             

            //Vector3 Eye = Vector3.Normalize(-input.fsEyePos);
              */
            return new Vector4(new Vector3(1,1, 0), 1);
        }
    }
}

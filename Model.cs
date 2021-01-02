using ObjLoader.Loader.Loaders;
using System.IO;
using System;
using System.Linq;
using GLObjects;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.ES30;

namespace Vox
{
    class Model
    {
        class MatProvider : IMaterialStreamProvider
        {
            string matPath;
            public MatProvider(string path)
            { matPath = path; }
            public Stream Open(string materialFilePath)
            {
                return File.Open(Path.Combine(matPath, materialFilePath), FileMode.Open);
            }
        }

        LoadResult result;
        VertexArray modelVA;
        Program _Program;
        RenderTarget[] sides = new RenderTarget[] { new RenderTarget(Size, Size, new TextureR32()),
            new RenderTarget(Size, Size, new TextureR32()),
            new RenderTarget(Size, Size, new TextureR32()),
            new RenderTarget(Size, Size, new TextureR32()),
            new RenderTarget(Size, Size, new TextureR32()),
            new RenderTarget(Size, Size, new TextureR32()) };
        float[][] buf = null;

        static int Size = 1024;

        public Model(string path)
        {
            var objLoaderFactory = new ObjLoaderFactory();
            var objLoader = objLoaderFactory.Create(new MatProvider(Path.GetDirectoryName(path)));
            var fileStream = File.Open(path, FileMode.Open);
            result = objLoader.Load(fileStream);
        }


        public Oct BuildOct(int minLod, int maxLod)
        {
            this.RenderCubes();
            return new Oct(minLod, maxLod, buf, Model.Size);
        }

        VertexArray Load()
        {
            _Program = Registry.Programs["depth"];
            List<uint> indices = new List<uint>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();

            foreach (var g in result.Groups)
            {
                foreach (var f in g.Faces)
                {
                    int[] idx = { 0, 1, 2, 2, 3, 0 };
                    int ct = f.Count == 4 ? 6 : 3;
                    for (int i = 0; i < ct; ++i)
                    {
                        indices.Add((uint)vertices.Count);
                        var vertex = result.Vertices[f[idx[i]].VertexIndex - 1];
                        vertices.Add(new Vector3(vertex.X, vertex.Y, vertex.Z));
                        if (result.Normals.Count > 0)
                        {
                            var normal = result.Normals[f[idx[i]].NormalIndex - 1];
                            normals.Add(new Vector3(normal.X, normal.Y, normal.Z));
                        }
                    }
                }
            }

            Vector3 min = vertices[0];
            Vector3 max = vertices[0];
            foreach (var v in vertices)
            {
                min.X = Math.Min(min.X, v.X);
                min.Y = Math.Min(min.Y, v.Y);
                min.Z = Math.Min(min.Z, v.Z);
                max.X = Math.Max(max.X, v.X);
                max.Y = Math.Max(max.Y, v.Y);
                max.Z = Math.Max(max.Z, v.Z);
            }

            Vector3 scl = max - min;
            Vector3 ori = (max + min) * 0.5f;
            float md = Math.Max(scl.X, Math.Max(scl.Y, scl.Z));
            md = 2.0f / md;
            for (int idx = 0; idx < vertices.Count; ++idx)
            {
                vertices[idx] = (vertices[idx] - ori) * md;
            }
            return new VertexArray(_Program, vertices.ToArray(), indices.ToArray(), null, normals.Count > 0 ? normals.ToArray() :
                null);
        }
        public void RenderCubes()
        {
            if (modelVA == null)
                modelVA = Load();

            _Program.Use(0);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            Matrix4 projection = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 viewProj = view.Inverted() * projection;

            Matrix4[] rots = new Matrix4[]
            {
                Matrix4.Identity,
                Matrix4.CreateRotationX((float)(Math.PI * 0.5)),
                Matrix4.CreateRotationY((float)(Math.PI * 0.5)),
            };

            if (buf == null)
                buf = new float[6][];
            for (int i = 0; i < 6; ++i)
            {
                sides[i].Use();
                GL.Clear(ClearBufferMask.ColorBufferBit);
                Matrix4 model = rots[i / 2];
                if ((i & 1) == 0)
                {
                    GL.ClearDepth(1.0f);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                }
                else
                {
                    GL.ClearDepth(0.0f);
                    GL.DepthFunc(DepthFunction.Gequal);
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                }
                _Program.SetMVP(model, viewProj);
                modelVA.Draw();
                GL.Finish();
                if (buf[i] == null)
                    buf[i] = new float[sides[i].Width * sides[i].Height];
                GL.ReadPixels<float>(0, 0, sides[i].Width, sides[i].Height, PixelFormat.Red, PixelType.Float, buf[i]);
                GLErr.Check();
            }

            GL.ClearDepth(1.0f);
            GL.DepthFunc(DepthFunction.Less);
            FrameBuffer.BindNone();
        }


        public void DebugDraw()
        {
            for (int i = 0; i < 6; ++i)
            {
                float x = (i % 3) * 2.0f / 3.0f;
                float y = 1 - (i / 3);
                x -= 1.0f;
                y -= 1.0f;
                sides[i].Draw(new Vector4(x, y, 2 / 3.0f, 1));
            }
        }
    }

}

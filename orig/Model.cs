using System;
using System.Linq;
using GLObjects;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.ES30;
using ai = Assimp;
using aic = Assimp.Configs;

namespace Vox
{
    class Model
    {
        VertexArray modelVA;
        Program _Program;
        RenderTarget[] depthcube = new RenderTarget[] { new RenderTarget(Size, Size, new TextureRgba128()),
            new RenderTarget(Size, Size, new TextureRgba128()),
            new RenderTarget(Size, Size, new TextureRgba128()),
            new RenderTarget(Size, Size, new TextureRgba128()),
            new RenderTarget(Size, Size, new TextureRgba128()),
            new RenderTarget(Size, Size, new TextureRgba128()) };
        Rgba32[][] buf = null;
        ai.Scene model;

        static int Size = 1024;

        public Model(string path)
        {
            ai.AssimpContext importer = new ai.AssimpContext();
            importer.SetConfig(new aic.NormalSmoothingAngleConfig(66.0f));
            model = importer.ImportFile(path, ai.PostProcessPreset.TargetRealTimeMaximumQuality);
        }


        public Oct BuildOct(int minLod, int maxLod)
        {
            this.RenderCubes();
            Oct o = new Oct(minLod, maxLod, buf, Model.Size);
            List<Oct> leafs = new List<Oct>();
            o.GetLeafNodes(leafs);
            o.Collapse();
            Oct.clocs.Sort();
            leafs.Clear();
            o.GetLeafNodes(leafs);
            return o;
        }

        VertexArray Load()
        {
            _Program = Registry.Programs["depth"];
            List<uint> indices = new List<uint>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector3> colors = new List<Vector3>();

            foreach (var g in model.Meshes)
            {
                foreach (var f in g.Faces)
                {
                    if (f.IndexCount < 3)
                        continue;
                    if (f.IndexCount > 3)
                        System.Diagnostics.Debugger.Break();
                    int[] idx = { 0, 1, 2, 2, 3, 0 };
                    int ct = f.IndexCount == 4 ? 6 : 3;
                    for (int i = 0; i < ct; ++i)
                    {
                        indices.Add((uint)vertices.Count);
                        var vertex = g.Vertices[f.Indices[idx[i]]];
                        vertices.Add(new Vector3(vertex.X, vertex.Y, vertex.Z));                        
                        if (g.HasNormals)
                        {
                            var normal = g.Normals[f.Indices[idx[i]]];
                            normals.Add(new Vector3(normal.X, normal.Y, normal.Z));
                        }
                        if (g.HasVertexColors(0))
                        {
                            var color = g.VertexColorChannels[0][f.Indices[idx[i]]];
                            colors.Add(new Vector3(color.R, color.G, color.B));
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
            return new VertexArray(_Program, vertices.ToArray(), indices.ToArray(), colors.ToArray(), normals.Count > 0 ? normals.ToArray() :
                null);
        }

        public void RenderCubes()
        {
            if (modelVA == null)
                modelVA = Load();

            _Program.Use(0);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

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
                buf = new Rgba32[6][];
            for (int i = 0; i < 6; ++i)
            {
                depthcube[i].Use();
                GL.Clear(ClearBufferMask.ColorBufferBit);
                Matrix4 model = rots[i / 2];
                if ((i & 1) == 0)
                {
                    GL.ClearDepth(1.0f);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    GL.CullFace(CullFaceMode.Front);
                }
                else
                {
                    GL.ClearDepth(0.0f);
                    GL.DepthFunc(DepthFunction.Gequal);
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    GL.CullFace(CullFaceMode.Back);
                }
                _Program.SetMVP(model, viewProj);
                modelVA.Draw();
                GL.Finish();
                if (buf[i] == null)
                    buf[i] = new Rgba32[depthcube[i].Width * depthcube[i].Height];
                GL.ReadPixels<Rgba32>(0, 0, depthcube[i].Width, depthcube[i].Height, PixelFormat.Rgba, PixelType.Float, buf[i]);
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
                depthcube[i].Draw(new Vector4(x, y, 2 / 3.0f, 1));
            }
        }
    }

}

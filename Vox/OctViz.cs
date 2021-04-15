using Common;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using SampleBase;
using System.Collections.Generic;
using Veldrid.SPIRV;
using System.Runtime.InteropServices;

namespace Vox
{
    class OctVizBlocks
    {
        public TextureView _view;
        public Framebuffer _FB;
        public Pipeline _pipeline;
        private DeviceBuffer cbufferTransform;
        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        private DeviceBuffer instanceBuffer;
        private ResourceSet resourceSet;
        uint indexCount;
        uint instanceCount;
        DeviceBuffer cubeBuffer;


        public struct VertexInfo
        {
            public static uint Size { get; } = (uint)Unsafe.SizeOf<VertexInfo>();

            public Vector3 Position;
            public Vector3 Normal;

            public VertexInfo(Vector3 position, Vector3 normal)
            {
                Position = position;
                Normal = normal;
            }
        }

        public struct InstanceInfo
        {
            public static uint Size { get; } = (uint)Unsafe.SizeOf<InstanceInfo>();

            public Vector4 Inst0;
            public Vector4 Inst1;

            public InstanceInfo(Vector4 inst0, Vector4 inst1)
            {
                Inst0 = inst0;
                Inst1 = inst1;
            }
        }

        public OctVizBlocks(uint cubecnt, DeviceBuffer _cubeBuffer)
        {
            instanceBuffer = _cubeBuffer;
            VertexArray va = BuildVA();
            VertexInfo[] vtx = new VertexInfo[va._positions.Length];
            for (int idx = 0; idx < vtx.Length; ++idx)
            {
                vtx[idx] = new VertexInfo(va._positions[idx],va._normals[idx]);
            }

            vertexBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(VertexInfo.Size * vtx.Length), BufferUsage.VertexBuffer));
            Utils.G.UpdateBuffer(vertexBuffer, 0, vtx);
            indexBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(sizeof(uint) * va._elems.Length), BufferUsage.IndexBuffer));
            Utils.G.UpdateBuffer(indexBuffer, 0, va._elems);

            indexCount = (uint)va._elems.Length;
            instanceCount = cubecnt;
            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                         new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                         new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));
                                                                                                                                                                     
            VertexLayoutDescription instLayout = new VertexLayoutDescription(
                         new VertexElementDescription("InstData0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));
            instLayout.InstanceStepRate = 1;

            ResourceLayout cbLayout = Utils.Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
                           
            ShaderSetDescription instShaders = new ShaderSetDescription(
                new[] { vertexLayout, instLayout },
                Utils.Factory.CreateFromSpirv(
                new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Inst-vertex"), "main"),
                new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Inst-fragment"), "main")));

            GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
                PrimitiveTopology.TriangleList,
                instShaders,
                cbLayout,
                Utils.G.SwapchainFramebuffer.OutputDescription);

            _pipeline = Utils.Factory.CreateGraphicsPipeline(pd);
            cbufferTransform = Utils.Factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Shaders.Inst.Transform>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            resourceSet = Utils.Factory.CreateResourceSet(new ResourceSetDescription(cbLayout, cbufferTransform));
        }


        public void UpdateUniformBuffer(Camera c)
        {
            Vox.Shaders.Inst.Transform ui = new Vox.Shaders.Inst.Transform { LightPos = new Vector4(0, 0, 0, 1) };
            ui.Projection = c.ProjectionMatrix;
            ui.View = c.ViewMatrix;
            ui.Model = Matrix4x4.CreateTranslation(-0.5f, -0.5f, -0.5f) * Matrix4x4.CreateScale(10.0f);
            Utils.G.UpdateBuffer(cbufferTransform, 0, ref ui);
        }

        public void Draw(Camera c)
        {
            UpdateUniformBuffer(c);
            Utils.Cl.SetPipeline(_pipeline);
            Utils.Cl.SetGraphicsResourceSet(0, resourceSet);
            Utils.Cl.SetVertexBuffer(0, vertexBuffer);
            Utils.Cl.SetIndexBuffer(indexBuffer, IndexFormat.UInt32);
            Utils.Cl.SetVertexBuffer(1, instanceBuffer);
            Utils.Cl.DrawIndexed(indexCount, instanceCount, 0, 0, 0);
        }



        public static VertexArray BuildVA()
        {
            uint[] indices = new uint[_Cube.Length];
            Vector3[] texCoords = new Vector3[_Cube.Length];
            Vector3[] normals = new Vector3[3]
            {
                Vector3.UnitZ,
                Vector3.UnitY,
                Vector3.UnitX
            };
            Vector3[] xdirs = new Vector3[3]
            {
                Vector3.UnitX,
                Vector3.UnitX,
                Vector3.UnitZ
            };
            Vector3[] ydirs = new Vector3[3]
            {
                Vector3.UnitY,
                Vector3.UnitZ,
                Vector3.UnitY
            };


            Vector3[] nrmCoords = new Vector3[_Cube.Length];
            int sides = _Cube.Length / 6;
            for (int i = 0; i < sides; ++i)
            {
                Vector3 d1 = _Cube[i * 6 + 1] - _Cube[i * 6];
                Vector3 d2 = _Cube[i * 6 + 2] - _Cube[i * 6 + 1];
                Vector3 nrm = Vector3.Normalize(Vector3.Cross(d1, d2));
                for (int nIdx = 0; nIdx < 6; ++nIdx)
                {
                    nrmCoords[i * 6 + nIdx] = nrm;
                }
            }

            for (int i = 0; i < indices.Length; ++i)
            {
                indices[i] = (uint)i;
                Vector3 xdir = xdirs[i / 12];
                Vector3 ydir = ydirs[i / 12];
                int sideIdx = i / 6;
                texCoords[i] = new Vector3(Vector3.Dot(_Cube[i], xdir),
                    Vector3.Dot(_Cube[i], ydir), (float)sideIdx / 6.0f);
            }

            return new VertexArray(_Cube, indices, nrmCoords);
        }


        private static readonly Vector3[] _Cube = new Vector3[] {
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(-1.0f, -1.0f, 1.0f),  // 4
            new Vector3(1.0f, -1.0f, 1.0f),  // 5
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(-1.0f, -1.0f, 1.0f),  // 4
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, -1.0f, 1.0f),  // 5

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, -1.0f, 1.0f),  // 5
            new Vector3(-1.0f, -1.0f, 1.0f),  // 4

            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7

            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(-1.0f, -1.0f, 1.0f),  // 4
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(1.0f, -1.0f, 1.0f),  // 5          
        };


    };


}


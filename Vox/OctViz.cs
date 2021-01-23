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

        public OctVizBlocks(VertexArray va)
        {
            VertexInfo[] vtx = new VertexInfo[va._positions.Length];
            for (int idx = 0; idx < vtx.Length; ++idx)
            {
                vtx[idx] = new VertexInfo(va._positions[idx],va._normals[idx]);
            }
            InstanceInfo[] inst = new InstanceInfo[va._instanceData0.Length];
            for (int idx = 0; idx < inst.Length; ++idx)
            {
                inst[idx] = new InstanceInfo(va._instanceData0[idx], va._instanceData1[idx]);
            }

            vertexBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(VertexInfo.Size * vtx.Length), BufferUsage.VertexBuffer));
            Utils.G.UpdateBuffer(vertexBuffer, 0, vtx);
            indexBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(sizeof(uint) * va._elems.Length), BufferUsage.IndexBuffer));
            Utils.G.UpdateBuffer(indexBuffer, 0, va._elems);
            instanceBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(InstanceInfo.Size * inst.Length), BufferUsage.VertexBuffer));
            Utils.G.UpdateBuffer(instanceBuffer, 0, inst);

            indexCount = (uint)va._elems.Length;
            instanceCount = (uint)inst.Length;
            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                         new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                         new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));
                                                                                                                                                                     
            VertexLayoutDescription instLayout = new VertexLayoutDescription(
                         new VertexElementDescription("InstData0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
                         new VertexElementDescription("InstData1", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));
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
            ui.Model = Matrix4x4.CreateScale(10);
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

    };
}


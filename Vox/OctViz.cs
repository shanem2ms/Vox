using Common;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using System.Collections.Generic;
using Veldrid.SPIRV;
using System.Runtime.InteropServices;

namespace Vox
{
    class OctVizBlocks
    {
        private Texture _color;
        private Texture _staging;
        public TextureView _view;
        public Framebuffer _FB;
        public Pipeline _pipeline;
        private DeviceBuffer cbufferTransform;
        private DeviceBuffer vertexBuffer;
        private DeviceBuffer indexBuffer;
        public Rgba32[] _pixelData;

        int idx;
        struct Vertex
        {
            public Vector3 Pos;
            public Vector3 Nrm;
        }

        public OctVizBlocks(VertexArray va)
        {
            Vertex[] vtx = new Vertex[va._positions.Length];
            for (int idx = 0; idx < vtx.Length; ++idx)
            {
                vtx[idx] = new Vertex() { Pos = va._positions[idx], Nrm = va._normals[idx] };
            }
            vertexBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(24 * vtx.Length), BufferUsage.VertexBuffer));
            Utils.G.UpdateBuffer(vertexBuffer, 0, vtx);
            indexBuffer = Utils.Factory.CreateBuffer(new BufferDescription((uint)(sizeof(uint) * va._elems.Length), BufferUsage.VertexBuffer));
            Utils.G.UpdateBuffer(indexBuffer, 0, va._elems);

            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                         new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                         new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

            VertexLayoutDescription instLayout = new VertexLayoutDescription(
                         new VertexElementDescription("InstData0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
                         new VertexElementDescription("InstData1", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));

            ResourceLayout cbLayout = Utils.Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
                           
            ShaderSetDescription instShaders = new ShaderSetDescription(
                new[] { vertexLayout },
                Utils.Factory.CreateFromSpirv(
                new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Inst-vertex"), "main"),
                new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Inst-fragment"), "main")));
        }

    };
}


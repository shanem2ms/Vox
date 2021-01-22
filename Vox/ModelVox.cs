using Common;
using SampleBase;
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
    class ModelVox
    {
        public class Side
        {
            private Texture _color;
            private Texture _staging;
            public TextureView _view;
            public Framebuffer _FB;
            public Pipeline _pipeline;
            private DeviceBuffer cbufferTransform;
            private ResourceSet depthResourceSet;
            public Rgba32[] _pixelData;

            int idx;

            public Side(int _idx, ResourceFactory factory, uint width, uint height,
                ShaderSetDescription depthShaders, ResourceLayout depthLayout)
            {
                idx = _idx;
                bool frontOrBack = (idx & 1) == 0;
                _color = factory.CreateTexture(TextureDescription.Texture2D(
                                width, height, 1, 1,
                                 PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget | TextureUsage.Sampled));
                _view = factory.CreateTextureView(_color);
                Texture offscreenDepth = factory.CreateTexture(TextureDescription.Texture2D(
                    width, height, 1, 1, PixelFormat.D24_UNorm_S8_UInt, TextureUsage.DepthStencil));
                _FB = factory.CreateFramebuffer(new FramebufferDescription(offscreenDepth, _color));
                GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
                    BlendStateDescription.SingleOverrideBlend,
                    frontOrBack ? DepthStencilStateDescription.DepthOnlyLessEqual : DepthStencilStateDescription.DepthOnlyGreaterEqual,
                    new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
                    PrimitiveTopology.TriangleList,
                    depthShaders,
                    depthLayout,
                    _FB.OutputDescription);
                _pipeline = factory.CreateGraphicsPipeline(pd);
                cbufferTransform = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Shaders.Depth.Transform>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
                depthResourceSet = factory.CreateResourceSet(new ResourceSetDescription(depthLayout, cbufferTransform));

                _staging = factory.CreateTexture(TextureDescription.Texture2D(
                                width, height, 1, 1,
                                 PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));
                _pixelData = new Rgba32[_staging.Width * _staging.Height]; 
            }

            public void UpdateUniformBufferOffscreen()
            {
                Vox.Shaders.Depth.Transform ui = new Vox.Shaders.Depth.Transform { LightPos = new Vector4(0, 0, 0, 1) };

                ui.Projection = Matrix4x4.CreateScale(1, 1, 0.5f) *
                    Matrix4x4.CreateTranslation(0, 0, 0.5f);

                ui.View = Matrix4x4.Identity;
                ui.Model = Matrix4x4.Identity;


                Matrix4x4[] rots = new Matrix4x4[]
                {
                Matrix4x4.Identity,
                Matrix4x4.CreateRotationX((float)(Math.PI * 0.5)),
                Matrix4x4.CreateRotationY((float)(Math.PI * 0.5)),
                };

                ui.Model = rots[idx / 2];
                Utils.G.UpdateBuffer(cbufferTransform, 0, ref ui);
            }

            public void CopyTexture()
            {
                Utils.Cl.CopyTexture(_color, _staging);
                
                // When a texture is mapped into a CPU-visible region, it is often not laid out linearly.
                // Instead, it is laid out as a series of rows, which are all spaced out evenly by a "row pitch".
                // This spacing is provided in MappedResource.RowPitch.

                // It is also possible to obtain a "structured view" of a mapped data region, which is what is done below.
                // With a structured view, you can read individual elements from the region.
                // The code below simply iterates over the two-dimensional region and places each texel into a linear buffer.
                // ImageSharp requires the pixel data be contained in a linear buffer.
                MappedResourceView<Rgba32> map = Utils.G.Map<Rgba32>(_staging, MapMode.Read);

                // Rgba32 is synonymous with PixelFormat.R8_G8_B8_A8_UNorm.
                for (int y = 0; y < _staging.Height; y++)
                {
                    for (int x = 0; x < _staging.Width; x++)
                    {
                        int index = (int)(y * _staging.Width + x);
                        _pixelData[index] = map[x, y];
                    }
                }
                Utils.G.Unmap(_staging); 
            }

            public void Draw()
            {
                bool frontOrBack = (idx & 1) == 0;
                UpdateUniformBufferOffscreen();
                Utils.Cl.SetFramebuffer(_FB);
                Utils.Cl.SetFullViewports();
                Utils.Cl.ClearColorTarget(0, RgbaFloat.Black);
                Utils.Cl.ClearDepthStencil(frontOrBack ? 1f : 0f);

                Utils.Cl.SetPipeline(_pipeline);
                Utils.Cl.SetGraphicsResourceSet(0, depthResourceSet);
            }
        };


        private Model _model;
        Side[] sides;

        TextureView[] views = null;
        public TextureView []View => views;
        static uint Size = 1024;

        public ModelVox(string model)
        {
            using (Stream modelStream = File.OpenRead(model))
            {
                _model = new Model(
                    Utils.G,
                    Utils.Factory,
                    modelStream,
                    "ply",
                    new[] { VertexElementSemantic.Position, VertexElementSemantic.TextureCoordinate, VertexElementSemantic.Color, VertexElementSemantic.Normal },
                    new Model.ModelCreateInfo(new Vector3(1, 1, 1), Vector2.One, Vector3.Zero));
            }

            CreateResource(Utils.Factory, ModelVox.Size, ModelVox.Size);

        }


        void CreateResource(ResourceFactory factory, uint width, uint height)
        {

            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                         new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                         new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                         new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                         new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));
                                                                                                                                                 
            ResourceLayout depthLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
            ShaderSetDescription depthShaders = new ShaderSetDescription(
                new[] { vertexLayout },
                factory.CreateFromSpirv(
                new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Depth-vertex"), "main"),
                new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Depth-fragment"), "main")));

            sides = new Side[6];
            views = new TextureView[6];
            for (int i = 0; i < 6; ++i)
            {
                sides[i] = new Side(i, factory, width, height, depthShaders, depthLayout);
                views[i] = sides[i]._view;
            }

        }    


        public void DrawOffscreen()
        {
            for (int idx = 0; idx < 6; ++idx)
            {
                sides[idx].Draw();
                Utils.Cl.SetVertexBuffer(0, _model.VertexBuffer);
                Utils.Cl.SetIndexBuffer(_model.IndexBuffer, IndexFormat.UInt32);
                Utils.Cl.DrawIndexed(_model.IndexCount, 1, 0, 0, 0);                
            }
        }

        public Oct BuildOct(int minLod, int maxLod)
        {
            for (int idx = 0; idx < 6; ++idx)
            {
                sides[idx].CopyTexture();
            }
            if (sides[0]._pixelData[0].a == 1)
            {
                Rgba32[][] buf = new Rgba32[][] { sides[0]._pixelData,
                sides[1]._pixelData,
                sides[2]._pixelData,
                sides[3]._pixelData,
                sides[4]._pixelData,
                sides[5]._pixelData };
                Oct o = new Oct(minLod, maxLod, buf, (int)ModelVox.Size);
                List<Oct> leafs = new List<Oct>();
                o.GetLeafNodes(leafs);
                o.Collapse();
                Oct.clocs.Sort();
                leafs.Clear();
                o.GetLeafNodes(leafs);
                return o;
            }
            else
                return null;
        }
    }
}

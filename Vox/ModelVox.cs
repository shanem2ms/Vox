using Common;
using System;
using System.Linq;
using System.IO;
using System.Numerics;
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
            TextureView _view;
            public Framebuffer _FB;
            public Pipeline _pipeline;
            private DeviceBuffer cbufferTransform;
            private ResourceSet []depthResourceSets;
            public MMTex _pixelData;
            DownScale[] _mips;
            Texture[] _staging;

            public TextureView[] View => _mips.Select(m => m.View).ToArray();

            int idx;

            public Side(int _idx, ResourceFactory factory, uint width, uint height,
                ShaderSetDescription depthShaders, ResourceLayout depthLayout, DeviceBuffer[] materials)
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
                depthResourceSets = new ResourceSet[materials.Length];
                for (int idx = 0; idx < depthResourceSets.Length; ++idx)
                {
                    depthResourceSets[idx] = factory.CreateResourceSet(new ResourceSetDescription(depthLayout, cbufferTransform, materials[idx]));
                }

                int levels = (int)Math.Log2(width) - 2;
                _mips = new DownScale[levels];
                TextureView curView = _view;
                for (int idx = 0; idx < levels; ++idx)
                {
                    _mips[idx] = new DownScale();
                    _mips[idx].CreateResources(factory, curView, width >> (idx + 1), height >> (idx + 1));
                    curView = _mips[idx].View;
                }
            }

            public void UpdateUniformBufferOffscreen()
            {
                Vox.Shaders.Depth.Transform ui = new Vox.Shaders.Depth.Transform { LightPos = new Vector4(0, 0, 0, 1)};
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

            public void CopyMips()
            {
                if (_pixelData == null)
                {
                    _pixelData = new MMTex(2, _mips.Length + 1);
                    _pixelData.baseLod = 2;
                    _staging = new Texture[_mips.Length + 1];
                    for (int idx = 0; idx < _pixelData.Length; ++idx)
                    {
                        Texture tex = idx == 0 ? _color : _mips[idx - 1].OutTexture;
                        _staging[idx] = Utils.Factory.CreateTexture(TextureDescription.Texture2D(
                            tex.Width, tex.Height, 1, 1,
                             PixelFormat.R32_G32_B32_A32_Float, TextureUsage.Staging));
                        _pixelData[_pixelData.Length - idx - 1] = new Rgba32[tex.Width * tex.Height];
                    }
                }
                for (int idx = 0; idx < _pixelData.Length; ++idx)
                {
                    Texture tex = idx == 0 ? _color : _mips[idx - 1].OutTexture;
                    Utils.Cl.CopyTexture(tex, _staging[idx]);
                    CopyTexture(_staging[idx], _pixelData[_pixelData.Length - idx - 1]);
                }
            }

            public int LodCnt => _mips.Length + 1;
            public Texture TexAtLod(int lod)
            {
                return lod == 0 ? _color : _mips[lod - 1].OutTexture;
            }

            void CopyTexture(Texture tex, Rgba32[] pixelData)                 
            {
                MappedResourceView<Rgba32> map = Utils.G.Map<Rgba32>(tex, MapMode.Read);

                for (int y = 0; y < tex.Height; y++)
                {
                    for (int x = 0; x < tex.Width; x++)
                    {
                        int index = (int)(y * tex.Width + x);
                        pixelData[index] = map[x, y];
                    }
                }
                Utils.G.Unmap(tex);
            }

            public void Prepare()
            {
                bool frontOrBack = (idx & 1) == 0;
                Utils.Cl.SetFramebuffer(_FB);
                Utils.Cl.SetFullViewports();
                Utils.Cl.ClearColorTarget(0, RgbaFloat.Black);
                Utils.Cl.ClearDepthStencil(frontOrBack ? 1f : 0f);
                UpdateUniformBufferOffscreen();
                Utils.Cl.SetPipeline(_pipeline);
            }

            public void Draw(int idx)
            {
                Utils.Cl.SetGraphicsResourceSet(0, depthResourceSets[idx]);
            }

            public void BuildMips()
            {
                for (int idx = 0; idx < _mips.Length; ++idx)
                {
                    _mips[idx].Draw();
                }
            }

            public void WriteBmp(string path)
            {
                _pixelData.SaveTo(path);
            }
        };


        private Model _model;
        Side[] sides;

        TextureView[] views = null;
        public TextureView []View => views;
        static uint Size = 1024;
        DeviceBuffer[] materialCbs;
        ComputeOct computeOct = new ComputeOct();

        public ModelVox(string model)
        {
            string extension = System.IO.Path.GetExtension(model);
            extension = extension.Substring(1);
            using (Stream modelStream = File.OpenRead(model))
            {
                _model = new Model(
                    Utils.G,
                    Utils.Factory,
                    modelStream,
                    extension,
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
                                                                                                                                                 
            ResourceLayout depthLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription[] {
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Fragment) }));

            ShaderSetDescription depthShaders = new ShaderSetDescription(
                new[] { vertexLayout },
                factory.CreateFromSpirv(
                new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Depth-vertex"), "main"),
                new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Depth-fragment"), "main")));

            materialCbs = new DeviceBuffer[_model.Materials.Count];
            for (int idx = 0; idx < _model.Materials.Count; ++idx)
            {
                materialCbs[idx] = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Shaders.Depth.Material>(), BufferUsage.UniformBuffer));
                Shaders.Depth.Material m = new Shaders.Depth.Material();
                m.DiffuseColor = _model.Materials[idx].Color;
                Utils.G.UpdateBuffer(materialCbs[idx], 0, ref m);
            }

            sides = new Side[6];
            views = new TextureView[6];
            for (int i = 0; i < 6; ++i)
            {
                sides[i] = new Side(i, factory, width, height, depthShaders, depthLayout, materialCbs);
                views[i] = sides[i].View[0];
            }

            computeOct.CreateResources(factory, sides.Select(s => s.View).ToArray());
        }

        public void DrawOffscreen()
        {
            for (int idx = 0; idx < 6; ++idx)
            {
                sides[idx].Prepare();
                Utils.Cl.SetVertexBuffer(0, _model.VertexBuffer);
                Utils.Cl.SetIndexBuffer(_model.IndexBuffer, IndexFormat.UInt32);
                for (int pIdx = 0; pIdx < _model.Parts.Count; ++pIdx)
                {
                    sides[idx].Draw(pIdx);
                    var modelPart = _model.Parts[pIdx];
                    Utils.Cl.DrawIndexed(modelPart.indexCount, 1, modelPart.indexBase, 0, 0);
                }

                sides[idx].BuildMips();
            }
        }

        public OctBuffer BuildOct()
        {
            int lodCnt = sides[0].LodCnt;
            computeOct.RunCompute();
            /*
            for (int i = 0; i < 6; ++i)
                sides[i].CopyMips();
            if (sides[0]._pixelData[0][0].a == 1)
            {
                MMTex[] mmtex = new MMTex[] { sides[0]._pixelData,
                sides[1]._pixelData,
                sides[2]._pixelData,
                sides[3]._pixelData,
                sides[4]._pixelData,
                sides[5]._pixelData };
                OctBuffer buf = new OctBuffer();
                buf.Build(mmtex);
                
                return buf;
            }
            else*/
                return null;
        }
    }
}

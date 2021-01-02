using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Veldrid;

namespace Vox
{
    class DownScale
    {
        private Plane plane;
        private Pipeline _pipeline;
        private ResourceSet _resourceSet;
        private Texture _color;
        public TextureView _outView;
        public Framebuffer _FB;
        private DeviceBuffer cbufferSubsample;


        public TextureView View => _outView;
        public Texture OutTexture => _color;
        public DownScale()
        { }
        
        public void CreateResources(ResourceFactory factory, TextureView inputView, uint width, uint height) 
        {
            plane = new Plane();
            _color = factory.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1,
            PixelFormat.R32_G32_B32_A32_Float, TextureUsage.RenderTarget | TextureUsage.Sampled));
            _outView = factory.CreateTextureView(_color);
            _FB = factory.CreateFramebuffer(new FramebufferDescription(null, _color));

            GraphicsPipelineDescription mirrorPD = new GraphicsPipelineDescription(
               BlendStateDescription.SingleOverrideBlend,
               DepthStencilStateDescription.DepthOnlyLessEqual,
               new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
               PrimitiveTopology.TriangleList,
               Utils.DepthDownScale.ShaderDesc,
               Utils.DepthDownScale.ResourceLayout,
               _FB.OutputDescription);

            cbufferSubsample = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Shaders.DepthDownScale.Subsample>(), 
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            Sampler greateSampler = factory.CreateSampler(new SamplerDescription(SamplerAddressMode.Clamp, SamplerAddressMode.Clamp, 
                SamplerAddressMode.Clamp, SamplerFilter.MinPoint_MagPoint_MipPoint,
                ComparisonKind.Greater, 0, 0, 0, 0, SamplerBorderColor.OpaqueBlack));
            
            _resourceSet = factory.CreateResourceSet(new ResourceSetDescription(Utils.DepthDownScale.ResourceLayout,
                inputView,
                greateSampler,
                cbufferSubsample));            
            _pipeline = factory.CreateGraphicsPipeline(ref mirrorPD);
            Vox.Shaders.DepthDownScale.Subsample ss = new Shaders.DepthDownScale.Subsample() { ddx = 0.5f / width, ddy = 0.5f / height };
            Utils.G.UpdateBuffer(cbufferSubsample, 0, ref ss);
        }


        public void Draw()
        {
            Utils.Cl.SetFramebuffer(_FB);
            Utils.Cl.SetFullViewports();
            Utils.Cl.ClearColorTarget(0, RgbaFloat.Black);
            Utils.Cl.SetPipeline(_pipeline);
            Utils.Cl.SetGraphicsResourceSet(0, _resourceSet);
            Utils.Cl.SetVertexBuffer(0, plane.VertexBuffer);
            Utils.Cl.SetIndexBuffer(plane.IndexBuffer, IndexFormat.UInt32);
            Utils.Cl.DrawIndexed(plane.IndexCount, 1, 0, 0, 0);
        }
    }
}

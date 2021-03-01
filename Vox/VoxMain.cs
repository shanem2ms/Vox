using Common;
using SampleBase;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
namespace Vox
{
    public class VoxMain : SampleApplication
    {
        private const uint OffscreenWidth = 1024;
        private const uint OffscreenHeight = 1024;

        private Plane plane;
        private Pipeline _dbgPipeline;
        private DeviceBuffer[] blitTransform;
        private ResourceSet[] _dbgResourceSet;
        ModelVox modelVox;
        OctVizBlocks octVizBlocks;

        public VoxMain(ApplicationWindow window) : base(window)
        {
            _camera.Position = new Vector3(0, 0, 22f);
        }

        protected override void CreateResources(ResourceFactory factory)
        {
            Utils.CreateGraphics(GraphicsDevice, factory, Window.Width, Window.Height);

            plane = new Plane();

            GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleDisabled,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                Utils.Blit.ShaderDesc,
                Utils.Blit.ResourceLayout,
                GraphicsDevice.SwapchainFramebuffer.OutputDescription);

            GraphicsPipelineDescription mirrorPD = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
                PrimitiveTopology.TriangleList,
                Utils.Blit.ShaderDesc,
                Utils.Blit.ResourceLayout,
                GraphicsDevice.SwapchainFramebuffer.OutputDescription);
            _dbgPipeline = factory.CreateGraphicsPipeline(ref mirrorPD);

            modelVox = new ModelVox(@"C:\Users\shane\Downloads\tesla\model.dae");


            _dbgResourceSet = new ResourceSet[6];
            blitTransform = new DeviceBuffer[6];
            for (int i = 0; i < 6; ++i)
            {
                blitTransform[i] = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Shaders.Blit.Transform>(),
                    BufferUsage.UniformBuffer | BufferUsage.Dynamic));
                _dbgResourceSet[i] = factory.CreateResourceSet(new ResourceSetDescription(Utils.Blit.ResourceLayout,
                    modelVox.View[i],
                    GraphicsDevice.LinearSampler,
                    blitTransform[i]));                                   
            }


            plane = new Plane();
        }

        protected override void Draw(float deltaSeconds)
        {
            Utils.Cl.Begin();
            DrawMain();
            Utils.Cl.End();
            GraphicsDevice.SubmitCommands(Utils.Cl);
            if (Utils.FlushAtEnd)
            {
                Utils.G.WaitForIdle();
                Utils.FlushAtEnd = false;
            }
            GraphicsDevice.SwapBuffers();
        }

        Oct o = null;

        static int frame = 0;
        private void DrawMain()
        {
            if (frame == 0)
            {
                modelVox.DrawOffscreen();
                Utils.FlushAtEnd = true;
            }
            else if (o == null)
            {
                o = modelVox.BuildOct(4, 8);
                if (o != null)
                {
                    VertexArray va = OctViz.BuildVA(o);
                    octVizBlocks = new OctVizBlocks(va);
                }
            }
            Utils.Cl.SetFramebuffer(GraphicsDevice.SwapchainFramebuffer);
            Utils.Cl.SetFullViewports();
            Utils.Cl.ClearColorTarget(0, RgbaFloat.Black);
            Utils.Cl.ClearDepthStencil(1f);
            if (octVizBlocks != null)
            {
                octVizBlocks.Draw(_camera);
            }
            else
            {
                for (int i = 0; i < 6; ++i)
                {
                    UpdateUniformBuffers(i);
                    Utils.Cl.SetPipeline(_dbgPipeline);
                    Utils.Cl.SetGraphicsResourceSet(0, _dbgResourceSet[i]);
                    Utils.Cl.SetVertexBuffer(0, plane.VertexBuffer);
                    Utils.Cl.SetIndexBuffer(plane.IndexBuffer, IndexFormat.UInt32);
                    Utils.Cl.DrawIndexed(plane.IndexCount, 1, 0, 0, 0);
                }
            }
            frame++;
        }

        private void UpdateUniformBuffers(int i)
        {
            int x = i % 3;
            int y = i / 3;
            Shaders.Blit.Transform ui = new Shaders.Blit.Transform
            { MWP = Matrix4x4.CreateScale(new Vector3(2f / 3f, 1, 1)) * Matrix4x4.CreateTranslation(new Vector3(-1 + (x * 2f / 3f), -1 + y, 0)) };
            GraphicsDevice.UpdateBuffer(blitTransform[i], 0, ref ui);
        }



    }
}


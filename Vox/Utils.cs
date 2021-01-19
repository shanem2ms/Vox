using Common;
using SampleBase;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using Veldrid.SPIRV;


namespace Vox
{
    public interface IShader
    {
        ShaderSetDescription ShaderDesc { get; }
        ResourceLayout ResourceLayout { get; }
        VertexLayoutDescription VtxLayout { get; }
    }

    struct Rgba32
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }


    class Utils
    {

        private static CommandList _cl;
        public static CommandList Cl => _cl;
        public static GraphicsDevice G;
        public static ResourceFactory Factory;
        public static UInt32 ScreenWidth;
        public static UInt32 ScreenHeight;
        public static IShader Phong;
        public static IShader Blit;
        public static IShader Depth;

        public static void CreateGraphics(GraphicsDevice g, ResourceFactory factory, uint sw, uint sh)
        {
            _cl = factory.CreateCommandList();
            Factory = factory;
            G = g;
            ScreenHeight = sh;
            ScreenWidth = sw;
            Phong = new PhongShader();
            Blit = new BlitShader();
        }

        public static float DegreesToRadians(float degrees)
        {
            return degrees * (float)Math.PI / 180f;
        }

        public static byte[] LoadShaderBytes(GraphicsDevice _gd, string name)
        {
            string extension;
            switch (_gd.BackendType)
            {
                case GraphicsBackend.Direct3D11:
                    extension = "hlsl";
                    break;
                case GraphicsBackend.Vulkan:
                    extension = "450.glsl";
                    break;
                case GraphicsBackend.OpenGL:
                    extension = "330.glsl";
                    break;
                case GraphicsBackend.Metal:
                    extension = "metallib";
                    break;
                case GraphicsBackend.OpenGLES:
                    extension = "300.glsles";
                    break;
                default: throw new InvalidOperationException();
            }

            extension = "450.glsl";
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}"));
        }
    }

    class Plane
    {

        public DeviceBuffer VertexBuffer { get; private set; }
        public DeviceBuffer IndexBuffer { get; private set; }
        public uint IndexCount => (uint)_ArrayElems.Length;

        public Plane()
        {
            uint iBufferSize = (uint)_ArrayElems.Length * sizeof(UInt32);
            uint vBufferSize = (uint)_ArrayPositionTexCoord.Length * 6 * sizeof(float);
            VertexBuffer = Utils.Factory.CreateBuffer(new BufferDescription(vBufferSize, BufferUsage.VertexBuffer));
            IndexBuffer = Utils.Factory.CreateBuffer(new BufferDescription(iBufferSize, BufferUsage.IndexBuffer));

            Utils.G.UpdateBuffer(VertexBuffer, 0, ref _ArrayPositionTexCoord[0], vBufferSize);
            Utils.G.UpdateBuffer(IndexBuffer, 0, ref _ArrayElems[0], iBufferSize);

        }

        private static readonly Vector3[] _ArrayPositionTexCoord = new Vector3[] {
            new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 1.0f, 0.0f),  new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f),
        };

        private static readonly UInt32[] _ArrayElems = new UInt32[]
        {
            0, 1, 2, 2, 3, 0,
        };

    }

    class PhongShader : IShader
    {
        public PhongShader()
        {
            VtxLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

            ResourceLayout = Utils.Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ShaderDesc = new ShaderSetDescription(new[] { VtxLayout },
                    Utils.Factory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Phong-vertex"), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Phong-fragment"), "main")));
        }

        public ShaderSetDescription ShaderDesc { get; }

        public ResourceLayout ResourceLayout { get; }

        public VertexLayoutDescription VtxLayout { get; }
    }

    class BlitShader : IShader
    {
        public BlitShader()
        {
            VtxLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("UVW", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

            ResourceLayout = Utils.Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ShaderDesc = new ShaderSetDescription(new[] { VtxLayout },
                    Utils.Factory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Blit-vertex"), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Blit-fragment"), "main")));
        }

        public ShaderSetDescription ShaderDesc { get; }

        public ResourceLayout ResourceLayout { get; }

        public VertexLayoutDescription VtxLayout { get; }
    }

    class DepthShader : IShader
    {
        public DepthShader()
        {
            VtxLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

            ResourceLayout = Utils.Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ShaderDesc = new ShaderSetDescription(new[] { VtxLayout },
                    Utils.Factory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "Depth-vertex"), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "Depth-fragment"), "main")));
        }

        public ShaderSetDescription ShaderDesc { get; }

        public ResourceLayout ResourceLayout { get; }

        public VertexLayoutDescription VtxLayout { get; }
    }

    public class VertexArray
    {
        Vector3[] _positions;
        uint[] _elems;
        Vector3[] _normals;
        Vector4[] _instanceData0;
        Vector4[] _instanceData1;

        public VertexArray(Vector3[] positions, uint[] elems, Vector3[] normals,
            Vector4[] instanceData0, Vector4[] instanceData1)
        {
            _positions = positions;
            _elems = elems;
            _normals = normals;
            _instanceData0 = instanceData0;
            _instanceData1 = instanceData1;
        }
        
    }


}

using Common;
using SampleBase;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using Veldrid.SPIRV;
using System.Runtime.InteropServices;
using System.Collections.Generic;

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

        public override string ToString()
        {
            return $"{r} {g} {b} {a}";
        }
    }

    class MMTex
    {
        public Rgba32[][] data;
        public int baseLod = 0;

        public MMTex(int baseLod, int levels)
        {
            data = new Rgba32[levels][];
        }
        public Rgba32[] this[int i]
        {
            get => data[i];
            set { data[i] = value; }
        }
        public int Length => data.Length;

        public void SaveTo(string file)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                FileStream f = File.Create($"{file}_{i}.dat");
                byte[] bytes = getBytes(data[i]);
                f.Write(bytes, 0, bytes.Length);
                f.Close();
            }
        }

        byte[] getBytes<T>(T[] data)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] arr = new byte[size * data.Length];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            for (int i = 0; i < data.Length; ++i)
            {
                Marshal.StructureToPtr(data[i], ptr, true);
                Marshal.Copy(ptr, arr, i * size, size);
            }
            Marshal.FreeHGlobal(ptr);
            return arr;
        }
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
        public static IShader DepthDownScale;
        public static bool FlushAtEnd = false;

        public static void CreateGraphics(GraphicsDevice g, ResourceFactory factory, uint sw, uint sh)
        {
            _cl = factory.CreateCommandList();
            Factory = factory;
            G = g;
            ScreenHeight = sh;
            ScreenWidth = sw;
            Phong = new PhongShader();
            Blit = new BlitShader();
            DepthDownScale = new DepthDownScaleShader();
    }

        public static float DegreesToRadians(float degrees)
        {
            return degrees * (float)Math.PI / 180f;
        }

        public static byte[] LoadShaderBytes(GraphicsDevice _gd, string name)
        {
            string extension;
            extension = "glsl";
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}"));
        }

        public static byte[] LoadShaderBytesPP(GraphicsDevice _gd, string name, string pp)
        {

            return LoadShaderBytesPP(_gd, name, pp, "glsl");
        }
        public static byte[] LoadShaderBytesPP(GraphicsDevice _gd, string name, string pp, string extension)
        {
            string []lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}"));
            bool write = true;
            List<string> outlines = new List<string>();
            for (int idx = 0; idx < lines.Length; ++idx)
            {
                if (lines[idx].StartsWith("#if "))
                {
                    string symbol = lines[idx].Substring(4).Trim();
                    if (symbol != pp)
                        write = false;
                }
                else if (lines[idx].StartsWith("#endif"))
                {
                    write = true;
                }
                else if (write)
                {
                    outlines.Add(lines[idx]);
                }
            }

            string outstr = string.Join("\n", outlines);
            return System.Text.Encoding.ASCII.GetBytes(outstr);
        }

        public static byte[] LoadShaderBytes(string name)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name));
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


    class DepthDownScaleShader : IShader
    {
        public DepthDownScaleShader()
        {
            VtxLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("UVW", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

            ResourceLayout = Utils.Factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("UBO", ResourceKind.UniformBuffer, ShaderStages.Fragment)));

            ShaderDesc = new ShaderSetDescription(new[] { VtxLayout },
                    Utils.Factory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Utils.LoadShaderBytes(Utils.G, "DepthDownScale-vertex"), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Utils.LoadShaderBytes(Utils.G, "DepthDownScale-fragment"), "main")));
        }

        public ShaderSetDescription ShaderDesc { get; }

        public ResourceLayout ResourceLayout { get; }

        public VertexLayoutDescription VtxLayout { get; }
    }

    public class VertexArray
    {
        public Vector3[] _positions;
        public uint[] _elems;
        public Vector3[] _normals;

        public VertexArray(Vector3[] positions, uint[] elems, Vector3[] normals)
        {
            _positions = positions;
            _elems = elems;
            _normals = normals;
        }
        
    }


}

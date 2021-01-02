using System;
using OpenTK.Graphics.ES30;
using OpenTK;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GLObjects
{
    // Note: abstractions for drawing using programmable pipeline.

    public static class GLErr
    {
        public static void Check()
        {
            ErrorCode ec = GL.GetError();
            if (ec != ErrorCode.NoError)
                System.Diagnostics.Debugger.Break();
        }
    }
    /// <summary>
    /// Shader object abstraction.
    /// </summary>
    public class Shader : IDisposable
    {
        public Shader(ShaderType shaderType, string source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Create
            ShaderName = GL.CreateShader(shaderType);
            // Submit source code
            GL.ShaderSource(ShaderName, source);
            // Compile
            GL.CompileShader(ShaderName);
            // Check compilation status
            int compiled;

            GL.GetShader(ShaderName, ShaderParameter.CompileStatus, out compiled);
            if (compiled != 0)
                return;

            // Throw exception on compilation errors
            const int logMaxLength = 1024;

            string infolog = null;
            int infologLength;

            GL.GetShaderInfoLog(ShaderName, logMaxLength, out infologLength, out infolog);

            throw new InvalidOperationException($"unable to compile shader: {infolog}");
        }

        public readonly int ShaderName;

        public void Dispose()
        {
            GL.DeleteShader(ShaderName);
        }
    }

    /// <summary>
    /// Program abstraction.
    /// </summary>
    public class Program : IDisposable
    {
        public static Program FromFiles(string vtxPath, string pixPath)
        {
            return new Program(
                System.IO.File.ReadAllText("Shaders/" + vtxPath),
                System.IO.File.ReadAllText("Shaders/" + pixPath),
                System.IO.File.ReadAllText("Shaders/Pick.frag"));
        }
        public Program(string vertexSource, string fragmentSource, string pickSource)
        {
            // Create vertex and frament shaders
            // Note: they can be disposed after linking to program; resources are freed when deleting the program
            using (Shader vObject = new Shader(ShaderType.VertexShader, vertexSource))
            using (Shader fObject = new Shader(ShaderType.FragmentShader, fragmentSource))
            using (Shader pObject = new Shader(ShaderType.FragmentShader, pickSource))
            {
                data = new Data[2]
                {
                    new Data(vObject, fObject),
                    new Data(vObject, pObject),
                };
            }
        }

        public class Data
        {
            public readonly int pgm;
            public readonly int LocationPosition;
            public readonly int LocationTexCoord0;
            public readonly int LocationTexCoord1;
            public readonly int LocationTexCoord2;
            public readonly int LocationNormals;
            public readonly int InstanceData0;
            public readonly int InstanceData1;
            public Dictionary<string, int> shaderOffsets =
                new Dictionary<string, int>();

            public Data(Shader vert, Shader frag)
            {
                // Create program
                pgm = GL.CreateProgram();
                // Attach shaders
                GL.AttachShader(pgm, vert.ShaderName);
                GL.AttachShader(pgm, frag.ShaderName);
                // Link program
                GL.LinkProgram(pgm);

                // Check linkage status
                int linked;
                GL.GetProgram(pgm, GetProgramParameterName.LinkStatus, out linked);

                if (linked == 0)
                {
                    const int logMaxLength = 1024;
                    string infolog;
                    int infologLength;

                    GL.GetProgramInfoLog(pgm, 1024, out infologLength, out infolog);

                    throw new InvalidOperationException($"unable to link program: {infolog}");
                }

                // Get attributes locations
                if ((LocationPosition = GL.GetAttribLocation(pgm, "aPosition")) < 0)
                    throw new InvalidOperationException("no attribute aPosition");
                LocationTexCoord0 = GL.GetAttribLocation(pgm, "aTexCoord0");
                LocationTexCoord1 = GL.GetAttribLocation(pgm, "aTexCoord1");
                LocationTexCoord2 = GL.GetAttribLocation(pgm, "aTexCoord2");
                LocationNormals = GL.GetAttribLocation(pgm, "aNormal");
                InstanceData0 = GL.GetAttribLocation(pgm, "aInstData0");
                InstanceData1 = GL.GetAttribLocation(pgm, "aInstData1");
            }
        }

        Data[] data = new Data[2];
        public void Dispose()
        {
            GL.DeleteProgram(data[0].pgm);
            GL.DeleteProgram(data[1].pgm);
        }

        int dataIdx = 0;
        public int DataIdx => dataIdx;

        public void Use(int idx)
        {
            dataIdx = idx;
            GL.UseProgram(data[idx].pgm);
        }

        public Data D => data[dataIdx];

        public int GetLoc(string name)
        {
            int loc;
            if (!data[dataIdx].shaderOffsets.TryGetValue(name, out loc))
            {
                loc = GL.GetUniformLocation(data[dataIdx].pgm, name);
                data[dataIdx].shaderOffsets.Add(name, loc);
            }
            return loc;
        }

        public void SetMVP(Matrix4 model, Matrix4 viewProj)
        {
            Matrix4 mvp = model * viewProj;
            SetMat4("uMVP", ref mvp);
            Matrix4 matWorldInvT = model;
            SetMat4("uWorld", ref matWorldInvT);
            SetMat4("uWorldInvTranspose", ref matWorldInvT);
        }

        public void Set1(string name, float value)
        {
            GL.Uniform1(GetLoc(name), value);
        }

        public void Set1(string name, int value)
        {
            GL.Uniform1(GetLoc(name), value);
        }

        public void Set2(string name, Vector2 value)
        {
            GL.Uniform2(GetLoc(name), value);
        }

        public void Set3(string name, Vector3 value)
        {
            GL.Uniform3(GetLoc(name), value);
        }

        public void Set4(string name, Vector4 value)
        {
            GL.Uniform4(GetLoc(name), value);
        }

        public void SetMat4(string name, ref Matrix4 value)
        {
            int l = GetLoc(name);
            if (l >= 0) GL.UniformMatrix4(l, false, ref value);        
        }
    }

    public abstract class BufferBase : IDisposable
    {
        public int BufferName;

        public void Dispose()
        {
            GL.DeleteBuffer(BufferName);
        }

        public abstract void Update(object vectors);
    }

    public class Buffer<T> : BufferBase where T : struct
    {

        public static int SizeOf<S>() where S : struct
        {
            return Marshal.SizeOf(default(S));
        }

        public Buffer(T[] vectors)
        {
            // Generate a buffer name: buffer does not exists yet
            BufferName = GL.GenBuffer();
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(SizeOf<T>() * vectors.Length), vectors, BufferUsageHint.StaticDraw);
        }

        public override void Update(object vecs)
        {
            T[] vectors = vecs as T[];
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(SizeOf<T>() * vectors.Length), vectors, BufferUsageHint.StaticDraw);
        }
    }

    public class Texture : IDisposable
    {
        public readonly int TextureName;
        public int Width { get; protected set; }
        public int Height { get; protected set; }

        protected Texture()
        {
            TextureName = GL.GenTexture();
        }

        public void Dispose()
        {
            GL.DeleteTexture(TextureName);
        }

        public virtual void Create(int width, int height) { }
        public virtual void BindToIndex(int idx) { }
    }

    public class TextureFloat : Texture
    {
        public TextureFloat()
        {
        }
        public void LoadDepthFrame(int width, int height, float[] data)
        {
            Width = width;
            Height = height;
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.R8,
                width, height, 0, PixelFormat.Red, PixelType.Float,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public override void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }

    }

    public class TextureRgba : Texture
    {
        public TextureRgba()
        {
        }

        public override void Create(int width, int height)
        {
            LoadData(width, height, IntPtr.Zero);
        }

        public void LoadData(int width, int height, IntPtr data)
        {
            Width = width;
            Height = height;
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgba8,
                width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public override void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }
    }

    public class TextureRgba128 : Texture
    {
        public TextureRgba128()
        {
        }

        public override void Create(int width, int height)
        {
            LoadData(width, height, IntPtr.Zero);
        }

        public void LoadData(int width, int height, IntPtr data)
        {
            Width = width;
            Height = height;
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgba32f,
                width, height, 0, PixelFormat.Rgba, PixelType.Float,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public override void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }
    }

    public class TextureR32 : Texture
    {
        public TextureR32()
        {
        }

        public override void Create(int width, int height)
        {
            LoadData(width, height, IntPtr.Zero);
        }

        public void LoadData(int width, int height, IntPtr data)
        {
            Width = width;
            Height = height;
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.R32f,
                width, height, 0, PixelFormat.Red, PixelType.Float,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public override void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }
    }

    public class TextureR8 : Texture
    {
        public TextureR8()
        {
        }

        public void Create(int width, int height)
        {
            LoadData(width, height, null);
        }

        public void LoadData(int width, int height, byte[] data)
        {
            Width = width;
            Height = height;
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.R8,
                width, height, 0, PixelFormat.Red, PixelType.UnsignedByte,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public override void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }
    }

    public class DepthBuffer : IDisposable
    {
        public DepthBuffer()
        {
            RenderBufferName = GL.GenRenderbuffer();
        }

        public void Create(int width, int height)
        {
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferName);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.DepthComponent32f,
                width, height);
        }

        public readonly int RenderBufferName;
        public void Bind()
        {
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferName);
        }

        public void Dispose()
        {
            GL.DeleteRenderbuffer(RenderBufferName);
        }

    }

    public class FrameBuffer
    {
        public readonly int FrameBufferName;
        int viewportW = 0;
        int viewportH = 0;
        public FrameBuffer()
        {
            FrameBufferName = GL.GenFramebuffer();
        }

        public void Create(Texture[] textures, DepthBuffer db)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferName);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, db.RenderBufferName);

            viewportW = textures[0].Width;
            viewportH = textures[0].Height;
            DrawBufferMode[] drawBuffers = new DrawBufferMode[textures.Length];
            for (int i = 0; i < textures.Length; ++i)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0 + i, TextureTarget2d.Texture2D, textures[i].TextureName, 0);
                drawBuffers[i] = DrawBufferMode.ColorAttachment0 + i;
            }

            GL.DrawBuffers(textures.Length, drawBuffers);
            FramebufferErrorCode errorCode = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            FrameBuffer.BindNone();
        }

        public static void BindNone()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.Viewport(0, 0, mainViewportW, mainViewportH);
        }

        static int mainViewportW = 0;
        static int mainViewportH = 0;
        public static void SetViewPortSize(int width, int height)
        {
            mainViewportW = width;
            mainViewportH = height;

        }

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferName);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FrameBufferName);
            GL.Viewport(0, 0, viewportW, viewportH);
        }
    }

    public class RenderTarget
    {
        FrameBuffer fb;
        Texture tex;
        DepthBuffer depth;

        public Texture Tex => tex;

        public int Width => tex.Width;
        public int Height => tex.Height;

        public RenderTarget(int width, int height)
        {
            tex = new TextureRgba();
            tex.Create(width, height);
            depth = new DepthBuffer();
            depth.Create(width, height);
            fb = new FrameBuffer();
            fb.Create(new Texture[] { tex }, depth);
        }

        public RenderTarget(int width, int height, Texture _tex)
        {
            tex = _tex;
            tex.Create(width, height);
            depth = new DepthBuffer();
            depth.Create(width, height);
            fb = new FrameBuffer();
            fb.Create(new Texture[] { tex }, depth);
        }

        public void Use()
        {
            fb.Bind();
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
        }

        public void Draw(Vector4 offsetScl)
        {
            Registry.DrawRT(this, offsetScl);
        }
    }

    class TextureYUV : IDisposable
    {
        public TextureYUV()
        {
            TextureNameY = GL.GenTexture();
            TextureNameUV = GL.GenTexture();
        }

        public delegate void OnGlErrorDel();
        public void LoadImageFrame(int width, int height, byte[] data)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameY);
            int ySize = height * width;
            byte[] yData = new byte[ySize];
            System.Buffer.BlockCopy(data, 0, yData, 0, ySize);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.R8,
                width, height, 0, PixelFormat.Red, PixelType.UnsignedByte,
                yData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameUV);

            int uvWidth = (width / 2);
            int uvHeight = (height / 2);
            int uvSize = uvHeight * uvWidth * 2;
            byte[] uvData = new byte[uvSize];
            System.Buffer.BlockCopy(data, ySize, uvData, 0, uvSize);
            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rg8,
                uvWidth, uvHeight, 0, PixelFormat.Rg, PixelType.UnsignedByte,
                uvData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public void BindToIndex(int idx0, int idx1)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx0);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameY);
            GL.ActiveTexture(TextureUnit.Texture0 + idx1);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameUV);
        }

        public readonly int TextureNameY;
        public readonly int TextureNameUV;

        public void Dispose()
        {
            GL.DeleteTexture(TextureNameY);
        }
    }

    public struct Vec4i
    {
        public int X;
        public int Y;
        public int Z;
        public int W;
    }
    /// <summary>
    /// Vertex array abstraction.
    /// </summary>
    public class VertexArray : IDisposable
    {
        private int elementCount = 0;
        private int elementCountWireframe = 0;
        private int vertexCount = 0;
        private int instanceCount = 0;

        public readonly int[] ArrayName = new int[2];

        private readonly GLObjects.BufferBase _BufferPosition;
        private readonly GLObjects.BufferBase _BufferNormal;
        private readonly GLObjects.BufferBase _BufferElems;

        private readonly GLObjects.BufferBase _BufferTexCoords0;
        private readonly GLObjects.BufferBase _BufferTexCoords1;
        private readonly GLObjects.BufferBase _BufferTexCoords2;
        private GLObjects.Buffer<uint> _BufferWireframeElems;
        private readonly Program _Program;

        public Program Program { get { return _Program; } }
        uint[] ElementArray = null;


        public VertexArray(Program program, Vector3[] positions, ushort[] elems, Vector3[] texCoords,
            Vector3[] normals) :
            this(program, positions, Array.ConvertAll(elems, e => (uint)e), texCoords, null, null,
                normals)
        {

        }

        public VertexArray(Program program, Vector3[] positions, uint[] elems, Vector3[] texCoords0,
            Vector3[] normals) :
            this(program, positions, elems, texCoords0, null, null, normals)
        {

        }

        public VertexArray(Program program, Vector3[] positions, uint[] elems, Vector3[] texCoords0,
            Vec4i[] texCoords1, Vector4[] texCoords2, Vector3[] normals)
        {
            this._Program = program;
            this.ElementArray = elems;

            vertexCount = positions.Length;
            int stride = 3;
            // Allocate buffers referenced by this vertex array
            _BufferPosition = new GLObjects.Buffer<Vector3>(positions);
            if (texCoords0 != null)
            {
                _BufferTexCoords0 = new GLObjects.Buffer<Vector3>(texCoords0);
                stride += 3;
            }
            if (texCoords1 != null)
            {
                _BufferTexCoords1 = new GLObjects.Buffer<Vec4i>(texCoords1);
                stride += 4;
            }
            if (texCoords2 != null)
            {
                _BufferTexCoords2 = new GLObjects.Buffer<Vector4>(texCoords2);
                stride += 4;
            }
            if (normals != null)
            {
                _BufferNormal = new GLObjects.Buffer<Vector3>(normals);
                stride += 3;
            }
            if (elems != null)
            {
                elementCount = elems.Length;
                _BufferElems = new GLObjects.Buffer<uint>(elems);
            }

            for (int i = 0; i < 2; ++i)
            {
                // Generate VAO name
                ArrayName[i] = GL.GenVertexArray();
                // First bind create the VAO
                GLErr.Check();
                program.Use(i);

                GL.BindVertexArray(ArrayName[i]);
                if (_BufferElems != null)
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferElems.BufferName);

                // Select the buffer object
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferPosition.BufferName);
                GL.VertexAttribPointer((int)program.D.LocationPosition, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                if (elems != null)
                    GL.EnableVertexAttribArray((int)program.D.LocationPosition);

                if (texCoords0 != null && program.D.LocationTexCoord0 >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords0.BufferName);
                    GL.VertexAttribPointer((int)program.D.LocationTexCoord0, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.LocationTexCoord0);
                }

                if (texCoords1 != null && program.D.LocationTexCoord1 >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords1.BufferName);
                    GL.VertexAttribIPointer((int)program.D.LocationTexCoord1, 4, VertexAttribIntegerType.Int, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.LocationTexCoord1);
                }

                if (texCoords2 != null && program.D.LocationTexCoord2 >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords2.BufferName);
                    GL.VertexAttribPointer((int)program.D.LocationTexCoord2, 4, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.LocationTexCoord2);
                }

                if (normals != null && program.D.LocationNormals >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferNormal.BufferName);
                    GL.VertexAttribPointer((int)program.D.LocationNormals, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.LocationNormals);
                }

                GL.BindVertexArray(0);
            }

            GLErr.Check();

        }

        public VertexArray(Program program, Vector3[] positions, uint[] elems, Vector3[] normals,
            Vector4 []instanceData0, Vector4[] instanceData1)
        {
            this._Program = program;
            this.ElementArray = elems;

            vertexCount = positions.Length;
            int stride = 3;
            // Allocate buffers referenced by this vertex array
            _BufferPosition = new GLObjects.Buffer<Vector3>(positions);
            if (normals != null)
            {
                _BufferNormal = new GLObjects.Buffer<Vector3>(normals);
                stride += 3;
            }
            if (elems != null)
            {
                elementCount = elems.Length;
                _BufferElems = new GLObjects.Buffer<uint>(elems);
            }

            if (instanceData0 != null)
            {
                instanceCount = instanceData0.Length;
                _BufferTexCoords0 = new GLObjects.Buffer<Vector4>(instanceData0);
                stride += 4;
            }

            if (instanceData1 != null)
            {
                _BufferTexCoords1 = new GLObjects.Buffer<Vector4>(instanceData1);
                stride += 4;
            }

            for (int i = 0; i < 2; ++i)
            {
                // Generate VAO name
                ArrayName[i] = GL.GenVertexArray();
                // First bind create the VAO
                GLErr.Check();
                program.Use(i);

                GL.BindVertexArray(ArrayName[i]);
                if (_BufferElems != null)
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferElems.BufferName);

                // Select the buffer object
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferPosition.BufferName);
                GL.VertexAttribPointer((int)program.D.LocationPosition, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                if (elems != null)
                    GL.EnableVertexAttribArray((int)program.D.LocationPosition);

                if (normals != null && program.D.LocationNormals >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferNormal.BufferName);
                    GL.VertexAttribPointer((int)program.D.LocationNormals, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.LocationNormals);
                }

                int divisor = 0;
                if (instanceData0 != null && program.D.InstanceData0 >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords0.BufferName);
                    GL.VertexAttribPointer((int)program.D.InstanceData0, 4, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.InstanceData0);
                    GL.VertexAttribDivisor(program.D.InstanceData0, 1);
                    divisor++;
                }

                if (instanceData1 != null && program.D.InstanceData1 >= 0)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords1.BufferName);
                    GL.VertexAttribPointer((int)program.D.InstanceData1, 4, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                    GL.EnableVertexAttribArray((int)program.D.InstanceData1);
                    GL.VertexAttribDivisor(program.D.InstanceData1, 1);
                    divisor++;
                }

                GL.BindVertexArray(0);
            }

            GLErr.Check();

        }

        public void UpdatePositions(Vector3[] positions)
        {
            _BufferPosition.Update(positions);
        }

        public void UpdateNormals(Vector3[] normals)
        {
            _BufferNormal.Update(normals);
        }

        public void UpdateTexCoords(Vector3[] texcoords)
        {
            _BufferTexCoords0.Update(texcoords);
        }

        void BuildWireframeElems()
        {
            List<uint> wireframeElems = new List<uint>();
            for (int idx = 0; idx < ElementArray.Length; idx += 3)
            {
                wireframeElems.Add(ElementArray[idx]);
                wireframeElems.Add(ElementArray[idx + 1]);
                wireframeElems.Add(ElementArray[idx + 1]);
                wireframeElems.Add(ElementArray[idx + 2]);
                wireframeElems.Add(ElementArray[idx + 2]);
                wireframeElems.Add(ElementArray[idx]);
            }

            elementCountWireframe = wireframeElems.Count;
            _BufferWireframeElems = new GLObjects.Buffer<uint>(wireframeElems.ToArray());
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferWireframeElems.BufferName);
        }

        public void Draw(int offset, int count)
        {
            GL.BindVertexArray(ArrayName[Program.DataIdx]);
            if (_BufferElems != null)
                GL.DrawElements(PrimitiveType.Triangles, count, DrawElementsType.UnsignedInt, (IntPtr)(offset * 4));
            else
                GL.DrawArrays(PrimitiveType.Points, 0, vertexCount);
        }

        public void Draw()
        {
            Draw(0, elementCount);
        }

        public void DrawInst()                                                                                         
        {
            GL.BindVertexArray(ArrayName[Program.DataIdx]);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, elementCount, DrawElementsType.UnsignedInt, IntPtr.Zero, instanceCount);
        }

        public void DrawWireframe()
        {
            GL.BindVertexArray(ArrayName[0]);
            if (_BufferWireframeElems == null)
                BuildWireframeElems();
            GL.DrawElements(PrimitiveType.Lines, elementCountWireframe, DrawElementsType.UnsignedInt, IntPtr.Zero);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(ArrayName[0]);
            GL.DeleteVertexArray(ArrayName[1]);

            _BufferPosition.Dispose();
            _BufferTexCoords0.Dispose();
            _BufferTexCoords1.Dispose();
            _BufferTexCoords2.Dispose();
            _BufferNormal.Dispose();
            _BufferElems.Dispose();
        }
    }

    static class Registry
    {
        public static Dictionary<string, Program> Programs =
            new Dictionary<string, Program>();

        public static VertexArray BltVA;
        static public void LoadAllPrograms()
        {
            Programs.Add("main", Program.FromFiles("Main.vert", "Main.frag"));
            Programs.Add("vox", Program.FromFiles("Vox.vert", "Vox.frag"));
            Programs.Add("vid", Program.FromFiles("VidShader.vert", "VidShader.frag"));
            Programs.Add("depth", Program.FromFiles("DepthShader.vert", "DepthShader.frag"));
            Programs.Add("blt", Program.FromFiles("BltTex.vert", "BltTex.frag"));
            BltVA = new VertexArray(Programs["blt"], _ArrayPosition, _ArrayElems, _ArrayTexCoord, null);
        }

        private static readonly Vector3[] _ArrayPosition = new Vector3[] {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f)
        };

        private static readonly ushort[] _ArrayElems = new ushort[]
        {
            0, 1, 2, 2, 3, 0,
        };

        /// <summary>
        /// Vertex color array.
        /// </summary>
        private static readonly Vector3[] _ArrayTexCoord = new Vector3[] {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
        };

        public static void DrawRT(RenderTarget rt, Vector4 sclOffset)
        {
            Program blt = Programs["blt"];
            blt.Use(0);
            blt.Set1("texSampler", (int)0);
            rt.Tex.BindToIndex(0);
            blt.Set4("offsetScale", sclOffset);
            BltVA.Draw();
        }

    }

    static class Line
    {
        public static void Draw(Vector3 pt0, Vector3 pt1, float width, Vector3 color, List<Vector3> pts, List<uint> ind,
                    List<Vector3> colors
                    )
        {
            uint startIdx = (uint)pts.Count;
            Vector3 dir = (pt1 - pt0).Normalized();
            Vector3 nrm = Vector3.Cross(dir, dir.X > dir.Z ? Vector3.UnitZ : Vector3.UnitX);
            pts.Add(pt0 - nrm * width);
            pts.Add(pt0 + nrm * width);
            pts.Add(pt1 - nrm * width);
            pts.Add(pt1 + nrm * width);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            ind.Add(startIdx);
            ind.Add(startIdx + 1);
            ind.Add(startIdx + 2);
            ind.Add(startIdx + 1);
            ind.Add(startIdx + 3);
            ind.Add(startIdx + 2);
        }
    }

    class Cube
    {
        public static VertexArray MakeCube(Program program)
        {
            ushort[] indices = new ushort[_Cube.Length];
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
            for (int i = 0; i < 6; ++i)
            {
                Vector3 d1 = _Cube[i * 6 + 1] - _Cube[i * 6];
                Vector3 d2 = _Cube[i * 6 + 2] - _Cube[i * 6 + 1];
                Vector3 nrm = Vector3.Cross(d1, d2).Normalized();
                for (int nIdx = 0; nIdx < 6; ++nIdx)
                {
                    nrmCoords[i * 6 + nIdx] = nrm;
                }
            }

            for (int i = 0; i < indices.Length; ++i)
            {
                indices[i] = (ushort)i;
                Vector3 xdir = xdirs[i / 12];
                Vector3 ydir = ydirs[i / 12];
                int sideIdx = i / 6;
                texCoords[i] = new Vector3(Vector3.Dot(_Cube[i], xdir),
                    Vector3.Dot(_Cube[i], ydir), (float)sideIdx / 6.0f);
            }

            return new VertexArray(program, _Cube, indices, texCoords, nrmCoords);
        }

        static Vector3[] _Octahedron
         = new Vector3[] {
            new Vector3(1, 0, 0),
            new Vector3(0, -1, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1) };

        static uint[] _OctaIndices =
        {
            4, 0, 1,
            4, 1, 2,
            4, 2, 3,
            4, 3, 0,
            5, 1, 0,
            5, 2, 1,
            5, 3, 2,
            5, 0, 3
        };

        public static VertexArray MakeOctahedron(Program program)
        {
            Vector3[] texCoords = new Vector3[_Octahedron.Length];
            Vector3[] nrmCoords = new Vector3[_Octahedron.Length];
            for (int idx = 0; idx < _Octahedron.Length; ++idx)
            {
                texCoords[idx] = _Octahedron[idx];
                nrmCoords[idx] = _Octahedron[idx].Normalized();
            }

            return new VertexArray(program, _Octahedron, _OctaIndices, texCoords, nrmCoords);
        }

        private static readonly Vector3[] _Cube = new Vector3[] {
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, -1.0f),  // 2

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3

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

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(-1.0f, -1.0f, 1.0f),  // 4

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(1.0f, -1.0f, 1.0f),  // 5
        };

        static Vector3[] _Torus = new Vector3[]
        {
new Vector3(0.946977f, 0.392250f, 0.000000f),
new Vector3(1.012500f, 0.000000f, 0.021651f),
new Vector3(1.025000f, 0.000000f, 0.000000f),
new Vector3(0.912331f, 0.377900f, 0.021651f),
new Vector3(0.987500f, 0.000000f, 0.021651f),
new Vector3(0.975000f, 0.000000f, 0.000000f),
new Vector3(0.912331f, 0.377900f, -0.021651f),
new Vector3(0.987500f, 0.000000f, -0.021651f),
new Vector3(1.012500f, 0.000000f, -0.021651f),
new Vector3(0.724784f, 0.724784f, 0.000000f),
new Vector3(0.935428f, 0.387467f, 0.021651f),
new Vector3(0.698268f, 0.698268f, 0.021651f),
new Vector3(0.900783f, 0.373116f, 0.000000f),
new Vector3(0.689429f, 0.689429f, 0.000000f),
new Vector3(0.698268f, 0.698268f, -0.021651f),
new Vector3(0.935428f, 0.387467f, -0.021651f),
new Vector3(0.387467f, 0.935428f, 0.021651f),
new Vector3(0.715946f, 0.715946f, 0.021651f),
new Vector3(0.373117f, 0.900782f, 0.000000f),
new Vector3(0.387467f, 0.935428f, -0.021651f),
new Vector3(0.715946f, 0.715946f, -0.021651f),
new Vector3(0.000000f, 1.025000f, 0.000000f),
new Vector3(0.392251f, 0.946976f, 0.000000f),
new Vector3(0.000000f, 1.012500f, 0.021651f),
new Vector3(0.377900f, 0.912331f, 0.021651f),
new Vector3(0.000000f, 0.987500f, 0.021651f),
new Vector3(0.000000f, 0.987500f, -0.021651f),
new Vector3(0.377900f, 0.912331f, -0.021651f),
new Vector3(0.000000f, 1.012500f, -0.021651f),
new Vector3(-0.392251f, 0.946976f, 0.000000f),
new Vector3(-0.377900f, 0.912331f, 0.021651f),
new Vector3(0.000000f, 0.975000f, 0.000000f),
new Vector3(-0.377900f, 0.912331f, -0.021651f),
new Vector3(-0.387467f, 0.935428f, -0.021651f),
new Vector3(-0.724785f, 0.724784f, 0.000000f),
new Vector3(-0.387467f, 0.935428f, 0.021651f),
new Vector3(-0.698268f, 0.698268f, 0.021651f),
new Vector3(-0.373116f, 0.900783f, 0.000000f),
new Vector3(-0.698268f, 0.698268f, -0.021651f),
new Vector3(-0.935428f, 0.387467f, 0.021651f),
new Vector3(-0.715946f, 0.715945f, 0.021651f),
new Vector3(-0.900783f, 0.373116f, 0.000000f),
new Vector3(-0.689429f, 0.689429f, 0.000000f),
new Vector3(-0.935428f, 0.387467f, -0.021651f),
new Vector3(-0.715946f, 0.715945f, -0.021651f),
new Vector3(-1.025000f, 0.000000f, 0.000000f),
new Vector3(-0.946976f, 0.392251f, 0.000000f),
new Vector3(-1.012500f, 0.000000f, 0.021651f),
new Vector3(-0.912331f, 0.377900f, 0.021651f),
new Vector3(-0.987500f, 0.000000f, 0.021651f),
new Vector3(-0.975000f, 0.000000f, 0.000000f),
new Vector3(-0.912331f, 0.377900f, -0.021651f),
new Vector3(-0.987500f, 0.000000f, -0.021651f),
new Vector3(-1.012500f, 0.000000f, -0.021651f),
new Vector3(-0.946977f, -0.392250f, 0.000000f),
new Vector3(-0.912331f, -0.377900f, 0.021651f),
new Vector3(-0.900783f, -0.373116f, 0.000000f),
new Vector3(-0.912331f, -0.377900f, -0.021651f),
new Vector3(-0.935428f, -0.387467f, -0.021651f),
new Vector3(-0.724785f, -0.724784f, 0.000000f),
new Vector3(-0.935428f, -0.387467f, 0.021651f),
new Vector3(-0.698268f, -0.698268f, 0.021651f),
new Vector3(-0.689429f, -0.689429f, 0.000000f),
new Vector3(-0.698268f, -0.698268f, -0.021651f),
new Vector3(-0.387467f, -0.935428f, 0.021651f),
new Vector3(-0.715946f, -0.715945f, 0.021651f),
new Vector3(-0.373116f, -0.900783f, 0.000000f),
new Vector3(-0.387467f, -0.935428f, -0.021651f),
new Vector3(-0.715946f, -0.715945f, -0.021651f),
new Vector3(0.000000f, -1.025000f, 0.000000f),
new Vector3(-0.392251f, -0.946976f, 0.000000f),
new Vector3(0.000000f, -1.012500f, 0.021651f),
new Vector3(-0.377900f, -0.912331f, 0.021651f),
new Vector3(0.000000f, -0.975000f, 0.000000f),
new Vector3(-0.377900f, -0.912331f, -0.021651f),
new Vector3(0.000000f, -1.012500f, -0.021651f),
new Vector3(0.392251f, -0.946976f, 0.000000f),
new Vector3(0.377900f, -0.912331f, 0.021651f),
new Vector3(0.000000f, -0.987500f, 0.021651f),
new Vector3(0.373117f, -0.900782f, 0.000000f),
new Vector3(0.000000f, -0.987500f, -0.021651f),
new Vector3(0.377900f, -0.912331f, -0.021651f),
new Vector3(0.387467f, -0.935428f, -0.021651f),
new Vector3(0.724784f, -0.724785f, 0.000000f),
new Vector3(0.387467f, -0.935428f, 0.021651f),
new Vector3(0.698268f, -0.698268f, 0.021651f),
new Vector3(0.698268f, -0.698268f, -0.021651f),
new Vector3(0.715945f, -0.715946f, -0.021651f),
new Vector3(0.935428f, -0.387467f, 0.021651f),
new Vector3(0.715945f, -0.715946f, 0.021651f),
new Vector3(0.900782f, -0.373117f, 0.000000f),
new Vector3(0.689429f, -0.689430f, 0.000000f),
new Vector3(0.935428f, -0.387467f, -0.021651f),
new Vector3(0.946976f, -0.392251f, 0.000000f),
new Vector3(0.912331f, -0.377900f, 0.021651f),
new Vector3(0.912331f, -0.377900f, -0.021651f)
        };

        static uint[] _TorusIndices =
        {
0, 1, 2,
1, 3, 4,
3, 5, 4,
5, 6, 7,
6, 8, 7,
8, 0, 2,
9, 10, 0,
10, 11, 3,
11, 12, 3,
13, 6, 12,
14, 15, 6,
15, 9, 0,
9, 16, 17,
16, 11, 17,
11, 18, 13,
18, 14, 13,
14, 19, 20,
19, 9, 20,
21, 16, 22,
23, 24, 16,
25, 18, 24,
18, 26, 27,
27, 28, 19,
28, 22, 19,
29, 23, 21,
23, 30, 25,
30, 31, 25,
31, 32, 26,
32, 28, 26,
33, 21, 28,
34, 35, 29,
35, 36, 30,
36, 37, 30,
37, 38, 32,
38, 33, 32,
33, 34, 29,
34, 39, 40,
39, 36, 40,
36, 41, 42,
41, 38, 42,
38, 43, 44,
43, 34, 44,
45, 39, 46,
47, 48, 39,
49, 41, 48,
50, 51, 41,
52, 43, 51,
53, 46, 43,
54, 47, 45,
47, 55, 49,
49, 56, 50,
56, 52, 50,
57, 53, 52,
58, 45, 53,
59, 60, 54,
60, 61, 55,
61, 56, 55,
62, 57, 56,
63, 58, 57,
58, 59, 54,
59, 64, 65,
64, 61, 65,
61, 66, 62,
66, 63, 62,
63, 67, 68,
67, 59, 68,
69, 64, 70,
71, 72, 64,
72, 73, 66,
73, 74, 66,
74, 75, 67,
75, 70, 67,
76, 71, 69,
71, 77, 78,
78, 79, 73,
79, 80, 73,
81, 75, 80,
82, 69, 75,
83, 84, 76,
84, 85, 77,
85, 79, 77,
79, 86, 81,
86, 82, 81,
87, 76, 82,
83, 88, 89,
88, 85, 89,
85, 90, 91,
90, 86, 91,
86, 92, 87,
92, 83, 87,
93, 1, 88,
1, 94, 88,
94, 5, 90,
5, 95, 90,
95, 8, 92,
8, 93, 92,
0, 10, 1,
1, 10, 3,
3, 12, 5,
5, 12, 6,
6, 15, 8,
8, 15, 0,
9, 17, 10,
10, 17, 11,
11, 13, 12,
13, 14, 6,
14, 20, 15,
15, 20, 9,
9, 22, 16,
16, 24, 11,
11, 24, 18,
18, 27, 14,
14, 27, 19,
19, 22, 9,
21, 23, 16,
23, 25, 24,
25, 31, 18,
18, 31, 26,
27, 26, 28,
28, 21, 22,
29, 35, 23,
23, 35, 30,
30, 37, 31,
31, 37, 32,
32, 33, 28,
33, 29, 21,
34, 40, 35,
35, 40, 36,
36, 42, 37,
37, 42, 38,
38, 44, 33,
33, 44, 34,
34, 46, 39,
39, 48, 36,
36, 48, 41,
41, 51, 38,
38, 51, 43,
43, 46, 34,
45, 47, 39,
47, 49, 48,
49, 50, 41,
50, 52, 51,
52, 53, 43,
53, 45, 46,
54, 60, 47,
47, 60, 55,
49, 55, 56,
56, 57, 52,
57, 58, 53,
58, 54, 45,
59, 65, 60,
60, 65, 61,
61, 62, 56,
62, 63, 57,
63, 68, 58,
58, 68, 59,
59, 70, 64,
64, 72, 61,
61, 72, 66,
66, 74, 63,
63, 74, 67,
67, 70, 59,
69, 71, 64,
71, 78, 72,
72, 78, 73,
73, 80, 74,
74, 80, 75,
75, 69, 70,
76, 84, 71,
71, 84, 77,
78, 77, 79,
79, 81, 80,
81, 82, 75,
82, 76, 69,
83, 89, 84,
84, 89, 85,
85, 91, 79,
79, 91, 86,
86, 87, 82,
87, 83, 76,
83, 93, 88,
88, 94, 85,
85, 94, 90,
90, 95, 86,
86, 95, 92,
92, 93, 83,
93, 2, 1,
1, 4, 94,
94, 4, 5,
5, 7, 95,
95, 7, 8,
8, 2, 93
        };

        public static VertexArray MakeTorus(Program program)
        {
            Vector3[] texCoords = new Vector3[_Torus.Length];
            Vector3[] nrmCoords = new Vector3[_Torus.Length];
            for (int idx = 0; idx < _Torus.Length; ++idx)
            {
                texCoords[idx] = _Torus[idx];
                nrmCoords[idx] = _Torus[idx].Normalized();
            }

            return new VertexArray(program, _Torus, _TorusIndices, texCoords, nrmCoords);
        }

    }

}

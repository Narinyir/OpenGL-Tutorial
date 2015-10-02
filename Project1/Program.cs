using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace OpenGLTutorial
{
    public sealed class VBO : IDisposable
    {
        private const int InvalidHandle = -1;

        public int Handle { get; private set; }
        public BufferTarget Type { get; private set; }

        public VBO(BufferTarget type = BufferTarget.ArrayBuffer)
        {
            Type = type;
            AcquireHandle();
        }

        private void AcquireHandle()
        {
            Handle = GL.GenBuffer();
        }

        public void Use()
        {
            GL.BindBuffer(Type, Handle);
        }

        public void SetData<T>(T[] data) where T : struct
        {
            if (data.Length == 0)
                throw new ArgumentException("Массив должен содержать хотя бы один элемент", "data");

            Use();
            GL.BufferData(Type, (IntPtr)(data.Length * Marshal.SizeOf(typeof(T))), data, BufferUsageHint.StaticDraw);
        }

        private void ReleaseHandle()
        {
            if (Handle == InvalidHandle)
                return;

            GL.DeleteBuffer(Handle);

            Handle = InvalidHandle;
        }

        public void Dispose()
        {
            ReleaseHandle();
            GC.SuppressFinalize(this);
        }

        ~VBO()
        {
            if (GraphicsContext.CurrentContext != null && !GraphicsContext.CurrentContext.IsDisposed)
                ReleaseHandle();
        }
    }

    public sealed class VAO : IDisposable
    {
        private const int InvalidHandle = -1;

        public int Handle { get; private set; }
        public int VertexCount { get; private set; }

        public VAO(int vertexCount)
        {
            VertexCount = vertexCount;
            AcquireHandle();
        }

        private void AcquireHandle()
        {
            Handle = GL.GenVertexArray();
        }

        public void Use()
        {
            GL.BindVertexArray(Handle);
        }

        public void AttachVBO(int index, VBO vbo, int elementsPerVertex, VertexAttribPointerType pointerType, int stride, int offset)
        {
            Use();
            vbo.Use();
            GL.EnableVertexAttribArray(index);
            GL.VertexAttribPointer(index, elementsPerVertex, pointerType, false, stride, offset);
        }

        public void Draw()
        {
            Use();
            GL.DrawArrays(PrimitiveType.Triangles, 0, VertexCount);
        }

        private void ReleaseHandle()
        {
            if (Handle == InvalidHandle)
                return;

            GL.DeleteVertexArray(Handle);

            Handle = InvalidHandle;
        }

        public void Dispose()
        {
            ReleaseHandle();
            GC.SuppressFinalize(this);
        }

        ~VAO()
        {
            if (GraphicsContext.CurrentContext != null && !GraphicsContext.CurrentContext.IsDisposed)
                ReleaseHandle();
        }
    }

    public sealed class Shader : IDisposable
    {
        private const int InvalidHandle = -1;

        public int Handle { get; private set; }
        public ShaderType Type { get; private set; }

        public Shader(ShaderType type)
        {
            Type = type;
            AcquireHandle();
        }

        private void AcquireHandle()
        {
            Handle = GL.CreateShader(Type);
        }

        public void Compile(string source)
        {
            GL.ShaderSource(Handle, source);
            GL.CompileShader(Handle);

            int compileStatus;
            GL.GetShader(Handle, ShaderParameter.CompileStatus, out compileStatus);

            if (compileStatus == 0)
                Console.WriteLine(GL.GetShaderInfoLog(Handle));
        }

        private void ReleaseHandle()
        {
            if (Handle == InvalidHandle)
                return;

            GL.DeleteShader(Handle);

            Handle = InvalidHandle;
        }

        public void Dispose()
        {
            ReleaseHandle();
            GC.SuppressFinalize(this);
        }

        ~Shader()
        {
            if (GraphicsContext.CurrentContext != null && !GraphicsContext.CurrentContext.IsDisposed)
                ReleaseHandle();
        }
    }

    public sealed class ShaderProgram : IDisposable
    {
        private const int InvalidHandle = -1;

        public int Handle { get; private set; }

        public ShaderProgram()
        {
            AcquireHandle();
        }

        private void AcquireHandle()
        {
            Handle = GL.CreateProgram();
        }

        public void AttachShader(Shader shader)
        {
            GL.AttachShader(Handle, shader.Handle);
        }

        public void Link()
        {
            GL.LinkProgram(Handle);

            int linkStatus;
            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out linkStatus);

            if (linkStatus == 0)
                Console.WriteLine(GL.GetProgramInfoLog(Handle));
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }

        private void ReleaseHandle()
        {
            if (Handle == InvalidHandle)
                return;

            GL.DeleteProgram(Handle);

            Handle = InvalidHandle;
        }

        public void Dispose()
        {
            ReleaseHandle();
            GC.SuppressFinalize(this);
        }

        ~ShaderProgram()
        {
            if (GraphicsContext.CurrentContext != null && !GraphicsContext.CurrentContext.IsDisposed)
                ReleaseHandle();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public float X, Y;
        public float R, G, B;

        public Vertex(float x, float y, Color4 color)
        {
            X = x;
            Y = y;
            R = color.R;
            G = color.G;
            B = color.B;
        }
    }

    public sealed class Window : GameWindow
    {
        private VBO meshVbo;
        private VAO meshVao;
        private ShaderProgram shaderProgram;

        public Window()
            : base(800, 600, GraphicsMode.Default, "OpenGL Tutorial", GameWindowFlags.Default, DisplayDevice.Default, 4, 0, GraphicsContextFlags.ForwardCompatible)
        { }

        protected override void OnLoad(EventArgs e)
        {
            GL.ClearColor(Color4.Black);

            meshVbo = new VBO();
            meshVbo.SetData(new[] { new Vertex(0.0f, 0.5f, Color4.Red), new Vertex(-0.5f, -0.5f, Color4.Green), new Vertex(0.5f, -0.5f, Color4.Blue) });

            meshVao = new VAO(3);
            meshVao.AttachVBO(0, meshVbo, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
            meshVao.AttachVBO(1, meshVbo, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 2 * sizeof(float));

            shaderProgram = new ShaderProgram();

            using (var vertexShader = new Shader(ShaderType.VertexShader))
            using (var fragmentShader = new Shader(ShaderType.FragmentShader))
            {
                vertexShader.Compile(@"
#version 400

layout(location = 0) in vec2 Position;
layout(location = 1) in vec3 Color;

out vec3 fragColor;

void main()
{
    gl_Position = vec4(Position, 0.0, 1.0);
    fragColor = Color;
}
");

                fragmentShader.Compile(@"
#version 400

in vec3 fragColor;

layout(location = 0) out vec4 outColor;

void main()
{
    outColor = vec4(fragColor, 1.0);
}
");

                shaderProgram.AttachShader(vertexShader);
                shaderProgram.AttachShader(fragmentShader);
                shaderProgram.Link();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            shaderProgram.Use();
            meshVao.Draw();
            SwapBuffers();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            meshVbo.Dispose();
            meshVao.Dispose();
            shaderProgram.Dispose();
        }

        public static void Main()
        {
            using (var window = new Window())
                window.Run(60);
        }
    }
}

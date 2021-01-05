using System;
using System.Windows;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using wf = System.Windows.Forms;
using System.Collections.Generic;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Documents;

namespace Vox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public abstract class IRenderer
    {
        public delegate void InvalidateDel();
        public InvalidateDel Invalidate;

        public abstract void Load();
        public abstract void Paint();
        public abstract void Resize(int width, int height);
        public abstract void TouchDn(Touch t);
        public abstract void TouchMove(Touch t);
        public abstract void MouseWheel(int x, int y, int delta);
        public abstract void KeyDown(wf.KeyEventArgs e);
        public abstract void KeyUp(wf.KeyEventArgs e);
        public abstract void Action(int param);
    }

    public class Touch
    {
        public enum TouchType
        {
            eSingleFinger,
            eTwoFingers
        }
        public TouchType touchType;
        
        public Object downData;
        public Vector2 downPos;
        public Vector2 curPos;

        public Vector2 TotalDelta { get => curPos - downPos; }
    }

    public partial class MainWindow : Window
    {
        IRenderer[] renderers = new IRenderer[] { new Editor() };


        public OpenTK.GLControl GLControl => glControl;

        List<Touch> touches = new List<Touch>();
        public IRenderer AR => renderers[0];

        public MainWindow()
        {
            InitializeComponent();
        }

        System.Timers.Timer renderTimer = new System.Timers.Timer();

        void RefreshMode()
        {

        }

        void OnInvalidate()
        {
            glControl.Invalidate();
        }

        private void glControl_Resize(object sender, EventArgs e)
        {
            foreach (var r in renderers)
                r.Resize(glControl.ClientRectangle.Width, glControl.ClientRectangle.Height);
        }

        private void RenderTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            glControl.Invalidate();
        }

        public void GlDebugProc(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            glControl.Paint += GlControl_Paint;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseUp += GlControl_MouseUp;
            glControl.MouseWheel += GlControl_MouseWheel;
            glControl.KeyDown += GlControl_KeyDown;
            glControl.KeyUp += GlControl_KeyUp;
            renderTimer.Interval = 1.0f / 60.0f;
            renderTimer.Elapsed += RenderTimer_Elapsed;

            GL.Enable(EnableCap.MultisampleSgis);
            GL.DebugMessageCallback(GlDebugProc, IntPtr.Zero);

            Registry.LoadAllPrograms();
            foreach (var r in renderers)
                r.Load();

            renderTimer.Start();
        }


        private void GlControl_KeyUp(object sender, wf.KeyEventArgs e)
        {
            AR.KeyUp(e);
        }

        private void GlControl_KeyDown(object sender, wf.KeyEventArgs e)
        {
            AR.KeyDown(e);
        }

        
        private void GlControl_Paint(object sender, wf.PaintEventArgs e)
        {
            AR.Paint();
            glControl.SwapBuffers();
        }


        private void GlControl_MouseUp(object sender, wf.MouseEventArgs e)
        {
            if (touches.Count > 0)
            {
                touches.Clear();
            }
        }

        private void GlControl_MouseWheel(object sender, wf.MouseEventArgs e)
        {
            AR.MouseWheel(e.X, e.Y, e.Delta);
        }

        Vector2 ScreenToViewport(System.Drawing.Point pt)
        {
            return new Vector2(((float)pt.X / (float)glControl.Width) * 2 - 1.0f,
                             1.0f - ((float)pt.Y / (float)glControl.Height) * 2);
        }

        private void GlControl_MouseMove(object sender, wf.MouseEventArgs e)
        {
            if (touches.Count > 0)
            {
                touches[0].curPos = new Vector2(e.X / (float)glControl.Width, e.Y / (float)glControl.Height);
                AR.TouchMove(touches[0]);
            }
        }

        private void GlControl_MouseDown(object sender, wf.MouseEventArgs e)
        {
            Touch t = new Touch();
            if (e.Button == wf.MouseButtons.Left)
                t.touchType = Touch.TouchType.eSingleFinger;
            else if (e.Button == wf.MouseButtons.Middle)
                t.touchType = Touch.TouchType.eTwoFingers;
            t.curPos = new Vector2(e.X / (float)glControl.Width, e.Y / (float)glControl.Height);
            t.downPos = t.curPos;
            touches.Add(t);
            AR.TouchDn(t);
        }
    }
}

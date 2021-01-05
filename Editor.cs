using System;
using System.Windows.Forms;
using GLObjects;
using OpenTK.Graphics.ES30;
using OpenTK;
using System.Collections.Generic;

namespace Vox
{
    class Editor : IRenderer
    {

        Program _Program;
        Program _ProgramInst;

        public Editor()
        {

        }
        Matrix4 projectionMat = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1, 0.5f, 120.0f) *
            Matrix4.CreateScale(new Vector3(-1, 1, 1));
        Vector3 viewOffset = Vector3.Zero;
        const float origScale = 1.0f;
        float viewScale = origScale;
        VertexArray gridVA;
        Oct oct;
        VertexArray octVA;
        Vector3 offsetVec = new Vector3(0, 0, 150);
        Vector3 moveVel = Vector3.Zero;
        float speed = 1;
        float rspeed = 2;
        int mouseWheelOffset = 0;
        Quaternion curViewrotate = Quaternion.Identity;
        RenderTarget pickTarget;
        Oct selectedNode = null;

        Model modelVA;
        

        Matrix4 ViewProj
        {
            get
            {
                Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1, 1f, 500);
                Matrix4 view = Matrix4.CreateTranslation(offsetVec) * CamRotate * Matrix4.CreateScale(viewScale);
                return view.Inverted() * projection;
            }
        }


        Matrix4 CamRotate
        {
            get
            {
                return Matrix4.CreateFromQuaternion(curViewrotate);
            }
        }

        public override void Load()
        {
            _Program = Registry.Programs["main"];
            _ProgramInst = Registry.Programs["vox"];
            List<Vector3> qpts = new List<Vector3>();
            List<Vector3> colors = new List<Vector3>();
            List<uint> ind = new List<uint>();
            float rgsize = 128;
            float rgh = rgsize / 2;
            float rgstep = 16;
            float width = 0.2f;

            for (float rgx = -rgh; rgx < rgh; rgx += rgstep)
            {
                Line.Draw(new Vector3(rgx, -rgh, -rgh), new Vector3(rgx, -rgh, rgh), width, new Vector3(1, 0, 0), qpts, ind, colors);
                Line.Draw(new Vector3(rgx, -rgh, -rgh), new Vector3(rgx, rgh, -rgh), width, new Vector3(0, 1, 0), qpts, ind, colors);
            }

            for (float rgy = -rgh; rgy < rgh; rgy += rgstep)
            {
                Line.Draw(new Vector3(-rgh, rgy, -rgh), new Vector3(-rgh, rgy, rgh), width, new Vector3(0, 0, 1), qpts, ind, colors);
                Line.Draw(new Vector3(-rgh, rgy, -rgh), new Vector3(rgh, rgy, -rgh), width, new Vector3(0, 1, 0), qpts, ind, colors);
            }

            for (float rgz = -rgh; rgz < rgh; rgz += rgstep)
            {
                Line.Draw(new Vector3(-rgh, -rgh, rgz), new Vector3(-rgh, rgh, rgz), width, new Vector3(0, 0, 1), qpts, ind, colors);
                Line.Draw(new Vector3(-rgh, -rgh, rgz), new Vector3(rgh, -rgh, rgz), width, new Vector3(1, 0, 0), qpts, ind, colors);
            }

            Vector3[] nrm = new Vector3[qpts.Count];
            for (int idx = 0; idx < nrm.Length; ++idx) nrm[idx] = new Vector3(0, 0, 1);
            gridVA = new VertexArray(this._Program, qpts.ToArray(), ind.ToArray(), colors.ToArray(), nrm);


            modelVA = new Model(@"C:\Users\shane\Documents\helene2.ply");
            oct = this.modelVA.BuildOct(7, 8);
            octVA = OctViz.BuildVA(_ProgramInst, oct);
        }

        public override void Paint()
        {
            offsetVec += Vector3.TransformVector(moveVel, CamRotate);
            Matrix4 viewProj = ViewProj;

            FrameBuffer.BindNone();
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            _Program.Use(0);

            _Program.Set1("opacity", 0.4f);
            _Program.Set3("meshColor", new Vector3(1, 1, 1));
            _Program.Set1("ambient", 1.0f);
            _Program.Set3("lightPos", new Vector3(2, 5, 2));
            _Program.SetMVP(Matrix4.Identity, viewProj);
            gridVA.Draw();

            _Program.SetMVP(Matrix4.CreateScale(64), viewProj);
            DrawVox(0);
//            this.modelVA.DebugDraw();
        }

        struct GLPixelf
        {
            public float r;
            public float g;
            public float b;
            public float a;

            public bool HasValue =>
                r != 0 || g != 0 || b != 0 || a != 0;

            public override string ToString()
            {
                return $"{r},{g},{b},{a}";
            }
        }


        void DrawVox(int idx)
        {
            Matrix4 viewProj = ViewProj;
            _ProgramInst.Use(idx);
            _ProgramInst.Set1("opacity", 1.0f);
            _ProgramInst.Set1("ambient", 0.4f);
            _ProgramInst.Set3("lightPos", new Vector3(2, 5, 2));
            _ProgramInst.SetMVP(Matrix4.CreateTranslation(-0.5f, -0.5f, -0.5f) * Matrix4.CreateScale(64), viewProj);
            octVA.DrawInst();
        }

        GLPixelf[] pixels = null;
        void DoPick(bool fullObjectPicking, float sx, float sy)
        {
            pickTarget.Use();

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            Matrix4 viewProj = ViewProj;
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            DrawVox(1);

            GL.Finish();
            if (pixels == null || pixels.Length != (pickTarget.Width * pickTarget.Height))
                pixels = new GLPixelf[pickTarget.Width * pickTarget.Height];
            GL.ReadPixels<GLPixelf>(0, 0, pickTarget.Width, pickTarget.Height, PixelFormat.Rgba, PixelType.Float, pixels);
            int cx = (int)(sx * pickTarget.Width);
            int cy = (int)(sy * pickTarget.Height);
            GLPixelf v = pixels[(pickTarget.Height - cy - 1) * pickTarget.Width + cx];

            int x = (int)Math.Round(v.r / v.a);
            int y = (int)Math.Round(v.g / v.a);
            int z = (int)Math.Round(v.b / v.a);
            int l = (int)Math.Round(Math.Log(1 / v.a) / Math.Log(2));

            this.selectedNode = this.oct.GetAtLoc(new Loc(l, x, y, z));
            System.Diagnostics.Debug.WriteLine($"{l} [{x}, {y}, {z}]");
            FrameBuffer.BindNone();

        }
        
        public override void Resize(int width, int height)
        {
            FrameBuffer.SetViewPortSize(width, height);
            pickTarget = new RenderTarget(1024, 1024, new TextureRgba128());
        }

        public override void Action(int param)
        {
        }

        public override void KeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                    moveVel.Z = -speed;
                    break;
                case Keys.A:
                    moveVel.X = -speed;
                    break;
                case Keys.S:
                    moveVel.Z = speed;
                    break;
                case Keys.D:
                    moveVel.X = speed;
                    break;
                case Keys.Space:
                    moveVel.Y = speed;
                    break;
                case Keys.ShiftKey:
                    moveVel.Y -= speed;
                    break;
            }
        }

        public override void KeyUp(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                    moveVel.Z = 0;
                    break;
                case Keys.A:
                    moveVel.X = 0;
                    break;
                case Keys.S:
                    moveVel.Z = 0;
                    break;
                case Keys.D:
                    moveVel.X = 0;
                    break;
                case Keys.Space:
                    moveVel.Y = 0;
                    break;
                case Keys.ShiftKey:
                    moveVel.Y = 0;
                    break;
            }
        }

        public override void MouseWheel(int x, int y, int delta)
        {
            mouseWheelOffset += delta;
            viewScale = (float)Math.Pow(2, mouseWheelOffset / 400.0) *
                origScale;
        }

        public override void TouchDn(Touch t)
        {
            DoPick(true, t.downPos.X, t.downPos.Y);
            if (t.touchType == Touch.TouchType.eSingleFinger)
                t.downData = this.curViewrotate;
            else if (t.touchType == Touch.TouchType.eTwoFingers)
                t.downData = this.offsetVec;
        }

        public override void TouchMove(Touch t)
        {
            if (t.touchType == Touch.TouchType.eSingleFinger)
            {
                Quaternion orig = (Quaternion)t.downData;
                Vector3 xAxis = orig * Vector3.UnitX;
                Vector3 yAxis = orig * Vector3.UnitY;
                this.curViewrotate = Quaternion.FromAxisAngle(xAxis, (t.TotalDelta.Y * rspeed) % (float)(Math.PI * 2)) *
                    Quaternion.FromAxisAngle(yAxis, (t.TotalDelta.X * rspeed) % (float)(Math.PI * 2)) * orig; 
            }
            else if (t.touchType == Touch.TouchType.eTwoFingers)
            {
                Vector3 orig = (Vector3)t.downData;
                this.offsetVec.X = orig.X - (t.TotalDelta).X * 100;
                this.offsetVec.Y = orig.Y + (t.TotalDelta).Y * 100;
            }
        }
    }
}

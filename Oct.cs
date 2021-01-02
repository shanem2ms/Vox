using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GLObjects;
using OpenTK;
using System.Diagnostics;

namespace Vox
{
    struct Loc : IComparable<Loc>
    {
        public int lev;
        public long x;
        public long y;
        public long z;

        public Loc(int l, long _x, long _y, long _z)
        {
            lev = l;
            x = _x; y = _y; z = _z;
        }


        public Loc this[int i]
        {
            get
            {
                return new Loc(lev + 1,
                    x * 2 + (i & 1),
                    y * 2 + ((i >> 1) & 1),
                    z * 2 + ((i >> 2) & 1));
            }
        }

        public int CompareTo(Loc other)
        {
            int c = z.CompareTo(other.z);
            if (c != 0) return c;
            c = y.CompareTo(other.y);
            if (c != 0) return c;
            return x.CompareTo(other.x);
        }

        public Vector4 GetPosScale()
        {
            float scale = 1.0f / (float)(1 << lev);
            return new Vector4(x * scale, y * scale, z * scale, scale);
        }

        public Vector3[] GetBox()
        {
            float scale = 1.0f / (float)(1 << lev);
            return new Vector3[2] { new Vector3(x * scale, y * scale, z * scale),
                new Vector3((x + 1) * scale, (y + 1) * scale, (z + 1) * scale)};
        }

        public override string ToString()
        {
            return $"{lev} [{x} {y} {z}]";
        }
    }

    class Oct : IComparable<Oct>
    {
        public Loc l;
        public Vector3 color = Vector3.One;
        public Oct[] n;
        public bool visible = true;

        public Oct(int buildlevels) : this(buildlevels, new Loc(0, 0, 0, 0))
        { }

        Oct(int buildlevels, Loc cl)
        {
            l = cl;

            if (l.lev < buildlevels)
            {
                n = new Oct[8];
                for (int i = 0; i < 8; ++i)
                {
                    n[i] = new Oct(buildlevels, l[i]);
                }
            }
        }

        public Oct(int minlevel, int maxlevel, float[][] sides, int size) : this(minlevel, maxlevel, new Loc(0, 0, 0, 0), sides, size)
        { }

        int IsHitX(Vector3[] mm, float[][] sides, int size)
        {
            return IsHit(new Vector2[4] {
                    new Vector2(mm[0].X, mm[0].Y),
                    new Vector2(mm[1].X, mm[0].Y),
                    new Vector2(mm[0].X, mm[1].Y),
                    new Vector2(mm[1].X, mm[1].Y) },
            mm[0].Z, mm[1].Z,
            sides[0],
            sides[1],
            size);
        }

        int IsHitY(Vector3[] mm, float[][] sides, int size)
        {
            return IsHit(new Vector2[4] {
                    new Vector2(mm[0].X, 1 - mm[0].Z),
                    new Vector2(mm[1].X, 1 - mm[0].Z),
                    new Vector2(mm[0].X, 1 - mm[1].Z),
                    new Vector2(mm[1].X, 1 - mm[1].Z) },
            mm[0].Y, mm[1].Y,
            sides[2],
            sides[3],
            size);
        }

        int IsHit(Vector2[] v, float z0, float z1, float[] side0, float[] side1, int size)
        {
            int hitcnt = 0;
            for (int i = 0; i < v.Length; ++i)
            {
                int w = (int)((v[i].X) * (size - 1));
                int h = (int)((v[i].Y) * (size - 1));
                float d0 = side0[h * size + w];
                float d1 = side1[h * size + w];
                bool hit0 =
                    (d0 != 0) && (d0 < z0) &&
                    (d1 != 0) && (d1 > z0);
                hitcnt += hit0 ? 1 : 0;
                bool hit1 =
                    (d0 != 0) && (d0 < z1) &&
                    (d1 != 0) && (d1 > z1);
                hitcnt += hit1 ? 1 : 0;
            }

            return hitcnt;
        }

        Oct(int minlevel, int maxlevel, Loc cl, float[][] sides, int size)
        {
            l = cl;

            if (l.lev < minlevel)
            {
                n = new Oct[8];
                for (int i = 0; i < 8; ++i)
                {
                    n[i] = new Oct(minlevel, maxlevel, l[i], sides, size);
                }
            }
            else
            {
                int hitcntX = IsHitX(l.GetBox(), sides, size);
                int hitcntY = hitcntX; // IsHitY(l.GetBox(), sides, size);
                if (hitcntX == 0 || hitcntY == 0)
                {
                    this.visible = false;
                }
                else if ((hitcntX == 8 && hitcntY == 8) ||
                    l.lev == maxlevel)
                {
                    this.visible = true;
                }
                else
                {
                    n = new Oct[8];
                    for (int i = 0; i < 8; ++i)
                    {
                        n[i] = new Oct(minlevel, maxlevel, l[i], sides, size);
                    }
                }
            }
        }

        public void GetLeafNodes(List<Oct> leafs)
        {
            if (this.n == null)
            {
                if (this.visible) leafs.Add(this);
            }
            else
            {
                foreach (var c in n)
                { c.GetLeafNodes(leafs); }
            }
        }

        public int CompareTo(Oct other)
        {
            return l.CompareTo(other.l);
        }

        public override string ToString()
        {
            return l.ToString();
        }
    }

    static class OctViz
    {
        public static VertexArray BuildVA(Program program, Oct topNode)
        {
            uint[] indices = new uint[_Cube.Length];
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
            int sides = _Cube.Length / 6;
            for (int i = 0; i < sides; ++i)
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
                indices[i] = (uint)i;
                Vector3 xdir = xdirs[i / 12];
                Vector3 ydir = ydirs[i / 12];
                int sideIdx = i / 6;
                texCoords[i] = new Vector3(Vector3.Dot(_Cube[i], xdir),
                    Vector3.Dot(_Cube[i], ydir), (float)sideIdx / 6.0f);
            }

            List<Oct> leafs = new List<Oct>();
            topNode.GetLeafNodes(leafs);
            leafs.Sort();
            Vector4[] ind0 = leafs.Select(l => l.l.GetPosScale()).ToArray();
            Vector4[] ind1 = leafs.Select(l => new Vector4(l.color, 0)).ToArray();
            return new VertexArray(program, _Cube, indices, nrmCoords, ind0, ind1);
        }


        private static readonly Vector3[] _Cube = new Vector3[] {
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

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

            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(-1.0f, -1.0f, 1.0f),  // 4
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(1.0f, -1.0f, 1.0f),  // 5          
        };
    }


}

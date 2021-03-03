using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Vox
{    
    struct Loc : IComparable<Loc>, IEqualityComparer<Loc>
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


        public Loc GetParentAtLevel(int l)
        {
            if (l >= lev)
                return this;
            int d = lev - l;
            return new Loc(l, x >> d, y >> d, z >> d);
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
            int l = lev.CompareTo(other.lev);
            if (l != 0) return l;
            int c = z.CompareTo(other.z);
            if (c != 0) return c;
            c = y.CompareTo(other.y);
            if (c != 0) return c;
            return x.CompareTo(other.x);
        }

        public Vector4 GetPosScale()
        {
            float scale = 1.0f / (float)(1 << lev);
            return new Vector4((x + 0.5f) * scale, (y + 0.5f) * scale, (z + 0.5f) * scale, scale);
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

        public bool Equals(Loc x, Loc y)
        {
            return x.lev == y.lev && x.x == y.x &&
                x.y == y.y && x.z == y.z;
        }

        public int GetHashCode(Loc obj)
        {
            return (int)(obj.lev * 1313 * obj.x * 7 + obj.y * 3 +
                obj.z * 131);
        }
    }

    class Oct : IComparable<Oct>
    {

        enum HitResult
        {
            eAllOutside,
            ePartial,
            eAllInside
        }

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

        public static List<Loc> clocs = new List<Loc>();
        public bool Collapse()
        {
            clocs.Add(l);
            if (n == null)
                return true;

            int visiblecnt = 0;
            bool cancollapse = true;
            for (int i = 0; i < 8; ++i)
            {
                cancollapse &= n[i].Collapse();
                visiblecnt += n[i].visible ? 1 : 0;
            }

            if (cancollapse && (visiblecnt == 0 || visiblecnt == 8))
            {
                if (visiblecnt == 8)
                {
                    Vector3 nColor = Vector3.Zero;
                    for (int i = 0; i < 8; ++i)
                    {
                        nColor += n[i].color;
                    }

                    this.color = nColor / 8.0f;
                }
                n = null;
                visible = visiblecnt == 8 ? true : false;
                return true;
            }

            return false;
        }

        public Oct(int minlevel, int maxlevel, MMTex[] sides, int size) : this(minlevel, maxlevel, new Loc(0, 0, 0, 0), sides, size, 7)
        { }


        public Oct GetAtLoc(Loc _l)
        {
            if (_l.lev == l.lev)
                return this;

            if (n == null)
                return null;

            Loc p = _l.GetParentAtLevel(l.lev + 1);
            foreach (Oct o in n)
            {
                if (o.l.Equals(p))
                    return o.GetAtLoc(_l);
            }

            return null;
        }


        HitResult IsHitX(Vector3[] mm, MMTex[] sides, int lod, out float c)
        {
            return IsHit(
                    new Vector2((mm[0].X + mm[1].X) * 0.5f,
                    (mm[0].Y + mm[1].Y) * 0.5f),
            mm[0].Z, mm[1].Z,
            sides[0],
            sides[1],
            lod,
            out c);
        }

        HitResult IsHitY(Vector3[] mm, MMTex[] sides, int lod, out float c)
        {
            return IsHit(
                    new Vector2((mm[0].X + mm[1].X) * 0.5f,
                    1 - ((mm[0].Z + mm[1].Z) * 0.5f)),
            mm[0].Y, mm[1].Y,
            sides[2],
            sides[3],
            lod,
            out c);
        }

        HitResult IsHitZ(Vector3[] mm, MMTex[] sides, int lod, out float c)
        {
            return IsHit(
                    new Vector2((mm[0].Z + mm[1].Z) * 0.5f,
                    (mm[0].Y + mm[1].Y) * 0.5f),
            1 - mm[1].X, 1 - mm[0].X,
            sides[4],
            sides[5],
            lod,
            out c);
        }

        HitResult IsHit(Vector2 v, float nearz, float farz, MMTex near, MMTex far, int lod, out float c)
        {
            int size = 1 << lod;
            int mip = lod - 2;
            int w = (int)((v.X) * (size - 1));
            int h = (int)((v.Y) * (size - 1));
            float nearmax = near[mip][h * size + w].r;
            float nearmin = near[mip][h * size + w].g;
            float farmax = far[mip][h * size + w].r;
            float farmin = far[mip][h * size + w].g;
            if (nearz >= farmax ||
                farz <= nearmin)
            {
                c = 0;
                return HitResult.eAllOutside;
            }

            float a0 = near[mip][h * size + w].b;
            c = a0 > 0 ? near[mip][h * size + w].b :
                far[mip][h * size + w].b;
            if (nearz > nearmax && 
                farz < farmin)
            {
                return HitResult.eAllInside; 
            }

            return HitResult.ePartial;
        }

        Vector3 Decode(float c)
        {
            return new Vector3(c % 1, (c / 256) % 1, (c / (256 * 256)));
        }

        Oct(int minlevel, int maxlevel, Loc cl, MMTex[] sides, int size, int xyzmask)
        {
            l = cl;

            this.visible = false;
            if (l.lev < minlevel)
            {
                n = new Oct[8];
                for (int i = 0; i < 8; ++i)
                {
                    n[i] = new Oct(minlevel, maxlevel, l[i], sides, size, xyzmask);
                }
            }
            else
            {
                bool skipx = (xyzmask & 1) == 0;
                bool skipy = (xyzmask & 2) == 0;
                bool skipz = (xyzmask & 4) == 0;
                float cx = 0;
                float cy = 0;
                float cz = 0;
                HitResult hrX = skipx ? HitResult.eAllInside : IsHitX(l.GetBox(), sides, l.lev, out cx);
                HitResult hrY = skipy ? HitResult.eAllInside : IsHitY(l.GetBox(), sides, l.lev, out cy);
                HitResult hrZ = skipz ? HitResult.eAllInside : IsHitZ(l.GetBox(), sides, l.lev, out cz);                      
                if ((hrX == HitResult.eAllInside && hrY == HitResult.eAllInside && hrZ == HitResult.eAllInside)
                    || (l.lev >= maxlevel && hrX != HitResult.eAllOutside && hrY != HitResult.eAllOutside &&
                    hrZ != HitResult.eAllOutside))
                {
                    Vector3 c = Vector3.Zero;
                    if (cx > 0)
                        c = Decode(cx);
                    if (cy > 0)
                        c = Decode(cy);
                    if (cz > 0)
                        c = Decode(cz);
                    this.color = c;
                    this.visible = true;
                }
                else if ((hrX == HitResult.ePartial || hrY == HitResult.ePartial || hrZ == HitResult.ePartial) &&
                    l.lev < maxlevel)
                {
                    n = new Oct[8];
                    for (int i = 0; i < 8; ++i)
                    {
                        n[i] = new Oct(minlevel, maxlevel, l[i], sides, size, xyzmask);
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
        public static VertexArray BuildVA(Oct topNode)
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
                Vector3 nrm = Vector3.Normalize(Vector3.Cross(d1, d2));
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
            return new VertexArray(_Cube, indices, nrmCoords, ind0, ind1);
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

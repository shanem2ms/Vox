﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

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

    class OctBuffer
    {
        public uint nextWriteIdx;
        const int stgSize = 1 << 20;
        const int maxstg = 32;
        public Oct[][] nodes = new Oct[maxstg][];

        public OctBuffer()
        {
            for (int idx = 0; idx < maxstg; ++idx)
                nodes[idx] = new Oct[stgSize];
        }
            
        public uint CreateOct(Loc loc)
        {
            uint idx = Interlocked.Increment(ref nextWriteIdx) - 1;

            uint storageIdx = idx / stgSize;
            uint nodeidx = idx % stgSize;
            nodes[storageIdx][nodeidx] = new Oct(loc);
            return idx;
        }
          
        public Oct this[uint idx] => nodes[idx / stgSize][idx % stgSize];

        Oct Top => nodes[0][0];

        public void GetLeafNodes(List<Oct> leafs)
        {
            Top.GetLeafNodes(this, leafs);
        }

        public void Build(MMTex[] sides)
        {
            CreateOct(new Loc(0, 0, 0, 0));
            this[0].Build(this, null, 7, false);

            int baseLod = sides[0].baseLod;
            int lodLevels = sides[0].baseLod + sides[0].Length;
            uint curIdx = 1;
            for (int lod = 2; lod < lodLevels; ++lod)
            {
                uint buildTo = nextWriteIdx;
                Rgba32[][] sidesLod = lod >= baseLod ? sides.Select(mm => mm.data[lod - baseLod]).ToArray() : null;

                Thread[] threads = new Thread[28];
                uint nextReadIdx = curIdx;
                for (int idx = 0; idx < threads.Length; ++idx)
                {
                    threads[idx] = new Thread(() =>
                    {
                        uint readIdx = Interlocked.Increment(ref nextReadIdx) - 1;
                        if (readIdx >= buildTo)
                            return;
                        while (readIdx < buildTo)
                        {
                            this[readIdx].Build(this, sidesLod, 7, lod == (lodLevels - 1));
                            readIdx = Interlocked.Increment(ref nextReadIdx) - 1;
                        }
                    });

                    threads[idx].Start();
                }

                for (int idx = 0; idx < threads.Length; ++idx)
                {
                    threads[idx].Join();
                }
                curIdx = buildTo;
            }                                             
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
        public uint[] n;
        public bool isleaf = false;
        public bool visible = false;

        public Oct(Loc loc) { l = loc; }

        public void Build(OctBuffer buf, Rgba32[][] sides, int xyzmask, bool lastLevel)
        {
            if (isleaf) return;
            this.visible = false;
            if (sides == null)
            {
                n = new uint[8];
                for (int i = 0; i < 8; ++i)
                {
                    n[i] = buf.CreateOct(l[i]);
                }
            }
            else
            {
                n = new uint[8];
                for (int i = 0; i < 8; ++i)
                {
                    Loc cl = l[i];
                    bool skipx = (xyzmask & 1) == 0;
                    bool skipy = (xyzmask & 2) == 0;
                    bool skipz = (xyzmask & 4) == 0;
                    float cx = 0;
                    float cy = 0;
                    float cz = 0;
                    HitResult hrX = skipx ? HitResult.eAllInside : IsHitX(cl.GetBox(), sides, cl.lev, out cx);
                    HitResult hrY = skipy ? HitResult.eAllInside : IsHitY(cl.GetBox(), sides, cl.lev, out cy);
                    HitResult hrZ = skipz ? HitResult.eAllInside : IsHitZ(cl.GetBox(), sides, cl.lev, out cz);
                    if ((hrX == HitResult.eAllInside && hrY == HitResult.eAllInside && hrZ == HitResult.eAllInside)
                        || (lastLevel && hrX != HitResult.eAllOutside && hrY != HitResult.eAllOutside &&
                        hrZ != HitResult.eAllOutside))
                    {
                        Vector3 c = Vector3.Zero;
                        if (cx > 0)
                            c = Decode(cx);
                        if (cy > 0)
                            c = Decode(cy);
                        if (cz > 0)
                            c = Decode(cz);
                        n[i] = buf.CreateOct(cl);
                        buf[n[i]].color = c;
                        buf[n[i]].isleaf = true;
                        buf[n[i]].visible = true;
                    }
                    else if ((hrX == HitResult.ePartial || hrY == HitResult.ePartial || hrZ == HitResult.ePartial) &&
                        !lastLevel)
                    {
                        n[i] = buf.CreateOct(cl);
                    }
                }                                
            }
        }

        public bool Collapse(OctBuffer buf)
        {
            if (n == null)
                return true;

            int visiblecnt = 0;
            bool cancollapse = true;
            for (int i = 0; i < 8; ++i)
            {
                cancollapse &= buf[n[i]].Collapse(buf);
                visiblecnt += buf[n[i]].visible ? 1 : 0;
            }

            if (cancollapse && (visiblecnt == 0 || visiblecnt == 8))
            {                                                                                                                                                                                 

                if (visiblecnt == 8)
                {
                    Vector3 nColor = Vector3.Zero;
                    for (int i = 0; i < 8; ++i)
                    {
                        nColor += buf[n[i]].color;
                    }

                    this.color = nColor / 8.0f;
                }
                n = null;
                visible = visiblecnt == 8 ? true : false;
                return true;
            }

            return false;
        }



        public Oct GetAtLoc(OctBuffer buf, Loc _l)
        {
            if (_l.lev == l.lev)
                return this;

            if (n == null)
                return null;

            Loc p = _l.GetParentAtLevel(l.lev + 1);
            foreach (uint o in n)
            {
                if (buf[o].l.Equals(p))
                    return buf[o].GetAtLoc(buf, _l);
            }

            return null;
        }


        HitResult IsHitX(Vector3[] mm, Rgba32[][] sides, int lod, out float c)
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

        HitResult IsHitY(Vector3[] mm, Rgba32[][] sides, int lod, out float c)
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

        HitResult IsHitZ(Vector3[] mm, Rgba32[][] sides, int lod, out float c)
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

        HitResult IsHit(Vector2 v, float nearz, float farz, Rgba32[] near, Rgba32[] far, int lod, out float c)
        {
            int size = 1 << lod;
            int w = (int)((v.X) * (size - 1));
            int h = (int)((v.Y) * (size - 1));
            float nearmax = near[h * size + w].r;
            float nearmin = near[h * size + w].g;
            float farmax = far[h * size + w].r;
            float farmin = far[h * size + w].g;
            if (nearz >= farmax ||
                farz <= nearmin)
            {
                c = 0;
                return HitResult.eAllOutside;
            }

            float a0 = near[h * size + w].b;
            c = a0 > 0 ? near[h * size + w].b :                                                                                                                    
                far[h * size + w].b;
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

        public void GetLeafNodes(OctBuffer buf, List<Oct> leafs)
        {
            if (this.isleaf)
            {
                if (this.visible) leafs.Add(this);
            }
            else
            {
                foreach (uint idx in n)
                {
                    if (idx == 0)
                        continue;
                    buf[idx].GetLeafNodes(buf, leafs); }
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

    }

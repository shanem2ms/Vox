Texture2D Side0;
Texture2D Side1;
Texture2D Side2;
Texture2D Side3;
Texture2D Side4;
Texture2D Side5;


struct Ctr {
  uint nextReadIdx;
  uint nextWriteIdx;
  uint readToIdx;
  uint xyzmask;
};

RWStructuredBuffer<Ctr> ctr : register(u0);


struct Loc
{
    uint lev;
    uint x;
    uint y;
    uint z;    
};

/*
uint lev
uint x
uint y
uint z
uint child[8]
uint flags
uint color
*/
struct OctNode
{
    Loc l;
    uint child[8];
    uint flags;
    uint color;
};


RWStructuredBuffer<OctNode> nodes : register(u1);;


struct OctCube
{
    float4 pos;
};

RWStructuredBuffer<OctCube> cubes : register(u2);



Loc Loc_GetChild(Loc p, uint i)
{
    Loc lc;
    lc.lev = p.lev + 1;
    lc.x = p.x * 2 + (i & 1);
    lc.y = p.y * 2 + ((i >> 1) & 1);
    lc.z = p.z * 2 + ((i >> 2) & 1);
    return lc;
}

void Loc_GetBox(Loc l, out float3 v0, out float3 v1)
{
    float scale = 1.0f / float(1 << l.lev);
    v0 = float3(l.x * scale, l.y * scale, l.z * scale);
    v1 = float3((l.x + 1) * scale, (l.y + 1) * scale, (l.z + 1) * scale);
}


static const int eAllInside = 1;
static const int eAllOutside = 2;
static const int ePartial = 3;

int IsHit(float2 v, float nearz, float farz, Texture2D near, Texture2D far, uint lod, out float c)
{
    int size = 1 << lod;
    int w = int((v.x) * (size - 1));
    int h = int((v.y) * (size - 1));

    float4 nearcolor = near.Load(int3(w, h, 0));
    float4 farcolor = far.Load(int3(w, h, 0));
    float nearmax = nearcolor.r;
    float nearmin = nearcolor.g;
    float farmax = farcolor.r;
    float farmin = farcolor.g;
    if (nearz >= farmax ||
        farz <= nearmin)
    {
        c = 0;
        return eAllOutside;
    }

    float a0 = nearcolor.b;
    c = a0 > 0 ? nearcolor.b : farcolor.b;
    if (nearz > nearmax && farz < farmin)
    {
        return eAllInside; 
    }

    return ePartial;
}


int IsHitX(float3 mm0, float3 mm1, Texture2D sides0, Texture2D sides1, uint lod, out float c)
{
    return IsHit(
            float2((mm0.x + mm1.x) * 0.5f,
            (mm0.y + mm1.y) * 0.5f),
    mm0.z, mm1.z,
    sides0,
    sides1,
    lod,
    c);
}

int IsHitY(float3 mm0, float3 mm1, Texture2D sides2, Texture2D sides3, uint lod, out float c)
{
    return IsHit(
            float2((mm0.x + mm1.x) * 0.5f,
            1 - ((mm0.z + mm1.z) * 0.5f)),
    mm0.y, mm1.y,
    sides2,
    sides3,
    lod,
    c);
}

int IsHitZ(float3 mm0, float3 mm1, Texture2D sides4, Texture2D sides5, uint lod, out float c)
{
    return IsHit(
            float2((mm0.z + mm1.z) * 0.5f,
            (mm0.y + mm1.y) * 0.5f),
    1 - mm1.x, 1 - mm0.x,
    sides4,
    sides5,
    lod,
    c);
}


uint Oct_Create(Loc l, bool isLeaf, float3 c)
{
    uint n;    
    InterlockedAdd(ctr[0].nextWriteIdx, 1, n);
    OctNode newnode;
    newnode.l = l;
    newnode.flags = isLeaf ? 1 : 0;
    newnode.color = 0;
    for (int i = 0; i < 8; ++i)
        newnode.child[i] = 0;


    nodes[n] = newnode;
    return n;
}

float3 Decode(float c)
{
    return float3(1,1,1);
}

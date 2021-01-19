using Assimp;
using System;
using System.IO;
using System.Numerics;
using Veldrid;

namespace Common
{
    public class Model
    {
        private const PostProcessSteps DefaultPostProcessSteps =
            PostProcessSteps.FlipWindingOrder | PostProcessSteps.Triangulate | PostProcessSteps.PreTransformVertices
            | PostProcessSteps.CalculateTangentSpace | PostProcessSteps.GenerateSmoothNormals;

        public DeviceBuffer VertexBuffer { get; private set; }
        public DeviceBuffer IndexBuffer { get; private set; }
        public IndexFormat IndexFormat { get; private set; } = IndexFormat.UInt32;
        public uint IndexCount { get; private set; }
        public uint VertexCount { get; private set; }

        public Model(
            GraphicsDevice gd,
            ResourceFactory factory,
            string filename,
            VertexElementSemantic[] elementSemantics,
            ModelCreateInfo? createInfo,
            PostProcessSteps flags = DefaultPostProcessSteps)
        {
            using (FileStream fs = File.OpenRead(filename))
            {
                string extension = Path.GetExtension(filename);
                Init(gd, factory, fs, extension, elementSemantics, createInfo, flags);
            }
        }

        public Model(
            GraphicsDevice gd,
            ResourceFactory factory,
            Stream stream,
            string extension,
            VertexElementSemantic[] elementSemantics,
            ModelCreateInfo? createInfo,
            PostProcessSteps flags = DefaultPostProcessSteps)
        {
            Init(gd, factory, stream, extension, elementSemantics, createInfo, flags);
        }

        private void Init(
            GraphicsDevice gd,
            ResourceFactory factory,
            Stream stream,
            string extension,
            VertexElementSemantic[] elementSemantics,
            ModelCreateInfo? createInfo,
            PostProcessSteps flags = DefaultPostProcessSteps)
        {
            // Load file
            AssimpContext assimpContext = new AssimpContext();
            Scene pScene = assimpContext.ImportFileFromStream(stream, DefaultPostProcessSteps, extension);

            parts.Clear();
            parts.Count = (uint)pScene.Meshes.Count;

            Vector3 scale = new Vector3(1.0f);
            Vector2 uvscale = new Vector2(1.0f);
            Vector3 center = new Vector3(0.0f);
            if (createInfo != null)
            {
                scale = createInfo.Value.Scale;
                uvscale = createInfo.Value.UVScale;
                center = createInfo.Value.Center;
            }

            RawList<float> vertices = new RawList<float>();
            RawList<uint> indices = new RawList<uint>();

            VertexCount = 0;
            IndexCount = 0;

            // Load meshes
            for (int i = 0; i < pScene.Meshes.Count; i++)
            {
                var paiMesh = pScene.Meshes[i];

                parts[i] = new ModelPart();
                parts[i].vertexBase = VertexCount;
                parts[i].indexBase = IndexCount;

                VertexCount += (uint)paiMesh.VertexCount;

                var pColor = pScene.Materials[paiMesh.MaterialIndex].ColorDiffuse;

                Vector3D Zero3D = new Vector3D(0.0f, 0.0f, 0.0f);
                Vector3 min = new Vector3(float.MaxValue);
                Vector3 max = new Vector3(float.MinValue);
                Vector3[] vtxarray = new Vector3[paiMesh.VertexCount];
                for (int j = 0; j < paiMesh.VertexCount; j++)
                {
                    Vector3D pPos = paiMesh.Vertices[j];
                    vtxarray[j] = new Vector3(pPos.X * scale.X + center.X,
                        -pPos.Y * scale.Y + center.Y,
                        pPos.Z * scale.Z + center.Z);

                    max.X = Math.Max(vtxarray[j].X, max.X);
                    max.Y = Math.Max(vtxarray[j].Y, max.Y);
                    max.Z = Math.Max(vtxarray[j].Z, max.Z);

                    min.X = Math.Min(vtxarray[j].X, min.X);
                    min.Y = Math.Min(vtxarray[j].Y, min.Y);
                    min.Z = Math.Min(vtxarray[j].Z, min.Z);
                }

                Vector3 scl = max - min;
                Vector3 ori = (max + min) * 0.5f;
                float md = Math.Max(scl.X, Math.Max(scl.Y, scl.Z));
                md = 2.0f / md;

                for (int j = 0; j < paiMesh.VertexCount; j++)
                {
                    Vector3 v = vtxarray[j];
                    vtxarray[j] = (v - ori) * md;
                    dim.Max.X = Math.Max(vtxarray[j].X, dim.Max.X);
                    dim.Max.Y = Math.Max(vtxarray[j].Y, dim.Max.Y);
                    dim.Max.Z = Math.Max(vtxarray[j].Z, dim.Max.Z);

                    dim.Min.X = Math.Min(vtxarray[j].X, dim.Min.X);
                    dim.Min.Y = Math.Min(vtxarray[j].Y, dim.Min.Y);
                    dim.Min.Z = Math.Min(vtxarray[j].Z, dim.Min.Z);
                }

                dim.Size = dim.Max - dim.Min;

                for (int j = 0; j < paiMesh.VertexCount; j++)
                {
                    Vector3 pPos = vtxarray[j];
                    Vector3D pNormal = paiMesh.Normals[j];
                    Vector3D pTexCoord = paiMesh.HasTextureCoords(0) ? paiMesh.TextureCoordinateChannels[0][j] : Zero3D;
                    Vector3D pTangent = paiMesh.HasTangentBasis ? paiMesh.Tangents[j] : Zero3D;
                    Vector3D pBiTangent = paiMesh.HasTangentBasis ? paiMesh.BiTangents[j] : Zero3D;

                    foreach (VertexElementSemantic component in elementSemantics)
                    {
                        switch (component)
                        {
                            case VertexElementSemantic.Position:
                                vertices.Add(pPos.X);
                                vertices.Add(pPos.Y);
                                vertices.Add(pPos.Z);
                                break;
                            case VertexElementSemantic.Normal:
                                vertices.Add(pNormal.X);
                                vertices.Add(-pNormal.Y);
                                vertices.Add(pNormal.Z);
                                break;
                            case VertexElementSemantic.TextureCoordinate:
                                vertices.Add(pTexCoord.X * uvscale.X);
                                vertices.Add(pTexCoord.Y * uvscale.Y);
                                break;
                            case VertexElementSemantic.Color:
                                vertices.Add(pColor.R);
                                vertices.Add(pColor.G);
                                vertices.Add(pColor.B);
                                break;
                            default: throw new System.NotImplementedException();
                        };
                    }
                }

                parts[i].vertexCount = (uint)paiMesh.VertexCount;

                uint indexBase = indices.Count;
                for (uint j = 0; j < paiMesh.FaceCount; j++)
                {
                    Face Face = paiMesh.Faces[(int)j];
                    if (Face.IndexCount != 3)
                        continue;
                    indices.Add(indexBase + (uint)Face.Indices[0]);
                    indices.Add(indexBase + (uint)Face.Indices[1]);
                    indices.Add(indexBase + (uint)Face.Indices[2]);
                    parts[i].indexCount += 3;
                    IndexCount += 3;
                }
            }


            uint vBufferSize = (vertices.Count) * sizeof(float);
            uint iBufferSize = (indices.Count) * sizeof(uint);

            VertexBuffer = factory.CreateBuffer(new BufferDescription(vBufferSize, BufferUsage.VertexBuffer));
            IndexBuffer = factory.CreateBuffer(new BufferDescription(iBufferSize, BufferUsage.IndexBuffer));

            gd.UpdateBuffer(VertexBuffer, 0, ref vertices[0], vBufferSize);
            gd.UpdateBuffer(IndexBuffer, 0, ref indices[0], iBufferSize);
        }

        public struct ModelPart
        {
            public uint vertexBase;
            public uint vertexCount;
            public uint indexBase;
            public uint indexCount;
        }

        RawList<ModelPart> parts = new RawList<ModelPart>();

        public struct Dimension
        {
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 Size;
            public Dimension(Vector3 min, Vector3 max) { Min = min; Max = max; Size = new Vector3(); }
        }

        public Dimension dim = new Dimension(new Vector3(float.MaxValue), new Vector3(float.MinValue));

        public struct ModelCreateInfo
        {
            public Vector3 Center;
            public Vector3 Scale;
            public Vector2 UVScale;

            public ModelCreateInfo(Vector3 scale, Vector2 uvScale, Vector3 center)
            {
                Center = center;
                Scale = scale;
                UVScale = uvScale;
            }

            public ModelCreateInfo(float scale, float uvScale, float center)
            {
                Center = new Vector3(center);
                Scale = new Vector3(scale);
                UVScale = new Vector2(uvScale);
            }
        }
    }
}
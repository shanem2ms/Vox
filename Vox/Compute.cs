using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Numerics;
using Veldrid;
using Veldrid.SPIRV;

namespace Vox
{
    public class ComputeOct
    {
        public const int OctCount = 1024;

        private DeviceBuffer _ctrbuf;
        private DeviceBuffer _octTree;
        private DeviceBuffer _octCubes;
        private Shader _buildBaseOctCS;
        private Pipeline _buildBaseOctPipeline;
        private Shader _buildOctCS;
        private Pipeline _buildOctPipeline;
        private Shader _setValsCS;
        private Pipeline _setValsPipeline;
        private Shader _setVals2CS;
        private Pipeline _setVals2Pipeline;
        private Shader _writeCubesCS;
        private Pipeline _writeCubesPipeline;
        private ResourceSet _storageResourceSet;
        private ResourceSet[] _imgResourceSets;
        private bool _initialized;
        private TextureView[][] sides;
        private DeviceBuffer cubesVB;
        private DeviceBuffer cubesStg;
        public DeviceBuffer OctCubes => cubesVB;        

        [StructLayout(LayoutKind.Sequential)]
        struct OctLoc
        {
            public uint lev;
            public uint x;
            public uint y;
            public uint z;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct OctNode
        {
            OctLoc loc;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            uint[] nodes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            uint[] data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Ctr
        {
            public uint nextReadIdx;
            public uint nextWriteIdx;
            public uint readToIdx;
            public uint xyzmask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OctCube
        {
            public Vector4 posscl;
        }

        const int MaxOctNodes = 1 << 18;
        uint levels = 5;
        public void CreateResources(ResourceFactory factory, TextureView[][] _sides)
        {
            levels = (uint)_sides[0].Length + 2;
            sides = _sides;
            uint ctrlen = (uint)Marshal.SizeOf<Ctr>();
            _ctrbuf = factory.CreateBuffer(new BufferDescription(ctrlen, BufferUsage.StructuredBufferReadWrite,
                ctrlen));

            uint nodeSize = (uint)Marshal.SizeOf<OctNode>();
            _octTree = factory.CreateBuffer(
                            new BufferDescription(
                                nodeSize * MaxOctNodes,
                                BufferUsage.StructuredBufferReadWrite,
                                nodeSize));

            uint cubesize = (uint)Marshal.SizeOf<OctCube>();
            _octCubes = factory.CreateBuffer(
                            new BufferDescription(
                                cubesize * MaxOctNodes,
                                BufferUsage.StructuredBufferReadWrite,
                                cubesize));

            cubesVB = Utils.Factory.CreateBuffer(new BufferDescription(cubesize * MaxOctNodes, BufferUsage.VertexBuffer));

            ResourceLayout imgLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                          new ResourceLayoutElementDescription("Side0", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                          new ResourceLayoutElementDescription("Side1", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                          new ResourceLayoutElementDescription("Side2", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                          new ResourceLayoutElementDescription("Side3", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                          new ResourceLayoutElementDescription("Side4", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                          new ResourceLayoutElementDescription("Side5", ResourceKind.TextureReadOnly, ShaderStages.Compute)
                      ));

            ResourceLayout octStorageLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Ctr", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute),
                new ResourceLayoutElementDescription("Oct", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute),
                new ResourceLayoutElementDescription("Cubes", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)
                ));

            _storageResourceSet = factory.CreateResourceSet(new ResourceSetDescription(octStorageLayout,
                _ctrbuf, _octTree, _octCubes));

            _imgResourceSets = new ResourceSet[_sides[0].Length];
            for (int idx = 0; idx < _imgResourceSets.Length; ++idx)
            {
                int lod = _sides[0].Length - idx - 1;
                _imgResourceSets[idx] = factory.CreateResourceSet(new ResourceSetDescription(imgLayout,
                    sides[0][lod],
                    sides[1][lod],
                    sides[2][lod],
                    sides[3][lod],
                    sides[4][lod],
                    sides[5][lod]));
            }


            _buildBaseOctCS = factory.CreateShader(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytes("BuildBase.cso"),
                "main"));

            ComputePipelineDescription computePipelineDesc = new ComputePipelineDescription(
                _buildBaseOctCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _buildBaseOctPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _buildOctCS = factory.CreateShader(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytes("BuildOctTree.cso"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _buildOctCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _buildOctPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _setValsCS = factory.CreateShader(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytes("SetVals.cso"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _setValsCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _setValsPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _setVals2CS = factory.CreateShader(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytes("SetVals2.cso"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _setVals2CS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _setVals2Pipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _writeCubesCS = factory.CreateShader(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytes("WriteCubes.cso"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _writeCubesCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _writeCubesPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            InitResources(factory, levels);
            _initialized = true;
        }

        private void InitResources(ResourceFactory factory, uint levels)
        {
            Ctr ubo = new Ctr()
            {
                nextReadIdx = 0,
                nextWriteIdx = 1,
                readToIdx = 1,
                xyzmask = (levels << 4) | 1
            };
            Utils.Cl.UpdateBuffer(_ctrbuf, 0, ref ubo);
        }

        DeviceBuffer stgBuf;
        public void RunCompute()
        {
            if (!_initialized) { return; }

            for (int i = 0; i < 2; ++i)
            {
                Utils.Cl.SetPipeline(_buildBaseOctPipeline);
                Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
                Utils.Cl.Dispatch(8, 1, 1);

                Utils.Cl.SetPipeline(_setValsPipeline);
                Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
                Utils.Cl.Dispatch(1, 1, 1);
            }
            for (int i = 0; i < (levels - 2); ++i)
            {
                Utils.Cl.SetPipeline(_buildOctPipeline);
                Utils.Cl.SetComputeResourceSet(0, _imgResourceSets[i]);
                Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
                Utils.Cl.Dispatch((uint)(1 << (i + 4)), 1, 1);

                Utils.Cl.SetPipeline(_setValsPipeline);
                Utils.Cl.SetComputeResourceSet(0, _imgResourceSets[i]);
                Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
                Utils.Cl.Dispatch(1, 1, 1);
            }

            Utils.Cl.SetPipeline(_setVals2Pipeline);
            Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
            Utils.Cl.Dispatch(1, 1, 1);

            Utils.Cl.SetPipeline(_writeCubesPipeline);
            Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
            Utils.Cl.Dispatch(MaxOctNodes / 64, 1, 1);

            uint ctrlen = (uint)Marshal.SizeOf<Ctr>();
            stgBuf = Utils.Factory.CreateBuffer(new BufferDescription(ctrlen, BufferUsage.Staging));
            Utils.Cl.CopyBuffer(_ctrbuf, 0, stgBuf, 0, ctrlen);

            uint cubesize = (uint)Marshal.SizeOf<OctCube>();
            cubesStg = Utils.Factory.CreateBuffer(new BufferDescription(cubesize * MaxOctNodes, BufferUsage.Staging));
            Utils.Cl.CopyBuffer(_octCubes, 0, cubesVB, 0, cubesize * MaxOctNodes);
        }

        public uint CubeCnt()
        {
            MappedResourceView<Ctr> ctrarr = Utils.G.Map<Ctr>(stgBuf, MapMode.Read);
            return ctrarr[0].nextWriteIdx;
       }
    }
}

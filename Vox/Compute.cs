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
        private Shader _writeCubesCS;
        private Pipeline _writeCubesPipeline;
        private ResourceSet _storageResourceSet;
        private ResourceSet[] _imgResourceSets;
        private bool _initialized;
        private TextureView[][] sides;

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
            public bool isbaselod;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct OctCube
        {
            public Vector4 posscl;
        }

        const int MaxOctNodes = 1 << 18;
        public void CreateResources(ResourceFactory factory, TextureView[][] _sides)
        {
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


            _buildBaseOctCS = factory.CreateFromSpirv(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytesPP(Utils.G, "Compute", "BUILDBASEOCT"),
                "main"));

            ComputePipelineDescription computePipelineDesc = new ComputePipelineDescription(
                _buildBaseOctCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _buildBaseOctPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _buildOctCS = factory.CreateFromSpirv(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytesPP(Utils.G, "Compute", "BUILDOCT"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _buildOctCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _buildOctPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _setValsCS = factory.CreateFromSpirv(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytesPP(Utils.G, "Compute", "SETVALS"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _setValsCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _setValsPipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _writeCubesCS = factory.CreateFromSpirv(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytesPP(Utils.G, "Compute", "WRITECUBES"),
                "main"));

            computePipelineDesc = new ComputePipelineDescription(
                _writeCubesCS,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _writeCubesPipeline = factory.CreateComputePipeline(ref computePipelineDesc);
            

            InitResources(factory);
            _initialized = true;
        }

        private void InitResources(ResourceFactory factory)
        {
            Ctr ubo = new Ctr()
            {
                nextReadIdx = 0,
                nextWriteIdx = 1,
                isbaselod = true,
                readToIdx = 1,
                xyzmask = 7
            };
            Utils.Cl.UpdateBuffer(_ctrbuf, 0, ref ubo);
        }


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
            for (int i = 0; i < 6; ++i)
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

            Utils.Cl.SetPipeline(_writeCubesPipeline);
            Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
            Utils.Cl.Dispatch(1, 1, 1);
        }
    }
}

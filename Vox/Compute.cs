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
        private Shader _computeShader;
        private Pipeline _computePipeline;
        private ResourceSet _storageResourceSet;
        private ResourceSet []_imgResourceSets;
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
            public uint nodeptr;
        }


        const int MaxOctNodes = 2048;
        public void CreateResources(ResourceFactory factory, TextureView[][] _sides)
        {
            sides = _sides;
            _ctrbuf = factory.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<Ctr>(), BufferUsage.StructuredBufferReadWrite, 
                (uint)Marshal.SizeOf<Ctr>()));

            uint nodeSize = (uint)Marshal.SizeOf<OctNode>();
            _octTree = factory.CreateBuffer(
                            new BufferDescription(
                                nodeSize * MaxOctNodes,
                                BufferUsage.StructuredBufferReadWrite,
                                nodeSize));

            _computeShader = factory.CreateFromSpirv(new ShaderDescription(
                ShaderStages.Compute,
                Utils.LoadShaderBytes(Utils.G, "Compute"),
                "main"));

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
                new ResourceLayoutElementDescription("Oct", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)
                ));

            ComputePipelineDescription computePipelineDesc = new ComputePipelineDescription(
                _computeShader,
                new[] { imgLayout, octStorageLayout },
                1, 1, 1);
            _computePipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _storageResourceSet = factory.CreateResourceSet(new ResourceSetDescription(octStorageLayout, 
                _ctrbuf, _octTree));

            _imgResourceSets = new ResourceSet[_sides[0].Length];
            for (int lod = 0; lod < _imgResourceSets.Length; ++lod)
            {
                _imgResourceSets[lod] = factory.CreateResourceSet(new ResourceSetDescription(imgLayout,
                    sides[0][lod],
                    sides[1][lod],
                    sides[2][lod],
                    sides[3][lod],
                    sides[4][lod],
                    sides[5][lod]));
            }

            InitResources(factory);
            _initialized = true;
        }

        private void InitResources(ResourceFactory factory)
        {
            Ctr ubo = new Ctr() { nodeptr = 0 };
            Utils.Cl.UpdateBuffer(_ctrbuf, 0, ref ubo);
        }


        public void RunCompute()
        {
            if (!_initialized) { return; }

            Utils.Cl.SetPipeline(_computePipeline);
            Utils.Cl.SetComputeResourceSet(0, _imgResourceSets[0]);
            Utils.Cl.SetComputeResourceSet(1, _storageResourceSet);
            Utils.Cl.Dispatch(256, 1, 1);
        }
    }
}

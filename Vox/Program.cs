using System;
using SampleBase;

namespace Vox
{
    class Program
    {
        static void Main(string[] args)
        {
            VeldridStartupWindow window = new VeldridStartupWindow("Vox");
            VoxMain offscreen = new VoxMain(window);
            window.Run();
        }
    }
}

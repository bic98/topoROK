using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoSmooth
{
    public class topoSmoothInfo : GH_AssemblyInfo
    {
        public override string Name => "topoSmooth";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("c760f710-a441-4291-845d-57135eb676c6");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoContour
{
    public class topoContourInfo : GH_AssemblyInfo
    {
        public override string Name => "topoContour";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("23231d58-515e-4c4a-aa65-f143132068fb");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
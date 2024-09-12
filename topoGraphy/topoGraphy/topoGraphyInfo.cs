using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoGraphy
{
    public class topoGraphyInfo : GH_AssemblyInfo
    {
        public override string Name => "topoGraphy";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("a0e4a700-04ce-463f-8037-9c5caedc1769");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
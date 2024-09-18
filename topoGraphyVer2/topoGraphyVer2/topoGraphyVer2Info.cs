using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoGraphyVer2
{
    public class topoGraphyVer2Info : GH_AssemblyInfo
    {
        public override string Name => "topoGraphyVer2";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("e455e70b-c850-4027-b133-cafafab8e0d4");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoROK
{
    public class topoROKInfo : GH_AssemblyInfo
    {
        public override string Name => "topoROK";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("aad6fea8-fd4c-4fd1-bf78-44831263f511");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
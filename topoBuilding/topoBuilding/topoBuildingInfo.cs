using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoBuilding
{
    public class topoBuildingInfo : GH_AssemblyInfo
    {
        public override string Name => "topoBuilding";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("e4c31a02-53ad-42d3-a5c7-e192c838a861");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace topoRiver
{
    public class topoRiverInfo : GH_AssemblyInfo
    {
        public override string Name => "topoRiver";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("abaa4e51-f229-42bd-8504-913a166bec46");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}
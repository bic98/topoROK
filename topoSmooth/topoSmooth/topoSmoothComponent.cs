using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace topoSmooth
{
    public class topoSmoothComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public topoSmoothComponent()
          : base("topoSmooth", "topoSmooth",
            "Description",
            "topoKorea", "terrain")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("topoSurface", "TS", "topoSurface to contour", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "Boolean", GH_ParamAccess.item); 
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("topoBox", "topoBox", "topoBox", GH_ParamAccess.item);
            pManager.AddMeshParameter("topoBoxMesh", "topoBoxMesh", "topoBoxMesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Variables
            Surface TS = null;
            bool Run = false;
            // Input
            if (!DA.GetData(0, ref TS)) { return; }
            if (!DA.GetData(1, ref Run)) { return; }
            // Output
            Brep topoBox = new Brep();
            Mesh topoBoxMesh = new Mesh();

            if (!Run) return;
            
            var edges = TS.ToBrep().Edges.ToList();

            // Get bounding box and its corners
            BoundingBox bbox = TS.GetBoundingBox(true);
            Point3d[] corners = bbox.GetCorners();

            // Find max and min Z points
            Point3d maxZPoint = corners.OrderByDescending(pt => pt.Z).FirstOrDefault();
            Point3d minZPoint = corners.OrderBy(pt => pt.Z).FirstOrDefault();
            minZPoint = new Point3d(minZPoint.X, minZPoint.Y, minZPoint.Z - 3);

            // Create a point below the min Z point
            Point3d boxStPt = new Point3d(minZPoint.X, minZPoint.Y, minZPoint.Z - 100);
            Plane pl = Plane.WorldXY;
            pl.Origin = boxStPt;
            
            List<Brep> breps = new List<Brep>() { TS.ToBrep() };
            List<Curve> curves = new List<Curve>();
            foreach (var i in edges)
            {
                Curve upCrv = i.DuplicateCurve(); 
                Curve dnCrv = Curve.ProjectToPlane(upCrv, pl);
                var sideSrf = NurbsSurface.CreateRuledSurface(upCrv, dnCrv);
                breps.Add(sideSrf.ToBrep());
                curves.Add(dnCrv);
            }

            breps.Add(PlanarSrf(Curve.JoinCurves(curves)[0]));
            var union = Brep.JoinBreps(breps, 0.01);
            
            MeshingParameters meshingParams = new MeshingParameters();
            meshingParams.RefineGrid = true;
            Mesh tmp = new Mesh();
            var mesh = Mesh.CreateFromBrep(union[0], meshingParams);
            tmp.Append(mesh);
            topoBox= union[0]; 
            topoBoxMesh = tmp;
            DA.SetData(0, topoBox); 
            DA.SetData(1, topoBoxMesh);
        }
        
        public Brep PlanarSrf(Curve c)
        {
            string log;
            Brep ret = null;
            if (c.IsValidWithLog(out log) && c.IsPlanar() && c.IsClosed)
            {
                var tmp = Brep.CreatePlanarBreps(c, 0.01);
                ret = tmp[0];
                return ret;
            }
            return ret;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.topoBox; 
        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("76a7339d-7563-4dd8-be21-66d081aa38af");
    }
}
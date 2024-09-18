using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace topoContour
{
    public class topoContourComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public topoContourComponent()
            : base("topoContour", "topocontour",
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
            pManager.AddIntegerParameter("zInterval", "zInterval", "interval of contour", GH_ParamAccess.item, 10);
            pManager.AddBooleanParameter("Run", "Run", "Boolean", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("contour", "contourCurve", "contour", GH_ParamAccess.list);
            pManager.AddBrepParameter("contourBrep", "contourBrep", "contourBrep", GH_ParamAccess.list);
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
            int zInterval = 10;
            bool Run = false;

            // Input
            if (!DA.GetData(0, ref TS) || TS == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid or missing topographical surface.");
                return;
            }

            if (!DA.GetData(1, ref zInterval))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid or missing zInterval.");
                return;
            }

            if (!DA.GetData(2, ref Run) || !Run)
            {
                // Not running
                return;
            }

            // Validate Surface
            if (!TS.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Topographical surface is invalid.");
                return;
            }

            // Convert Surface to Brep
            Brep tsBrep = TS.ToBrep();
            if (tsBrep == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to convert surface to Brep.");
                return;
            }

            var edges = tsBrep.Edges;
            if (edges == null || edges.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Surface has no edges.");
                return;
            }

            // Get bounding box and its corners
            BoundingBox bbox = tsBrep.GetBoundingBox(true);
            if (!bbox.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid bounding box.");
                return;
            }

            Point3d[] corners = bbox.GetCorners();

            // Find max and min Z points
            Point3d maxZPoint = corners.OrderByDescending(pt => pt.Z).FirstOrDefault();
            Point3d minZPoint = corners.OrderBy(pt => pt.Z).FirstOrDefault();
            minZPoint = new Point3d(minZPoint.X, minZPoint.Y, minZPoint.Z - 3);

            // Create a point below the min Z point
            Point3d boxStPt = new Point3d(minZPoint.X, minZPoint.Y, minZPoint.Z - 100);
            Plane pl = new Plane(boxStPt, Vector3d.ZAxis);

            // Initialize lists for breps and curves
            List<Brep> breps = new List<Brep>() { tsBrep };
            List<Curve> curves = new List<Curve>();

            double maxLength = 0.0;

            // Process edges
            foreach (var edge in edges)
            {
                if (edge == null)
                    continue;

                Curve upCrv = edge.DuplicateCurve();
                if (upCrv == null || !upCrv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid edge curve encountered.");
                    continue;
                }

                Curve dnCrv = Curve.ProjectToPlane(upCrv, pl);
                if (dnCrv == null || !dnCrv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to project curve to plane.");
                    continue;
                }

                // Create ruled surface between curves
                Surface sideSrf = NurbsSurface.CreateRuledSurface(upCrv, dnCrv);
                if (sideSrf == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to create ruled surface.");
                    continue;
                }

                Brep sideBrep = sideSrf.ToBrep();
                if (sideBrep == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to convert surface to Brep.");
                    continue;
                }

                breps.Add(sideBrep);
                curves.Add(dnCrv);

                double length = dnCrv.GetLength();
                if (length > maxLength)
                {
                    maxLength = length;
                }
            }

            // Create bottom surface
            Curve[] joinedCurves = Curve.JoinCurves(curves);
            if (joinedCurves == null || joinedCurves.Length == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to join curves for bottom surface.");
                return;
            }

            Brep bottomBrep = PlanarSrf(joinedCurves[0]);
            if (bottomBrep == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to create bottom planar surface.");
                return;
            }

            breps.Add(bottomBrep);

            // Union all breps
            Brep[] union = Brep.JoinBreps(breps, 0.01);
            if (union == null || union.Length == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to union Breps.");
                return;
            }

            // Remesh the unioned brep
            QuadRemeshParameters quadRemeshParams = new QuadRemeshParameters
            {
                TargetEdgeLength = 10.0
            };
            Mesh cutted = Mesh.QuadRemeshBrep(union[0], quadRemeshParams);
            if (cutted == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "QuadRemesh failed.");
                return;
            }

            // Create contour curves
            Curve[] contourCurves = Mesh.CreateContourCurves(cutted, minZPoint, maxZPoint, zInterval, 0.001);
            if (contourCurves == null || contourCurves.Length == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to create contour curves.");
                return;
            }

            // Initialize collection for extruded surfaces
            ConcurrentBag<Brep> extrudedSurfaces = new ConcurrentBag<Brep>();

            // Process contour curves
            Parallel.For(0, contourCurves.Length, i =>
            {
                try
                {
                    var curve = contourCurves[i];
                    if (curve == null || !curve.IsValid || !curve.IsClosed)
                    {
                        // Skip invalid or open curves
                        return;
                    }

                    var areaProps = AreaMassProperties.Compute(curve);
                    if (areaProps == null || areaProps.Area < 30)
                    {
                        // Skip small area curves
                        return;
                    }

                    // Extrude the curve
                    Extrusion extrusion = Extrude(curve, zInterval);
                    if (extrusion == null)
                    {
                        // Skip if extrusion fails
                        return;
                    }

                    Brep brep = extrusion.ToBrep();
                    if (brep == null)
                    {
                        // Skip if conversion to Brep fails
                        return;
                    }

                    extrudedSurfaces.Add(brep);
                }
                catch (Exception ex)
                {
                    // Log exception details
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Error processing contour at index {i}: {ex.Message}");
                }
            });

            // Output results
            DA.SetDataList(0, contourCurves.ToList());
            DA.SetDataList(1, extrudedSurfaces.ToList());
        }

        public static Extrusion Extrude(Curve curve, double height)
        {
            if (curve == null || !curve.IsValid)
                return null;

            return Extrusion.Create(curve, height, true);
        }

        public Brep PlanarSrf(Curve c)
        {
            if (c == null || !c.IsValid || !c.IsPlanar() || !c.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid curve for PlanarSrf.");
                return null;
            }

            Brep[] breps = Brep.CreatePlanarBreps(c, 0.01);
            if (breps == null || breps.Length == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to create planar Brep.");
                return null;
            }

            return breps[0];
        }



        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.topoContour;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("27144a1e-47a3-4ff9-8af0-331b57f5c572");
    }
}
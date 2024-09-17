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
            if (!DA.GetData(0, ref TS)) return;
            if (!DA.GetData(1, ref zInterval)) return;
            if (!DA.GetData(2, ref Run)) return;
            // Process
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

            // Initialize lists for breps and curves
            List<Brep> breps = new List<Brep>() { TS.ToBrep() };
            List<Curve> curves = new List<Curve>();

            double maxLength = 0.0;
            foreach (var i in edges)
            {
                Curve upCrv = i.DuplicateCurve();
                Curve dnCrv = Curve.ProjectToPlane(upCrv, pl);
                var sideSrf = NurbsSurface.CreateRuledSurface(upCrv, dnCrv);
                breps.Add(sideSrf.ToBrep());
                curves.Add(dnCrv);
                double length = dnCrv.GetLength();
                if (length > maxLength)
                {
                    maxLength = length;
                }
            }

            // Create bottom surface
            breps.Add(PlanarSrf(Curve.JoinCurves(curves)[0]));

            // Union all breps
            var union = Brep.JoinBreps(breps, 0.01);
            QuadRemeshParameters quadRemeshParams = new QuadRemeshParameters();
            quadRemeshParams.TargetEdgeLength = 10.0; 
            var cutted = Mesh.QuadRemeshBrep(union[0], quadRemeshParams); 

            var contourCurves = Mesh.CreateContourCurves(cutted, minZPoint, maxZPoint, zInterval, 0.001);
            // int dist = (int)maxLength + 100;
            //
            // Brep[] planarSurfaces = new Brep[contourCurves.Length];
            // Parallel.For(0, contourCurves.Length, i =>
            // {
            //     var curve = contourCurves[i];
            //     if(curve.IsClosed == false) return;
            //     BoundingBox bb = curve.GetBoundingBox(false);
            //     if(!bb.IsValid) return;
            //     Plane plane = new Plane(bb.Center, Vector3d.ZAxis);
            //     Surface planarSurface = new PlaneSurface(plane, new Interval(-dist, dist), new Interval(-dist, dist));
            //     planarSurfaces[i] = planarSurface.ToBrep();
            // });
            ConcurrentBag<Brep> extrudedSurfaces = new ConcurrentBag<Brep>();
            Parallel.For(0, contourCurves.Length, i =>
            {
                var curve = contourCurves[i];
                if(curve.IsClosed == false) return;
                if (AreaMassProperties.Compute(curve).Area < 300) return;
                Point3d now = curve.PointAtEnd;
                Point3d nxt = MovePt(now, Vector3d.ZAxis, zInterval);
                // Curve nxtCurve = curve.DuplicateCurve();
                // MoveOrientPoint(nxtCurve, now, nxt);
                // var sideSrf = NurbsSurface.CreateRuledSurface(curve, nxtCurve);
                // if (sideSrf == null) return;
                // var paper = planarSurfaces[i];
                // var cutter = sideSrf.ToBrep();
                // var cuttedSrf = paper.Split(cutter, 0.001);
                //
                // if (cuttedSrf != null && cuttedSrf.Length > 0)
                // {
                //     Brep caps = cuttedSrf.Last();
                //     Brep caps2 = caps.DuplicateBrep(); 
                //     MoveOrientPoint(caps, now, nxt);
                //     // extrudedSurfaces.Add(caps);
                //     // extrudedSurfaces.Add(cutter);
                //     IEnumerable<Brep> tmp = new Brep[] { caps, cutter, caps2}; 
                //     var joinbrep = Brep.JoinBreps(tmp, 0.001);
                //     if (joinbrep != null)
                //     {
                //         extrudedSurfaces.Add(joinbrep[0]); 
                //     }
                // }
                var sideSrf = Extrude(curve, zInterval);
                if (sideSrf == null) return;
                extrudedSurfaces.Add(sideSrf.ToBrep());
            });
            DA.SetDataList(0, contourCurves.ToList()); 
            DA.SetDataList(1, extrudedSurfaces.ToList());
        }
        public static Extrusion Extrude(Curve curve, double height)
        {
            return Extrusion.Create(curve, height, true); // extrusion 생성 및 반환
        }
        
        public T MoveOrientPoint<T>(T obj, Point3d now, Point3d nxt) where T : GeometryBase
        {
            Plane baseNow = Plane.WorldXY;
            Plane st = new Plane(now, baseNow.XAxis, baseNow.YAxis);
            Plane en = new Plane(nxt, baseNow.XAxis, baseNow.YAxis);
            Transform orient = Transform.PlaneToPlane(st, en);
            obj.Transform(orient);
            return obj;
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

        public Point3d MovePt(Point3d p, Vector3d v, double amp)
        {
            v.Unitize();
            Point3d newPoint = new Point3d(p);
            newPoint.Transform(Transform.Translation(v * amp));
            return newPoint;
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
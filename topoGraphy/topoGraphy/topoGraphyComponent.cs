using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;

namespace topoGraphy
{
    public class topoGraphyComponent : GH_Component
    {
        public topoGraphyComponent()
            : base("topoGraphy", "topography",
                "Description",
                "topoKorea", "terrain")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("terrainLines", "TL", "Curve", GH_ParamAccess.tree);
            pManager.AddNumberParameter("terrainLinesZ", "TLZ", "Number", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("resolution", "Resolution", "int", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "Boolean", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddSurfaceParameter("topoSurface", "TS", "Surface", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Curve> terrainLines;
            GH_Structure<GH_Number> terrainLinesZ;
            int resolution = 0;
            bool run = false;
            if (!DA.GetDataTree(0, out terrainLines)) return;
            if (!DA.GetDataTree(1, out terrainLinesZ)) return;
            if (!DA.GetData(2, ref resolution)) return;
            if (!DA.GetData(3, ref run)) return;
            
            if (!run) return;

            DataTree<Curve> terrainLinesCopy = new DataTree<Curve>();
            foreach (GH_Path path in terrainLines.Paths)
            {
                List<GH_Curve> branch = terrainLines.get_Branch(path).Cast<GH_Curve>().ToList();
                List<Curve> curves = branch.Select(ghCurve => ghCurve.Value).ToList();
                terrainLinesCopy.AddRange(curves, path);
            }

            DataTree<double> terrainLinesZCopy = new DataTree<double>();
            foreach (GH_Path path in terrainLinesZ.Paths)
            {
                List<GH_Number> branch = terrainLinesZ.get_Branch(path).Cast<GH_Number>().ToList();
                List<double> numbers = branch.Select(ghNumber => ghNumber.Value).ToList();
                terrainLinesZCopy.AddRange(numbers, path);
            }
            List<List<Curve>> topoLineList = ConvertTreeToNestedList(terrainLinesCopy);
            List<List<double>> topoHeightList = ConvertTreeToNestedList(terrainLinesZCopy);
            List<GeometryBase> gemoList = new List<GeometryBase>();
            List<Point3d> allDividePts = new List<Point3d>();

            Parallel.For(0, topoLineList.Count, i =>
            {
                if (topoLineList[i] == null || topoHeightList[i] == null) return;

                Curve now = topoLineList[i][0].DuplicateCurve(); 
                if (now == null || !now.IsValid) return;

                Curve nxt = now.DuplicateCurve();
                double height = topoHeightList[i][0]; 
                Point3d st = now.PointAtStart;
                Point3d en = MovePt(st, Vector3d.ZAxis, height);
                MoveOrientPoint(nxt, st, en);

                lock (gemoList)
                {
                    gemoList.Add(nxt);
                }

                Point3d[] divLenPts;
                nxt.DivideByLength(10, false, out divLenPts);
                if (divLenPts != null)
                {
                    lock (allDividePts)
                    {
                        allDividePts.AddRange(divLenPts);
                    }
                }
            });
            Brep bbox = CalculateBoundingBox(gemoList).ToBrep();
            var segRectangle = bbox.Faces;
            var sortedRectangle = segRectangle.OrderBy(x => x.GetBoundingBox(false).Center.Z).ToList();
            var bottomCrv = Curve.JoinCurves(sortedRectangle[0].ToBrep().Edges)[0];
            var bottomCrvOffset = OffsetCurve(bottomCrv, Plane.WorldXY, 30);

            var findNearestCrv = bbox.Edges
                .Where(edge =>
                {
                    Vector3d edgeDirection = edge.PointAtEnd - edge.PointAtStart;
                    edgeDirection.Unitize();
                    return Math.Abs(edgeDirection * Vector3d.ZAxis - 1) < RhinoMath.ZeroTolerance;
                })
                .Select(edge => edge.ToNurbsCurve())
                .ToList();

            Parallel.For(0, findNearestCrv.Count, i =>
            {
                var now = findNearestCrv[i];
                double t;

                Point3d closestPt = allDividePts.OrderBy(pt =>
                {
                    now.ClosestPoint(pt, out t);
                    return now.PointAt(t).DistanceTo(pt);
                }).First();

                now.ClosestPoint(closestPt, out t);
                var nxt = now.PointAt(t);

                lock (allDividePts)
                {
                    allDividePts.Add(nxt);
                }
            });

            var delMesh = CreateDelaunayMesh(allDividePts);
            int uCount = 0, vCount = 0;
            var ptsSrf = DivideSurface(PlanarSrf(bottomCrvOffset), resolution, ref uCount, ref vCount);

            Point3d?[,] sortedPtsDelMesh = new Point3d?[uCount + 1, vCount + 1];

            Parallel.For(0, ptsSrf.Count, i =>
            {
                var now = ptsSrf[i];
                Ray3d ray = new Ray3d(new Point3d(now.X, now.Y, now.Z - 1000.0), Vector3d.ZAxis);
                Point3d? intersectionPoint = MeshRay(delMesh, ray);
                if (intersectionPoint != null)
                {
                    int uIndex = i / (vCount + 1);
                    int vIndex = i % (vCount + 1);
                    sortedPtsDelMesh[uIndex, vIndex] = intersectionPoint.Value;
                }
            });

            List<Point3d> ptsDelMesh = new List<Point3d>(uCount * vCount);
            for (int u = 0; u <= uCount; u++)
            {
                for (int v = 0; v <= vCount; v++)
                {
                    var point = sortedPtsDelMesh[u, v];
                    if (point.HasValue)
                    {
                        ptsDelMesh.Add(point.Value);
                    }
                }
            }

            var finalTopo = SurfaceFromPoints(ptsDelMesh, uCount + 1, vCount + 1);
            DA.SetData(0, finalTopo);
        }

        public static DataTree<T> MakeDataTree2D<T>(List<List<T>> ret)
        {
            DataTree<T> tree = new DataTree<T>();
            for (int i = 0; i < ret.Count; i++)
            {
                GH_Path path = new GH_Path(i);
                for (int j = 0; j < ret[i].Count; j++)
                {
                    tree.Add(ret[i][j], path);
                }
            }

            return tree;
        }

        public Surface SurfaceFromPoints(List<Point3d> points, int uCount, int vCount)
        {
            if (points == null || points.Count < 4)
                throw new ArgumentException("At least 4 points are required to create a surface.");
            if (uCount * vCount != points.Count)
                throw new ArgumentException("The number of points must be equal to uCount * vCount.");

            return NurbsSurface.CreateFromPoints(points, uCount, vCount, 3, 3);
        }

        public static Point3d? MeshRay(Mesh mesh, Ray3d ray)
        {
            if (mesh == null || !mesh.IsValid)
                return null;

            int[] t;
            var intersection = Rhino.Geometry.Intersect.Intersection.MeshRay(mesh, ray, out t);
            return intersection >= 0 ? (Point3d?)ray.PointAt(intersection) : null;
        }

        public int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }

            return Math.Abs(a);
        }

        public Surface PlanarSrf(Curve c)
        {
            if (c.IsValid && c.IsPlanar() && c.IsClosed)
            {
                var tmp = Brep.CreatePlanarBreps(c, 0.01);
                return tmp[0].Faces[0];
            }

            return null;
        }

        public List<Point3d> DivideSurface(Surface surface, int k, ref int uCount, ref int vCount)
        {
            List<Point3d> pointsOnSurface = new List<Point3d>();
            int ul = (int)surface.Domain(0).Length / 100 * 100;
            int vl = (int)surface.Domain(1).Length / 100 * 100;
            int gcd = Gcd(ul, vl);
            var candidates = GetDivisors(gcd);
            int u = ul / candidates[Math.Min(candidates.Count - 1, k)];
            int v = vl / candidates[Math.Min(candidates.Count - 1, k)];

            double uSteps = surface.Domain(0).Length / u;
            double vSteps = surface.Domain(1).Length / v;

            pointsOnSurface.Capacity = (u + 1) * (v + 1);

            for (int i = 0; i <= u; i++)
            {
                for (int j = 0; j <= v; j++)
                {
                    double uParam = surface.Domain(0).T0 + i * uSteps;
                    double vParam = surface.Domain(1).T0 + j * vSteps;
                    pointsOnSurface.Add(surface.PointAt(uParam, vParam));
                }
            }

            uCount = u;
            vCount = v;
            return pointsOnSurface;
        }

        public List<int> GetDivisors(int n)
        {
            List<int> divisors = new List<int>();

            for (int i = 1; i <= Math.Sqrt(n); i++)
            {
                if (n % i == 0)
                {
                    divisors.Add(i);
                    if (i != n / i)
                        divisors.Add(n / i);
                }
            }

            return divisors.Where(x => x >= 10).OrderByDescending(x => x).ToList();
        }

        public static Mesh CreateDelaunayMesh(List<Point3d> pts)
        {
            if (pts == null || pts.Count < 3)
            {
                return null;
            }

            var nodes = new Grasshopper.Kernel.Geometry.Node2List();
            for (int i = 0; i < pts.Count; i++)
            {
                nodes.Append(new Grasshopper.Kernel.Geometry.Node2(pts[i].X, pts[i].Y));
            }

            var faces = new List<Grasshopper.Kernel.Geometry.Delaunay.Face>();
            faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Faces(nodes, 0);
            var delMesh = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 0, ref faces);

            for (int i = 0; i < pts.Count; i++)
            {
                delMesh.Vertices.SetVertex(i, pts[i]);
            }

            return delMesh;
        }

        public Curve OffsetCurve(Curve c, Plane p, double interval)
        {
            var seg = c.DuplicateSegments();
            var joinseg = Curve.JoinCurves(seg);
            List<Curve> outLines = new List<Curve>(joinseg.Length);

            foreach (var js in joinseg)
                outLines.AddRange(c.Offset(p, interval, 0, CurveOffsetCornerStyle.Sharp));

            var ret = Curve.JoinCurves(outLines);
            return ret != null && ret.Length > 0 ? ret[0] : null;
        }

        public static BoundingBox CalculateBoundingBox(IEnumerable<GeometryBase> geometries)
        {
            BoundingBox bbox = BoundingBox.Empty;
            foreach (var geometry in geometries)
                bbox.Union(geometry.GetBoundingBox(true));

            return bbox;
        }

        public Point3d MovePt(Point3d p, Vector3d v, double amp)
        {
            v.Unitize();
            Point3d newPoint = new Point3d(p); 
            newPoint.Transform(Transform.Translation(v * amp));
            return newPoint;
        }

        public T MoveOrientPoint<T>(T obj, Point3d now, Point3d nxt) where T : GeometryBase
        {
            Plane st = new Plane(now, Plane.WorldXY.XAxis, Plane.WorldXY.YAxis);
            Plane en = new Plane(nxt, Plane.WorldXY.XAxis, Plane.WorldXY.YAxis);
            obj.Transform(Transform.PlaneToPlane(st, en));
            return obj;
        }

        public List<List<T>> ConvertTreeToNestedList<T>(DataTree<T> tree)
        {
            List<List<T>> nestedList = new List<List<T>>();
            foreach (GH_Path path in tree.Paths)
            {
                List<T> subList = new List<T>(tree.Branch(path));
                nestedList.Add(subList);
            }

            return nestedList;
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources.paper; 

        public override Guid ComponentGuid => new Guid("6883fb68-c537-4432-b32b-6c8e32e639e1");
    }
}
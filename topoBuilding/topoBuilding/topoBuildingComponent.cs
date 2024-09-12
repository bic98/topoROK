using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry.Intersect;

namespace topoBuilding
{
    public class topoBuildingComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public topoBuildingComponent()
            : base("topoBuilding", "topobuilding",
                "Description",
                "topoKorea", "other")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("topoSurface", "TS", "surface", GH_ParamAccess.item);
            pManager.AddCurveParameter("buildingLines", "BL", "Curve", GH_ParamAccess.tree);
            pManager.AddNumberParameter("buildingLinesZ", "BLZ", "Number", GH_ParamAccess.tree);
            pManager.AddTextParameter("buildingType", "RoofType", "Text", GH_ParamAccess.item);
            pManager.AddNumberParameter("removeUnderArea", "removeUnderArea", "Number", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "Boolean", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("buildingBreps", "BB", "Brep", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Curve> buildingLine = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> buildingLinesZ = new GH_Structure<GH_Number>();
            Surface topo3D = null;
            string type = null;
            double removeUnderArea = 0;
            bool run = false;
            
            if (!DA.GetData(0, ref topo3D)) return;
            if (!DA.GetDataTree(1, out buildingLine)) return;
            if (!DA.GetDataTree(2, out buildingLinesZ)) return;
            if (!DA.GetData(3, ref type)) return;
            if (!DA.GetData(4, ref removeUnderArea)) return;
            if (!DA.GetData(5, ref run)) return;

            if (!run) return;
            DataTree<Curve> buildingLinesCopy = new DataTree<Curve>();
            foreach (GH_Path path in buildingLine.Paths)
            {
                List<GH_Curve> branch = buildingLine.get_Branch(path).Cast<GH_Curve>().ToList();
                List<Curve> curves = branch.Select(ghCurve => ghCurve.Value).ToList();
                buildingLinesCopy.AddRange(curves, path);
            }

            DataTree<double> buildingLinesZCopy = new DataTree<double>();
            foreach (GH_Path path in buildingLinesZ.Paths)
            {
                List<GH_Number> branch = buildingLinesZ.get_Branch(path).Cast<GH_Number>().ToList();
                List<double> numbers = branch.Select(ghNumber => ghNumber.Value).ToList();
                buildingLinesZCopy.AddRange(numbers, path);
            }

            List<List<Curve>> buildingLines = ConvertTreeToNestedList(buildingLinesCopy);
            List<List<double>> buildingHeights = ConvertTreeToNestedList(buildingLinesZCopy);
            List<List<Curve>> pureLines = new List<List<Curve>>(buildingLines.Count);
            List<List<Point3d>> purePoints = new List<List<Point3d>>(buildingLines.Count);
            List<List<Point3d>> nxtPurePoints = new List<List<Point3d>>(buildingLines.Count);

            var raySrf = new List<GeometryBase>() { topo3D.ToBrep() };
            List<List<double>> pureHeights = new List<List<double>>(buildingLines.Count);
            List<List<double>> deem = new List<List<double>>(buildingLines.Count);

            // 병렬로 빌딩 라인 처리
            Parallel.For(0, buildingLines.Count, i =>
            {
                var now = buildingLines[i][0];
                var disconNow = Discontinuity(now);
                var rayPts = new List<Point3d>(disconNow.Count);
                var bulidArea = AreaMassProperties.Compute(now).Area;
                if (bulidArea < removeUnderArea) return;
                foreach (var disconPts in disconNow)
                {
                    Ray3d ray = new Ray3d(new Point3d(disconPts.X, disconPts.Y, disconPts.Z - 100.0), Vector3d.ZAxis);
                    var pts = Intersection.RayShoot(ray, raySrf, 1).ToList();
                    if (pts.Count != 0) rayPts.Add(pts[0]);
                }

                if (rayPts.Count != disconNow.Count) return;

                double minDist = double.MaxValue, maxDist = double.MinValue;
                int id = -1;

                for (int j = 0; j < rayPts.Count; j++)
                {
                    var dist = rayPts[j].DistanceTo(disconNow[j]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        id = j;
                    }

                    if (dist > maxDist)
                    {
                        maxDist = dist;
                    }
                }

                lock (nxtPurePoints)
                {
                    nxtPurePoints.Add(new List<Point3d>() { rayPts[id] });
                    purePoints.Add(new List<Point3d>() { disconNow[id] });
                    pureLines.Add(buildingLines[i]);
                    pureHeights.Add(buildingHeights[i]);
                    deem.Add(new List<double>() { maxDist - minDist });
                }
            });

            List<Brep> makeBuildings = new List<Brep>(pureLines.Count);
            List<Brep> makeBuildingsParapet = new List<Brep>(pureLines.Count);


            // 병렬 처리: 건물 생성 작업
            Parallel.For(0, pureLines.Count, i =>
            {
                var now = pureLines[i][0].DuplicateCurve(); 
                Plane pl;
                now.TryGetPlane(out pl);
                if (pl.ZAxis.Z < 0) now.Reverse();
                var nxt = MoveOrientPoint(now, purePoints[i][0], nxtPurePoints[i][0]);


                if (type == "flat")
                {
                    lock (makeBuildings)
                    {
                        makeBuildings.Add(Extrude(nxt, deem[i][0] + 3.5 * pureHeights[i][0]).ToBrep());
                    }
                }

                else if (type == "parapet")
                {
                    List<Brep> faces = new List<Brep>();
                    Curve nxtUpper = nxt.DuplicateCurve();
                    nxtUpper = MoveOrientPoint(nxtUpper, nxtPurePoints[i][0],
                        MovePt(nxtPurePoints[i][0], Vector3d.ZAxis, deem[i][0] + 3.5 * pureHeights[i][0]));
                    faces.Add(NurbsSurface.CreateRuledSurface(nxt, nxtUpper).ToBrep());

                    nxtUpper.TryGetPlane(out pl);
                    var nxtUpperParapet = OffsetCurve(nxtUpper, pl, -0.3);
                    if (nxtUpperParapet == null) return;
                    faces.AddRange(nxtUpperParapet);

                    var finalBuilding = JoinAndCapBreps(faces);
                    if (finalBuilding == null) return;


                    lock (makeBuildingsParapet)
                    {
                        makeBuildingsParapet.Add(finalBuilding);
                    }
                }
            });

            if (type == "flat")
            {
                DA.SetDataList(0, makeBuildings);
            }
            else if (type == "parapet")
            {
                DA.SetDataList(0, makeBuildingsParapet);
            }
        }

        public static Brep JoinAndCapBreps(List<Brep> breps)
        {
            if (breps == null || breps.Count == 0)
                return null;

            // Join the Breps
            Brep[] joinedBreps = Brep.JoinBreps(breps, Rhino.RhinoMath.ZeroTolerance);
            if (joinedBreps == null || joinedBreps.Length == 0)
                return null;

            // Cap the joined Brep
            Brep cappedBrep = joinedBreps[0].CapPlanarHoles(Rhino.RhinoMath.ZeroTolerance);
            return cappedBrep;
        }

        // 포인트를 주어진 벡터 방향으로 이동시키는 함수
        public Point3d MovePt(Point3d p, Vector3d v, double amp)
        {
            v.Unitize();
            Point3d newPoint = new Point3d(p);
            newPoint.Transform(Transform.Translation(v * amp));
            return newPoint;
        }
 

        // 주어진 커브로부터 평면 브렙 생성
        public Brep PlanarSrf(Curve c)
        {
            // 유효성 검사 및 평면 여부 확인
            if (c.IsValid && c.IsPlanar() && c.IsClosed)
            {
                var tmp = Brep.CreatePlanarBreps(c, 0.01);
                if (tmp != null && tmp.Length > 0)
                    return tmp[0]; // 유효한 브렙 반환
            }

            return null; // 유효하지 않으면 null 반환
        }

        // 커브를 오프셋하고 결과 브렙 생성
        public List<Brep> OffsetCurve(Curve c, Plane p, double interval)
        {
            var seg = c.DuplicateSegments(); // 커브 세그먼트 복제
            var joinseg = Curve.JoinCurves(seg); // 세그먼트를 조인
            List<Curve> outLines = new List<Curve>(joinseg.Length); // 리스트의 초기 용량 설정

            foreach (var js in joinseg)
            {
                var offset = js.Offset(p, interval, 0.001, CurveOffsetCornerStyle.Sharp);
                if (offset != null && offset.Length > 0)
                    outLines.AddRange(offset);
            }

            var ret = Curve.JoinCurves(outLines);
            if (ret == null || ret.Length == 0) return null; // 오프셋 결과가 유효하지 않은 경우

            var boundary = PlanarSrf(c);
            if (boundary == null) return null; // 평면 브렙이 생성되지 않은 경우

            var outBrep = boundary.Split(ret, 0.001); // 오프셋 커브로 분할
            if (outBrep == null || outBrep.Length == 0) return null; // 분할 결과가 없는 경우

            List<Brep> parapet = new List<Brep>() { outBrep[0] };
            foreach (var crv in ret)
            {
                var tmp = crv.DuplicateCurve();
                var nxt = MoveOrientPoint(tmp, crv.PointAtStart, MovePt(crv.PointAtStart, -Vector3d.ZAxis, 1.3));
                parapet.Add(NurbsSurface.CreateRuledSurface(crv, nxt).ToBrep());
            }

            return parapet; // 첫 번째 분할 결과 반환
        }

        // 주어진 커브를 주어진 높이로 extrusion(압출)하는 함수
        public static Extrusion Extrude(Curve curve, double height)
        {
            return Extrusion.Create(curve, height, true); // extrusion 생성 및 반환
        }

        // 커브의 불연속 점들을 찾는 함수
        public List<Point3d> Discontinuity(Curve x)
        {
            var seg = x.DuplicateSegments(); // 커브 세그먼트 복제
            List<Point3d> pts = new List<Point3d>(seg.Length + 1); // 리스트의 초기 용량 설정

            // 모든 세그먼트의 시작과 끝점을 추가
            foreach (var s in seg)
            {
                if (pts.Count == 0) pts.Add(s.PointAtStart);
                pts.Add(s.PointAtEnd);
            }

            // 닫힌 커브의 경우 마지막 중복 점 제거
            if (x.IsClosed) pts.RemoveAt(pts.Count - 1);
            return pts; // 불연속점 리스트 반환
        }

        // 오브젝트를 두 점 사이에서 이동시키는 함수
        public T MoveOrientPoint<T>(T obj, Point3d now, Point3d nxt) where T : GeometryBase
        {
            Plane st = new Plane(now, Plane.WorldXY.XAxis, Plane.WorldXY.YAxis); // 시작 평면
            Plane en = new Plane(nxt, Plane.WorldXY.XAxis, Plane.WorldXY.YAxis); // 목표 평면
            obj.Transform(Transform.PlaneToPlane(st, en)); // 변환 적용
            return obj; // 변환된 오브젝트 반환
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
            ;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("ee32684f-389b-4887-88be-2cb076d4acdd");
    }
}
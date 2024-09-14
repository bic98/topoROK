using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace topoStreet
{
    public class topoStreetComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public topoStreetComponent()
            : base("topoStreet", "topostreet",
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
            pManager.AddCurveParameter("streetLines", "SL", "Curve", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Run", "Run", "Boolean", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("streetCurve", "SC", "curve", GH_ParamAccess.list);
            pManager.AddCurveParameter("streetFlatCurve", "SFC", "curve", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Surface topo3D = null;
            GH_Structure<GH_Curve> streetLine = new GH_Structure<GH_Curve>();
            bool run = false;

            if (!DA.GetData(0, ref topo3D)) return;
            if (!DA.GetDataTree(1, out streetLine)) return;
            if (!DA.GetData(2, ref run)) return;

            if (!run) return;
            DataTree<Curve> streetCurves = new DataTree<Curve>();
            foreach (GH_Path path in streetLine.Paths)
            {
                List<GH_Curve> branch = streetLine.get_Branch(path).Cast<GH_Curve>().ToList();
                List<Curve> curves = branch.Select(ghCurve => ghCurve.Value).ToList();
                streetCurves.AddRange(curves, path);
            }

            List<Curve> streetLines = new List<Curve>();
            var sLine = ConvertTreeToNestedList(streetCurves);
            foreach (var i in sLine)
            {
                if (i.Count > 0)
                {
                    Curve curve = i[0].DuplicateCurve();
                    streetLines.Add(curve);
                }
            }

            var regionCurve = Curve.CreateBooleanUnion(streetLines, 0.001).ToList();
            var validCurves = new ConcurrentBag<Curve>();
            var topoBrep3D = topo3D.ToBrep();

            Parallel.ForEach(regionCurve, curve =>
            {
                var area = AreaMassProperties.Compute(curve).Area;
                if (area > 50)
                {
                    var tmp = Curve.ProjectToBrep(curve, topoBrep3D, Vector3d.ZAxis, 0.001);
                    if (tmp != null && tmp.Length > 0)
                    {
                        foreach (var t in tmp)
                        {
                            validCurves.Add(t);
                        }
                    }
                }
            });

            DA.SetDataList(0, validCurves);
            DA.SetDataList(1, regionCurve);
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

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.topoStreet; 

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("efce81aa-a9cc-4e4e-8fa1-79787b412880");
    }
}
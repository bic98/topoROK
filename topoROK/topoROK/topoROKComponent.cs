using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using DotSpatial.Data;
using Grasshopper.Kernel.Data;
using FeatureType = DotSpatial.Data.FeatureType;

namespace topoROK
{
    public class topoROKComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        ///
        /// 
        public topoROKComponent()
            : base("topoSHP", "toposhp",
                "Provides 3D representations of shapefiles from the National Geographic Information Institute of Korea.",
                "topoKorea", "inputData")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("SHP folder", "SHP", "Shp file folder link", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("TopoLines", "TL", "topology lines", GH_ParamAccess.tree);
            pManager.AddNumberParameter("TopoLinesZ", "TLZ", "topology lines Z", GH_ParamAccess.tree);
            pManager.AddCurveParameter("BuildingLines", "BL", "building flat lines", GH_ParamAccess.tree);
            pManager.AddNumberParameter("BuildingLinesZ", "BLZ", "building flat lines Z", GH_ParamAccess.tree);
            pManager.AddCurveParameter("StreetLines", "SL", "street flat lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Water Lines", "WL", "water flat lines", GH_ParamAccess.tree);
            pManager.AddPointParameter("Park Points", "P", "park pts", GH_ParamAccess.tree);
            pManager.AddTextParameter("Shapefile Dictionary", "Dict", "Shapefile dictionary contents", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> shpFolders = new List<string>();
            if (!DA.GetDataList(0, shpFolders)) return;

            Dictionary<string, HashSet<string>> shpFileDict = new Dictionary<string, HashSet<string>>();

            foreach (string i in shpFolders)
            {
                string folderPath = i;
                if (folderPath.Length > 2 && folderPath[0] == '\"' && folderPath[folderPath.Length - 1] == '\"')
                {
                    folderPath = folderPath.Substring(1, folderPath.Length - 2);
                }

                if (System.IO.Directory.Exists(folderPath))
                {
                    // Get all .shp files in the directory
                    string[] shpFiles = System.IO.Directory.GetFiles(folderPath, "*.shp");
                    foreach (var shpFile in shpFiles)
                    {
                        // Extract the filename from the path
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(shpFile);
                        // Split the filename by underscore and convert to a list
                        List<string> parts = fileName.Split('_').ToList();

                        // The key is the last part of the filename
                        if (parts.Count > 0)
                        {
                            string key = parts[parts.Count - 1]; // Get the last element

                            // If the key exists in the dictionary, add to the HashSet; otherwise, create a new entry
                            if (shpFileDict.ContainsKey(key))
                            {
                                shpFileDict[key].Add(shpFile); // HashSet prevents duplicates
                            }
                            else
                            {
                                shpFileDict[key] = new HashSet<string> { shpFile };
                            }
                        }
                    }
                }
            }
            
            // Get the topology files
            var topology = GetFiles("F0010000", shpFileDict);
            List<List<Curve>> tmpTopoLines = new List<List<Curve>>();
            List<List<double>> tmpTopoLinesZ = new List<List<double>>();
            foreach (var topoUnit in topology)
            {
                var shpTopoUnit = Shapefile.OpenFile(topoUnit, DataManager.DefaultDataManager.ProgressHandler);
                var topoFeatures = shpTopoUnit.Features;
                foreach (var item in topoFeatures)
                {
                    var typeTopo = item.FeatureType;
                    List<Point3d> tmp = new List<Point3d>();
                    if (typeTopo == FeatureType.Line)
                    {
                        var coods = item.Geometry.Coordinates;
                        foreach (var cood in coods)
                        {
                            tmp.Add(new Point3d(cood.X, cood.Y, 0));
                        }
                    }

                    if (tmp.Count > 0)
                    {
                        object itemValue = item.DataRow.ItemArray[1];
                        double parsedValue = -1.0; // 기본값 설정
                        if (itemValue != null && itemValue != DBNull.Value) // null 및 DBNull 값 체크
                        {
                            if (double.TryParse(itemValue.ToString(), out double result)) // 문자열 또는 다른 타입을 double로 변환 시도
                            {
                                parsedValue = result;
                            }
                        }
                        if (parsedValue > -1.0)
                        {
                            Polyline pl = new Polyline(tmp);
                            tmpTopoLinesZ.Add(new List<double> { parsedValue }); 
                            tmpTopoLines.Add(new List<Curve> { pl.ToNurbsCurve() });
                        }
                    }
                }
            }
            
            //get the building files
            var building = GetFiles("B0010000", shpFileDict);
            List<List<Curve>> tmpBuildingLines = new List<List<Curve>>();
            List<List<double>> tmpBuildingLinesZ = new List<List<double>>();
            // List<List<object>> test = new List<List<object>>();
            foreach (var buildingUnit in building)
            {
                var shpBuildingUnit = Shapefile.OpenFile(buildingUnit, DataManager.DefaultDataManager.ProgressHandler);
                var buildingFeatures = shpBuildingUnit.Features;
                foreach (var item in buildingFeatures)
                {
                    var typeBuilding = item.FeatureType;
                    List<Point3d> tmp = new List<Point3d>();
                    if (typeBuilding == FeatureType.Polygon)
                    {
                        var coods = item.Geometry.Coordinates;
                        foreach (var cood in coods)
                        {
                            tmp.Add(new Point3d(cood.X, cood.Y, 0));
                        }
                    }

                    if (tmp.Count > 0)
                    {
                        Polyline pl = new Polyline(tmp);
                        object itemValue = item.DataRow.ItemArray[5];
                        double parsedValue = 1.0; // 기본값 설정
                        if (itemValue != null && itemValue != DBNull.Value) // null 및 DBNull 값 체크
                        {
                            if (double.TryParse(itemValue.ToString(), out double result)) // 문자열 또는 다른 타입을 double로 변환 시도
                            {
                                parsedValue = result;
                            }
                        }
                        if (pl.IsClosed)
                        { 
                            tmpBuildingLinesZ.Add(new List<double> { parsedValue });
                            tmpBuildingLines.Add(new List<Curve> { pl.ToNurbsCurve() });
                        }
                    }
                }
            }
            
            //get the street files
            var street = GetFiles("A0010000", shpFileDict);
            List<List<Curve>> tmpStreetLines = new List<List<Curve>>();
            foreach (var streetUnit in street)
            {
                var shpStreetUnit = Shapefile.OpenFile(streetUnit, DataManager.DefaultDataManager.ProgressHandler);
                var streetFeatures = shpStreetUnit.Features;
                foreach (var item in streetFeatures)
                {
                    var typeStreet = item.FeatureType;
                    List<Point3d> tmp = new List<Point3d>();
                    if (typeStreet == FeatureType.Polygon)
                    {
                        var coods = item.Geometry.Coordinates;
                        foreach (var cood in coods)
                        {
                            tmp.Add(new Point3d(cood.X, cood.Y, 0));
                        }
                    }
                    if (tmp.Count > 0)
                    {
                        Polyline pl = new Polyline(tmp);
                        tmpStreetLines.Add(new List<Curve> { pl.ToNurbsCurve() });
                    }
                }
            }
            
            //get the water files
            var water = GetFiles("E0010001" , shpFileDict);
            List<List<Curve>> tmpWaterLines = new List<List<Curve>>();
            foreach (var waterUnit in water)
            {
                var shpWaterUnit = Shapefile.OpenFile(waterUnit, DataManager.DefaultDataManager.ProgressHandler);
                var waterFeatures = shpWaterUnit.Features;
                foreach (var item in waterFeatures)
                {
                    var typeWater = item.FeatureType;
                    List<Point3d> tmp = new List<Point3d>();
                    if (typeWater == FeatureType.Polygon)
                    {
                        var coods = item.Geometry.Coordinates;
                        foreach (var cood in coods)
                        {
                            tmp.Add(new Point3d(cood.X, cood.Y, 0));
                        }
                    }
                    if (tmp.Count > 0)
                    {
                        Polyline pl = new Polyline(tmp);
                        tmpWaterLines.Add(new List<Curve> { pl.ToNurbsCurve() });
                    }
                }
            }
            
            //get the park files
            var park = GetFiles("C0380000", shpFileDict);
            List<List<Point3d>> tmpParkPoints = new List<List<Point3d>>();
            List<List<object>> test = new List<List<object>>(); 
            foreach (var parkUnit in park)
            {
                var shpParkUnit = Shapefile.OpenFile(parkUnit, DataManager.DefaultDataManager.ProgressHandler);
                var parkFeatures = shpParkUnit.Features;
                foreach (var item in parkFeatures)
                {
                    var typePark = item.FeatureType;
                    if (typePark == FeatureType.Point)
                    {
                        var coods = item.Geometry.Coordinates;
                        string ret = item.DataRow.ItemArray.ToList()[1]?.ToString() ?? string.Empty; 
                        foreach (var cood in coods)
                        {
                            if (ret.Contains("공원"))
                            {
                                tmpParkPoints.Add(new List<Point3d> { new Point3d(cood.X, cood.Y, 0) });
                            }
                        }
                        test.Add(new List<object>{ret});
                    }
                }
            }
            
            List<string> outputList = shpFileDict
                .SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key}: {v}")) // 키-값 쌍을 텍스트로 변환
                .ToList();
            
            

            DataTree<Curve> topoTree = MakeDataTree2D(tmpTopoLines);
            DataTree<double> topoTreeZ = MakeDataTree2D(tmpTopoLinesZ);
            DataTree<Curve> buildingTree = MakeDataTree2D(tmpBuildingLines);
            DataTree<double> buildingTreeZ = MakeDataTree2D(tmpBuildingLinesZ);
            DataTree<Curve> streetTree = MakeDataTree2D(tmpStreetLines);
            DataTree<Curve> waterTree = MakeDataTree2D(tmpWaterLines);
            DataTree<Point3d> parkTree = MakeDataTree2D(tmpParkPoints);
            DA.SetDataTree(0, topoTree);
            DA.SetDataTree(1, topoTreeZ);
            DA.SetDataTree(2, buildingTree);
            DA.SetDataTree(3, buildingTreeZ);
            DA.SetDataTree(4, streetTree);
            DA.SetDataTree(5, waterTree);
            DA.SetDataTree(6, parkTree);
            DA.SetDataList(7, outputList);
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

        public static List<string> GetFiles(string key, Dictionary<string, HashSet<string>> hs)
        {
            List<string> resultList;
            if (hs.ContainsKey(key))
            {
                resultList = hs[key].ToList(); // Convert HashSet to List<string>
            }
            else
            {
                resultList = new List<string>(); // Handle the case where the key 'y' is not found
            }

            return resultList;
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.shpFile; 

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("3a881282-750a-4fe0-94ca-43c44493be7d");
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace GridBlockJoiner
{
    public class ContourTools
    {

        public class Spline 
        { 
         public double[] zs; 
         public double[] xs;          
         public double[] k;         
         public double[] a; 
 
         public Spline(List<double> pointsZ, List<double> pointsX) 
         { 

             int n = pointsZ.Count();
             zs = pointsZ.ToArray();
             xs = pointsX.ToArray();
             k = new double[n]; 
             a = new double[n]; 
            
            for (int i = 1; i < n; ++i)  
            { 
                 k[i] = zs[i] - zs[i - 1]; 
            } 
 
 
                 double[] sub = new double[n - 1]; 
                 double[] diag = new double[n - 1]; 
                 double[] sup = new double[n - 1]; 
 
 
                 for (int i = 1; i <= n - 2; ++i) 
                 { 
                     diag[i] = (k[i] + k[i + 1]) / 3; 
                     sup[i] = k[i + 1] / 6; 
                     sub[i] = k[i] / 6; 
                     a[i] = (xs[i + 1] - xs[i]) / k[i + 1] - (xs[i] - xs[i - 1]) / k[i]; 
                 } 
 
 
                 SolveTridiag(sub, diag, sup, ref a, n - 2); 
             }
          
 
 
         public double Interpolate(double zStar) 
         { 
             int interval = 0; 
             double prev = double.MinValue; 

 
             // At the end of this iteration, "gap" will contain the index of the interval 
             // between two known values, which contains the unknown z, and "previous" will 
            // contain the biggest z value among the known samples, left of the unknown z 
             for (int i = 0; i < zs.Length; i++) 
             { 
                 if (zs[i] < zStar && zs[i] > prev) 
                 {                    
                    prev = zs[i]; 
                    interval = i + 1; 
                 } 
             } 
 
 
             var z1 = zStar - prev; 
             var z2 = k[interval] - z1; 
 
 
             return ((-a[interval - 1] / 6 * (z2 + k[interval]) * z1 + xs[interval - 1]) * z2 + 
                 (-a[interval] / 6 * (z1 + k[interval]) * z2 + xs[interval]) * z1) / k[interval]; 
         } 
 
 
 
         private static void SolveTridiag(double[] sub, double[] diag, double[] sup, ref double[] b, int n) 
         { 
             int i; 
 
 
             for (i = 2; i <= n; i++) 
             { 
                 sub[i] = sub[i] / diag[i - 1]; 
                 diag[i] = diag[i] - sub[i] * sup[i - 1]; 
                 b[i] = b[i] - sub[i] * b[i - 1]; 
             } 
 
 
             b[n] = b[n] / diag[n]; 
              
             for (i = n - 1; i >= 1; i--) 
             { 
                 b[i] = (b[i] - sup[i] * b[i + 1]) / diag[i]; 
             } 
         } 
     } 



        public class CircleDef
        {
            public double xCenter {get;set;}
            public double yCenter {get;set;}
            public double radius {get;set;}
            public double zCoord{get;set;}
            public int zIndex{get;set;}
        }

        public static CircleDef convertToCircle(VVector[] points)
        {
            double area = 0.0;
            double cmX = 0.0;
            double cmY = 0.0;

            if (points.Count() == 0)
            {
                CircleDef retvalNull = new CircleDef();
                retvalNull.xCenter = cmX;
                retvalNull.yCenter = cmY;
                retvalNull.radius = 0.0;
                retvalNull.zCoord = 0.0;
                retvalNull.zIndex = 0;
                return retvalNull;
            }

            VVector origin=points[0];
            if (points.Count() > 2)
            {
                VVector firstCorner = points[1] - origin;
                VVector secondCorner = points[2] - origin;
                for (int ip = 2; ip < points.Count(); ++ip)
                {
                    var point = points[ip];
                    secondCorner = firstCorner;
                    firstCorner = point - origin;

                    double areaDelta = 0.5 * (secondCorner.x * firstCorner.y - firstCorner.x * secondCorner.y);
                    area += areaDelta;
                    cmX += (firstCorner.x + secondCorner.x) / 3.0 * areaDelta;
                    cmY += (firstCorner.y + secondCorner.y) / 3.0 * areaDelta;
                }
            }
            CircleDef retval = new CircleDef();
            double areaInv = area > 0.0 ? 1.0 / area : 0.0;
            retval.xCenter = cmX * areaInv + origin.x;
            retval.yCenter = cmY * areaInv + origin.y;
            retval.radius = Math.Floor((Math.Sqrt(area / Math.PI) + 0.05) * 10.0) * 0.1;
            retval.zCoord = origin.z;
            retval.zIndex = 0;
            return retval;
        }

        public static List<CircleDef> getAllCircles(int zCount, Structure structure)
        {
            List<CircleDef> retval = new List<CircleDef>();
            for(int z = 0; z < zCount; ++z)
            {
                var contours = structure.GetContoursOnImagePlane(z);
                if (contours.Count() != 1) continue;
                retval.Add(convertToCircle(contours.First()));
                retval.Last().zIndex = z;
            }
            
            double maxRadius = 0;
            foreach(CircleDef c in retval)
            {
                if (c.radius > maxRadius) maxRadius = c.radius;
            }
            foreach(CircleDef c in retval)
                c.radius = maxRadius;

            return retval;
        }

        public static double interpolateValue(double x0, double x1, double y0, double y1, double xStar)
        {
            return y0 + (xStar - x0) * (y1 - y0) / (x1 - x0);
        }

        public static List<CircleDef> interpolateCircleDefs(List<CircleDef> sparseCircles)
        {

            

            List<CircleDef> retval = new List<CircleDef>();
            retval.Add(sparseCircles.First());

            if (sparseCircles.Count() == 1)
                return retval;

            if (sparseCircles.Count() == 2)
            {
                foreach (CircleDef c in sparseCircles)
                {
                    if (c == sparseCircles.First())
                    {
                        continue;
                    }
                    CircleDef prev = retval.Last();
                    double zDelta = (c.zCoord - prev.zCoord) / (double)(c.zIndex - prev.zIndex);
                    for (int z = prev.zIndex + 1; z < c.zIndex; ++z)
                    {
                        CircleDef iC = new CircleDef();
                        iC.zCoord = prev.zCoord + (double)(z - prev.zIndex) * zDelta;
                        iC.xCenter = interpolateValue(prev.zCoord, c.zCoord, prev.xCenter, c.xCenter, iC.zCoord);
                        iC.yCenter = interpolateValue(prev.zCoord, c.zCoord, prev.yCenter, c.yCenter, iC.zCoord);
                        iC.zIndex = z;
                        iC.radius = c.radius;
                        retval.Add(iC);
                    }
                    retval.Add(c);
                }
                return retval;
            }

            List<double> zs = new List<double>();
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();

            foreach (CircleDef c in sparseCircles)
            {
                zs.Add(c.zCoord);
                xs.Add(c.xCenter);
                ys.Add(c.yCenter);
            }
            Spline xSpline = new Spline(zs, xs);
            Spline ySpline = new Spline(zs, ys);

            int zStart = sparseCircles.First().zIndex;
            int zEnd = sparseCircles.Last().zIndex;

            double zDeltaS = (zs.Last() - zs.First()) / (double)(zEnd - zStart);
            double radius = sparseCircles.First().radius;
            for (int z = zStart + 1; z < zEnd; ++z)
            {
                CircleDef iC = new CircleDef();
                iC.zCoord = zs.First() + zDeltaS * (double)(z - zStart);
                iC.xCenter = xSpline.Interpolate(iC.zCoord);
                iC.yCenter = ySpline.Interpolate(iC.zCoord);
                iC.zIndex = z;
                iC.radius = radius;
                retval.Add(iC);
            }
            retval.Add(sparseCircles.Last());
            return retval;
        }

        public static VVector[] getBoundingBox(int zCount, Structure structure)
        {
            VVector[] retval = new VVector[2];

            double highVal = 1.0e+10;
            retval[0].x = retval[0].y = retval[0].z = highVal;
            retval[1].x = retval[1].y = retval[1].z = -highVal;

            for (int z = 0; z < zCount; ++z)
            {
                var contours =  structure.GetContoursOnImagePlane(z);
                if (contours.Count() > 0) 
                { //layer has some contours
                    //TODO: handle cases when there are multiple contours

                    if (retval[0].z > contours.First().First().z) retval[0].z = contours.First().First().z;
                    if (retval[1].z < contours.First().First().z) retval[1].z = contours.First().First().z;
                    foreach (var contour in contours)
                        foreach (var p in contour)
                        {
                            if (retval[0].x > p.x) retval[0].x = p.x;
                            if (retval[0].y > p.y) retval[0].y = p.y;
                            
                            if (retval[1].x < p.x) retval[1].x = p.x;
                            if (retval[1].y < p.y) retval[1].y = p.y;
                           
                        }
                }
            }
            return retval;
        }

        public static bool isWithinBBWithMarginals(VVector[] bb, double marginal, Structure structure, int zcount)
        {
            VVector[] sbb = getBoundingBox(zcount, structure);
            return bb[0].x - marginal < sbb[0].x &&
                bb[0].y - marginal < sbb[0].y &&
                bb[0].z - marginal < sbb[0].z &&
                bb[1].x + marginal > sbb[1].x &&
                bb[1].y + marginal > sbb[1].y &&
                bb[1].z + marginal > sbb[1].z;
        }

        public static List<Structure> getCandidateStructures(ScriptContext context, Structure target)
        {
            List<Structure> retval = new List<Structure>();
            int zCount = context.Image.ZSize;
            VVector[] bb = getBoundingBox(zCount, target);
            foreach(Structure s in context.StructureSet.Structures)
            {
                if (s == target) continue;
                if (!isWithinBBWithMarginals(bb, 20.0, s, zCount)) continue;
                //check that we have only single contours 
                bool multiContour = false;
                for (int z = 0; z < zCount; ++z)
                {
                    var contours =  s.GetContoursOnImagePlane(z);
                    if (contours.Count() > 1) multiContour = true;
                }
                if (multiContour) continue;

                retval.Add(s);
            }

            return retval;
        }


        public static VVector[] CreateContour(VVector center, double radius, int nOfPoints)
        {
            VVector[] contour = new VVector[nOfPoints + 1];
            double angleIncrement = Math.PI * 2.0 / Convert.ToDouble(nOfPoints);
            for (int i = 0; i < nOfPoints; ++i)
            {
                double angle = Convert.ToDouble(i) * angleIncrement;
                double xDelta = radius * Math.Cos(angle);
                double yDelta = radius * Math.Sin(angle);
                VVector delta = new VVector(xDelta, yDelta, 0.0);
                contour[i] = center + delta;
            }
            contour[nOfPoints] = contour[0];

            return contour;
        }

        public static void addContoursToStructure(VVector[] gridPoints, Image image, ref Structure gridStructure, double radius, int zLayer)
        {
            double zCoord = (double)(zLayer) * image.ZRes + image.Origin.z;

            const int contourSegmentCount = 32;

            foreach (var contourCenter in gridPoints)
            {
                VVector center = new VVector(contourCenter.x, contourCenter.y, zCoord);
                var contour = CreateContour(contourCenter, radius, contourSegmentCount);
                gridStructure.AddContourOnImagePlane(contour, zLayer);
            }
        }

        public static void createGridStructure(ScriptContext context, Structure target, ref Structure gridStructure)
        {
            if (target == null) return;

            List<Structure> rodSpecs = getCandidateStructures(context, target);

            int zCount = context.Image.ZSize;
            int contourSegmentCount = 16;

            foreach(Structure s in rodSpecs)
            {
                List<CircleDef> sparseCircles = getAllCircles(zCount, s);
                List<CircleDef> allCircles = interpolateCircleDefs(sparseCircles);
                
                foreach(CircleDef c in allCircles)
                {
                    VVector center = new VVector(c.xCenter, c.yCenter, c.zCoord);
                    var contour = CreateContour(center, c.radius, contourSegmentCount);
                    gridStructure.AddContourOnImagePlane(contour, c.zIndex);
                }                                      
            }

            gridStructure.SegmentVolume = gridStructure.And(target);
        }

        public static void createGridStructureFromCandidates(ScriptContext context, List<Structure> rodSpecs, double radius, ref Structure gridStructure)
        {

            int zCount = context.Image.ZSize;
            int contourSegmentCount = 16;

            foreach(Structure s in rodSpecs)
            {
                List<CircleDef> sparseCircles = getAllCircles(zCount, s);
                if (radius > 0.0)
                    foreach (var c in sparseCircles)
                        c.radius = radius;
                List<CircleDef> allCircles = interpolateCircleDefs(sparseCircles);
                
                foreach(CircleDef c in allCircles)
                {
                    VVector center = new VVector(c.xCenter, c.yCenter, c.zCoord);
                    var contour = CreateContour(center, c.radius, contourSegmentCount);
                    gridStructure.AddContourOnImagePlane(contour, c.zIndex);
                }                                      
            }

        }

        public static Structure CreateGrid(ScriptContext context, Structure target)
        {
            //Start prepare the patient
            context.Patient.BeginModifications();
            var grid = context.StructureSet.AddStructure("PTV", "Grid");
            createGridStructure(context, target, ref grid);
            return grid;
        }

        public static void CreateGridAndInverse(ScriptContext context, Structure target)
        {
            //Start prepare the patient
            context.Patient.BeginModifications();
            var grid = CreateGrid(context, target);
            var inverse = context.StructureSet.AddStructure("PTV", "GridInv");
            inverse.SegmentVolume = target.Sub(grid);

        }

        public static string getFreeName(StructureSet mySS, string basename)
        {
            string freename = basename;
            int modifier = 0;
            bool nameNotCleared = true;
            while(nameNotCleared)
            {
                nameNotCleared = false;
                foreach(var s in mySS.Structures)
                    if (s.Id == freename)
                    {
                        nameNotCleared = true;
                        freename = basename + (++modifier).ToString();
                        break;
                    }
            }
            return freename;
            
        }

    }
}

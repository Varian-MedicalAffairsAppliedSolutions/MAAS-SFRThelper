using MAAS_SFRThelper.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


namespace MAAS_SFRThelper.ViewModels
{
    public class GridDialogViewModel : INotifyPropertyChanged
    {

        double radius;
        public double Radius
        {
            get { return radius; }
            set { radius = value; UpdateRadius(radius); OnPropertyChanged(); }
        }

        double spacingX;
        public double SpacingX
        {
            get { return spacingX; }
            set { spacingX = value; Create2Dgrid(zRes); OnPropertyChanged(); }
        }

        double spacingY;
        public double SpacingY
        {
            get { return spacingY; }
            set { spacingY = value; Create2Dgrid(zRes); OnPropertyChanged(); }
        }

        double offsetX;
        public double OffsetX
        {
            get { return offsetX; }
            set { offsetX = value; Create2Dgrid(zRes); OnPropertyChanged(); }
        }
        double offsetY;
        public double OffsetY
        {
            get { return offsetY; }
            set { offsetY = value; Create2Dgrid(zRes); OnPropertyChanged(); }
        }

        double tiltX;
        public double TiltX
        {
            get { return tiltX; }
            set { tiltX = value; Create2Dgrid(zRes); OnPropertyChanged(); }
        }
        double tiltY;
        public double TiltY
        {
            get { return tiltY; }
            set { tiltY = value; Create2Dgrid(zRes); OnPropertyChanged(); }
        }

        int zStart;
        public int ZStart
        {
            get { return zStart; }
            set { zStart = value; OnPropertyChanged(); }
        }

        int zEnd;
        public int ZEnd
        {
            get { return zEnd; }
            set { zEnd = value; OnPropertyChanged(); }
        }

        int zShown;
        public int ZShown
        {
            get { return zShown; }
            set { zShown = value; UpdateContours(zShown); Create2Dgrid(zRes); OnPropertyChanged(); }
        }

        ObservableCollection<BaseObject> drawingObjects;
        public ObservableCollection<BaseObject> DrawingObjects
        {
            get { return drawingObjects; }
            set { drawingObjects = value; OnPropertyChanged(); }
        }

        private EsapiWorker _esapiWorker;
        double canvasHeight;
        public double CanvasHeight
        {
            get { return canvasHeight; }
            set { canvasHeight = value; OnPropertyChanged(); }
        }
        double canvasWidth;
        public double CanvasWidth
        {
            get { return canvasWidth; }
            set { canvasWidth = value; OnPropertyChanged(); }
        }

        private List<string> targetStructures;
        public List<string> TargetStructures
        {
            get { return targetStructures; }
            set
            {
                targetStructures = value;
                OnPropertyChanged();
            }
        }


        private int targetSelected;
        public int TargetSelected
        {
            get { return targetSelected; }
            set
            {
                targetSelected = value;
                _esapiWorker.Run(sc =>
                {
                    UpdateFullState(sc);
                });
                OnPropertyChanged();
            }
        }

        double bbXs;
        double bbYs;
        double bbXe;
        double bbYe;

        double centerX;
        double centerY;
        private double zRes;
        private CoordinateConverter xConv;
        private CoordinateConverter yConv;

        //public ScriptContext context;

        public Structure target;

        public GridDialogViewModel(EsapiWorker esapiWorker)
        {
            _esapiWorker = esapiWorker;
            //context = currentContext;

            //ui 'consts'
            canvasHeight = 300;
            canvasWidth = 400;

            //ui defaults
            radius = 10;
            spacingX = 30;
            spacingY = 30;
            drawingObjects = new ObservableCollection<BaseObject>();
            offsetX = 0;
            offsetY = 0;

            //hidden defaults
            bbXs = 50;
            bbXe = 250;
            bbYs = 50;
            bbYe = 200;

            //plan isocenter
            _esapiWorker.Run(sc =>
            {
                var firstBeam = sc.PlanSetup.Beams.First();
                centerX = firstBeam.IsocenterPosition.x;
                centerY = firstBeam.IsocenterPosition.y;
                zRes = sc.Image.ZRes;
                //target structures
                targetStructures = new List<string>();
                targetSelected = -1;
                //plan target
                string planTargetId = null;
                //only for 15.x or later
                //List<ProtocolPhasePrescription> p = new List<ProtocolPhasePrescription>();
                //List<ProtocolPhaseMeasure> m = new List<ProtocolPhaseMeasure>();
                //context.PlanSetup.GetProtocolPrescriptionsAndMeasures(ref p, ref m);

                //if (p.Count() > 0) planTargetId = p.First().StructureId;
                //..plan target selection ends

                foreach (var i in sc.StructureSet.Structures)
                {
                    if (i.DicomType != "PTV") continue;
                    targetStructures.Add(i.Id);
                    if (planTargetId == null) continue;
                    if (i.Id == planTargetId) targetSelected = targetStructures.Count() - 1;
                }

                UpdateFullState(sc);
            });
        }

        private Circle GetOrCreateForInGrid(int xG, int yG, ObservableCollection<BaseObject> newDrawingObjects)
        {
            foreach (var newDrawingObject in newDrawingObjects)
            {
                if (newDrawingObject is Circle circle)
                {
                    if (circle.XGrid == xG && circle.YGrid == yG)
                        return circle;
                }
            }

            return new Circle
            {
                X = 0,
                Y = 0,
                R = xConv.LengthToCanvas(radius),
                Selected = true,
                XGrid = xG,
                YGrid = yG,
                TiltX = tiltX,
                TiltY = tiltY,
                ZCoordStarX = xConv.LengthToCanvas((double)(2.0 * zShown - zStart - zEnd) * 0.5 * zRes),
                ZCoordStarY = yConv.LengthToCanvas((double)(2.0 * zShown - zStart - zEnd) * 0.5 * zRes)
            };
        }

        private void Create2Dgrid(double Zres)
        {

            ObservableCollection<BaseObject> newDrawingObjects = new ObservableCollection<BaseObject>(drawingObjects); //new ObservableCollection<BaseObject>();
            DrawingObjects.Clear();
            DrawingObjects.Add(newDrawingObjects.First());
            int startX = (int)(-Math.Floor((centerX + offsetX - bbXs + radius) / (spacingX))) - 2;
            int endX = (int)(1 + Math.Floor((bbXe + Radius - centerX - offsetX) / (spacingX))) + 2;
            int startY = (int)(-Math.Floor((centerY + offsetY - bbYs + radius) / (spacingY))) - 2;
            int endY = (int)(1 + Math.Floor((bbYe + Radius - centerY - offsetY) / (spacingY))) + 2;

            for (int x = startX; x < endX; ++x)
            {
                for (int y = startY; y < endY; ++y)
                {
                    Circle curCircle = GetOrCreateForInGrid(x, y, newDrawingObjects);
                    curCircle.X = xConv.PointToCanvas(centerX + offsetX + (double)(x) * (spacingX) - radius);
                    curCircle.Y = yConv.PointToCanvas(centerY + offsetY + (double)(y) * (spacingY) - radius);
                    curCircle.TiltX = TiltX;
                    curCircle.TiltY = tiltY;
                    curCircle.ZCoordStarX = xConv.LengthToCanvas((double)(2 * zShown - zStart - zEnd) * 0.5 * Zres);
                    curCircle.ZCoordStarY = yConv.LengthToCanvas((double)(2 * zShown - zStart - zEnd) * 0.5 * Zres);

                    drawingObjects.Add(curCircle);
                }
            }

            UpdateRadius(radius);
        }

        private void UpdateRadius(double radius)
        {
            foreach (var drawingObject in drawingObjects)
            {
                if (drawingObject is Circle circle)
                {
                    double oldR = circle.R;

                    circle.R = xConv.LengthToCanvas(radius);
                    circle.X += oldR - circle.R;
                    circle.Y += oldR - circle.R;
                }
            }
        }

        private void UpdateCanvasScaling()
        {
            double fullWidth = bbXe - bbXs;
            double fullHeight = bbYe - bbYs;

            double largerScaler = fullWidth / canvasWidth > fullHeight / canvasHeight ? fullWidth / canvasWidth : fullHeight / canvasHeight;
            double widthMargin = 0.5 * (largerScaler - fullWidth / canvasWidth);
            double heightMargin = 0.5 * (largerScaler - fullHeight / canvasHeight);

            xConv = new CoordinateConverter(bbXs - widthMargin * canvasWidth, bbXe + widthMargin * canvasWidth, canvasWidth);

            yConv = new CoordinateConverter(bbYs - heightMargin * canvasHeight, bbYe + heightMargin * canvasHeight, canvasHeight);
        }

        private void SelectTarget(ScriptContext sc)
        {
            if (targetSelected == -1)
            {
                target = null;
                DrawingObjects.Clear();
                return;
            }

            string targetName = targetStructures.ElementAt(TargetSelected);

            var structures = sc.StructureSet.Structures;
            foreach (var s in structures)
            {
                if (s.Id == targetName)
                {
                    target = s;
                    return;
                }
            }

            target = null;
            DrawingObjects.Clear(); //clear objects
            return;
        }



        private void UpdateContours(int z)
        {
            var conts = target.GetContoursOnImagePlane(z);

            PointCollection points = new PointCollection();
            foreach (var contour in conts)
            {
                foreach (var p in contour)
                {
                    points.Add(new Point(xConv.PointToCanvas(p.x), yConv.PointToCanvas(p.y)));
                }
            }

            //check if we already have objects
            Polygon polygon;
            if (DrawingObjects.Any())
            {
                polygon = (Polygon)DrawingObjects.ElementAt(0);
                polygon.Points = points;
            }
            else
            {
                polygon = new Polygon { Points = points };
                DrawingObjects.Add(polygon);
            }
        }

        private void ScanTarget(ScriptContext sc)
        {
            int zCount = sc.Image.ZSize;
            int zStartTemp = zCount;
            int zEndTemp = 0;

            double highVal = 1.0e+10;
            bbXs = bbYs = highVal;
            bbXe = bbYe = -highVal;

            for (int z = 0; z < zCount; ++z)
            {
                var contours = target.GetContoursOnImagePlane(z);
                if (contours.Count() > 0)
                { //layer has some contours
                    //TODO: handle cases when there are multiple contours
                    zStartTemp = zStartTemp > z ? z : zStartTemp;
                    zEndTemp = z + 1;

                    //bb
                    foreach (var contour in contours)
                    {
                        foreach (var p in contour)
                        {
                            if (bbXs > p.x) bbXs = p.x;
                            if (bbYs > p.y) bbYs = p.y;

                            if (bbXe < p.x) bbXe = p.x;
                            if (bbYe < p.y) bbYe = p.y;
                        }
                    }
                }
            }

            UpdateCanvasScaling();

            //prepareFirst Layer
            ZStart = zStartTemp;
            ZEnd = zEndTemp;
            int zMid = (zEnd - zStart) / 2 + zStart;
            var conts = target.GetContoursOnImagePlane(zMid);
            while (conts.Count() == 0)
            {
                ++zMid;
                conts = target.GetContoursOnImagePlane(zMid);
            }
            ZShown = zMid;
            //updateContours(zMid);

            Create2Dgrid(sc.Image.ZRes);
        }

        private void UpdateFullState(ScriptContext sc)
        {
            SelectTarget(sc);
            if (target != null)
            {
                ScanTarget(sc);
                //Create2Dgrid is already called within ScanTarget
                //Create2Dgrid(sc);
            }

        }
        //creating the structure

        VVector[] CreateContour(VVector center, double radius, int nOfPoints)
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

        // CR This seems to be the method called to create the grid structure
        void CreateGridStructure(double zOrigin, ref Structure gridStructure)
        {
            if (target == null) return;

            double zCenter = (zEnd + zStart) / 2.0 * zRes + zOrigin;

            for (int z = zStart; z < zEnd; ++z)
            {
                double zCoord = z * zRes + zOrigin;
                double tiltXOffset = (zCoord - zCenter) * Math.Tan(tiltX / 180.0 * Math.PI);
                double tiltYOffset = (zCoord - zCenter) * Math.Tan(tiltY / 180.0 * Math.PI);
                const int contourSegmentCount = 32;

                foreach (var drawingObject in drawingObjects)
                {
                    if (drawingObject is Circle c)
                    {
                        if (c.Selected == false) continue;
                        VVector center = new VVector(xConv.PointFromCanvas(c.X) + radius + tiltXOffset,
                                                     yConv.PointFromCanvas(c.Y) + radius + tiltYOffset,
                                                     zCoord);
                        gridStructure.AddContourOnImagePlane(CreateContour(center, radius, contourSegmentCount), z);
                    }
                }
            }

            gridStructure.SegmentVolume = gridStructure.And(target);
        }

        //commented as not being called from 10.19.24 MCS
        //void CRTest(ref Structure gridStructure, float R)
        //{
        //    if (target == null) return;

        //    double zCenter = (double)(zEnd + zStart) / 2.0 * context.Image.ZRes + context.Image.Origin.z;
        //    for (int z = zStart; z < zEnd; ++z)
        //    {
        //        double zCoord = (double)(z) * context.Image.ZRes + context.Image.Origin.z;

        //        // For each slice find in plane radius
        //        var z_diff = Math.Abs(zCoord - zCenter);
        //        if (z_diff > R) // If we are out of range of the sphere continue
        //        {
        //            continue;
        //        }

        //        // Otherwise do the thing (make spheres)
        //        var r_z = Math.Pow(R, 2) - Math.Pow(z_diff, 2);

        //        // Just make one sphere at target center for now
        //        var contour = CreateContour(gridStructure.CenterPoint, 2, 64);
        //        gridStructure.AddContourOnImagePlane(contour, z);

        //    }

        //    gridStructure.SegmentVolume = gridStructure.SegmentVolume.And(target);
        //}

        public void CreateGrid()
        {
            // Caleb Summary
            // Add 'Grid' structure set base (based how? same struct?) on PTV
            // pass gridstructure to createGridStructure
            // This gets some vars related to rod center and position
            // For each slice between Z start and Z end
            // NOTE: for checking sphere touching try: https://gdbooks.gitbooks.io/3dcollisions/content/Chapter1/point_in_sphere.html


            //Start prepare the patient
            _esapiWorker.Run(sc =>
            {
                sc.Patient.BeginModifications();
                var grid = sc.StructureSet.AddStructure("PTV", "Grid");
                CreateGridStructure(sc.Image.Origin.z, ref grid);
            });
        }

        public void CreateGridAndInverse()
        {
            //Start prepare the patient
            _esapiWorker.Run(sc =>
            {
                sc.Patient.BeginModifications();
                var grid = sc.StructureSet.AddStructure("PTV", "Grid");
                CreateGridStructure(sc.Image.Origin.z, ref grid);
                var inverse = sc.StructureSet.AddStructure("PTV", "GridInv");
                inverse.SegmentVolume = target.Sub(grid);

            });
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}

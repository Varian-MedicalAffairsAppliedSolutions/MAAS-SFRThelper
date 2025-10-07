using MAAS_SFRThelper.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Voronoi3d;
using System.Numerics;
using System.Diagnostics.Eventing.Reader;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MAAS_SFRThelper.Services;
using System.Windows.Interop;
using System.Windows.Media.Media3D;
using System.Security.Policy;
using System.IO;

namespace MAAS_SFRThelper.ViewModels
{
    public class SphereDialogViewModel : BindableBase
    {
        #region UI Properties
        public List<string> Templates { get; set; }
        private string _selectedTemplate;

        public string SelectedTemplate
        {
            get { return _selectedTemplate; }
            set
            {
                SetProperty(ref _selectedTemplate, value);
                if (SelectedTemplate == "WashU")
                {
                    PatternEnabled = false;
                    ShiftEnabled = false;
                    ThresholdEnabled = false;
                    SingleSphereEnabled = false;
                    Radius = 7.5f; // Units = mm 
                    SpacingSelected = ValidSpacings.FirstOrDefault(s => s.Value == 30); // Selected spacing is in a list of valid spacings which we default to 30 using linq

                }
            }
        }

        private bool _patternEnabled;
        public bool PatternEnabled
        {
            get { return _patternEnabled; }
            set { SetProperty(ref _patternEnabled, value); }
        }

        private bool _thresholdenabled;

        public bool ThresholdEnabled
        {
            get { return _thresholdenabled; }
            set { SetProperty(ref _thresholdenabled, value); }
        }

        private bool _shiftEnabled;

        public bool ShiftEnabled
        {
            get { return _shiftEnabled; }
            set { SetProperty(ref _shiftEnabled, value); }
        }

        private bool _singleSphereEnabled;

        public bool SingleSphereEnabled
        {
            get { return _singleSphereEnabled; }
            set { SetProperty(ref _singleSphereEnabled, value); }
        }


        private string output;
        public string Output
        {
            get { return output; }
            set { SetProperty(ref output, value); }
        }

        private bool createSingle;
        public bool CreateSingle
        {
            get { return createSingle; }
            set { SetProperty(ref createSingle, value); }
        }

        private bool nullVoidsEnabled;
        public bool NullVoidsEnabled
        {
            get { return nullVoidsEnabled; }
            set { SetProperty(ref nullVoidsEnabled, value); }
        }

        private bool createNullsVoids;
        public bool CreateNullsVoids
        {
            get { return createNullsVoids; }
            set { SetProperty(ref createNullsVoids, value); }
        }

        private bool isHex;
        public bool IsHex
        {
            get { return isHex; }
            set
            {
                SetProperty(ref isHex, value);

            }
        }


        private bool isRect;
        public bool IsRect
        {
            get { return isRect; }
            set
            {
                SetProperty(ref isRect, value);

            }
        }

        private bool isRectAlt;
        public bool IsRectAlt
        {
            get { return isRectAlt; }
            set
            {
                SetProperty(ref isRectAlt, value);

            }
        }

        private bool isCVT3D;
        public bool IsCVT3D
        {
            get { return isCVT3D; }
            set
            {
                SetProperty(ref isCVT3D, value);
                if (IsCVT3D)
                {
                    LSFVisibility = false;

                }
                else
                {
                    LSFVisibility = true;
                    // nullVoidsEnabled = true;
                }

            }
        }
        private bool _LSFVisibility;
        public bool LSFVisibility
        {
            get { return _LSFVisibility; }
            set { SetProperty(ref _LSFVisibility, value); }
        }


        private double _LateralScalingFactor;
        public double LateralScalingFactor
        {
            get { return _LateralScalingFactor; }
            set { SetProperty(ref _LateralScalingFactor, value); }
        }

        private double xShift;
        public double XShift
        {
            get { return xShift; }
            set { SetProperty(ref xShift, value); }
        }

        private double yShift;
        public double YShift
        {
            get { return yShift; }
            set { SetProperty(ref yShift, value); }
        }

        private float radius;
        public float Radius
        {
            get { return radius; }
            set
            {
                SetProperty(ref radius, value);
                CreateLatticeCommand.RaiseCanExecuteChanged();
            }
        }

        private List<string> targetStructures;
        public List<string> TargetStructures
        {
            get { return targetStructures; }
            set { SetProperty(ref targetStructures, value); }
        }

        private string targetSelected;
        public string TargetSelected
        {
            get { return targetSelected; }
            set { SetProperty(ref targetSelected, value); }
        }

        private int vThresh;
        public int VThresh
        {
            get { return vThresh; }
            set
            {
                SetProperty(ref vThresh, value);
                if (vThresh == 100)
                {
                    PartialSphereText = "Full Spheres Only";
                }
                else
                {
                    PartialSphereText = $"Allow Partial Spheres ({vThresh}%)";
                }

            }
        }

        // private List<Spacing> validSpacings;
        public ObservableCollection<Spacing> ValidSpacings { get; set; }


        private Spacing spacingSelected;
        public Spacing SpacingSelected
        {
            get { return spacingSelected; }
            set
            {
                SetProperty(ref spacingSelected, value);
                CreateLatticeCommand.RaiseCanExecuteChanged();
            }
        }




        private string _partialSphereText;

        public string PartialSphereText
        {
            get { return _partialSphereText; }
            set { SetProperty(ref _partialSphereText, value); }
        }



        private string _latticeValidationText;

        public string LatticeValidationText
        {
            get { return _latticeValidationText; }
            set { SetProperty(ref _latticeValidationText, value); }
        }

        private bool _LVVis;

        public bool LVVis
        {
            get { return _LVVis; }
            set { SetProperty(ref _LVVis, value); }
        }
        private double _progressValue;

        public double ProgressValue
        {
            get { return _progressValue; }
            set { SetProperty(ref _progressValue, value); }
        }

        #endregion

        #region Export Properties and Container
        // Container class for export data
        public class ExportDataContainer
        {
            public List<seedPointModel> GridData { get; set; }
            public Dictionary<string, object> Statistics { get; set; }
            public List<(string Id, double Volume)> IndividualSpheres { get; set; }
            public string PatientId { get; set; }
            public string PlanId { get; set; }
            public VVector CentroidOffset { get; set; }
            public string TargetSelected { get; set; }

            public ExportDataContainer()
            {
                GridData = new List<seedPointModel>();
                Statistics = new Dictionary<string, object>();
                IndividualSpheres = new List<(string Id, double Volume)>();
                CentroidOffset = new VVector(0, 0, 0);
            }
        }

        private ExportDataContainer _exportData;
        private bool _dataReadyForExport;

        public bool DataReadyForExport
        {
            get { return _dataReadyForExport; }
            set { SetProperty(ref _dataReadyForExport, value); }
        }

        public DelegateCommand ExportDataCommand { get; set; }
        #endregion

        #region internals
        private readonly EsapiWorker _esapiWorker;
        //private ScriptContext scriptContext;
        public DelegateCommand CreateLatticeCommand { get; set; }

        #endregion

        public SphereDialogViewModel(EsapiWorker esapiWorker)
        {
            // constructor
            _esapiWorker = esapiWorker;
            double spacing = 0.0;
            _esapiWorker.RunWithWait(sc =>
            {
                //scriptContext = sc;
                spacing = sc.Image.ZRes;
            });

            Templates = new List<string> { "WashU" };
            // Set UI value defaults
            VThresh = 100;
            ValidSpacings = new ObservableCollection<Spacing>();
            IsHex = true; // default to hex
            IsRect = false;
            IsRectAlt = false;
            createSingle = true; // default to keeping individual structures
            nullVoidsEnabled = true; // default to not creating nulls and voids - if Voronio or hex are selected that need to go to false
            XShift = 0;
            YShift = 0;
            Output = "Welcome to the SFRT-Helper";
            PatternEnabled = true;
            ThresholdEnabled = true;
            ShiftEnabled = true;
            SingleSphereEnabled = true;
            LateralScalingFactor = 1.0;
            CreateLatticeCommand = new DelegateCommand(CreateLattice, CanCreateLattice);
            ExportDataCommand = new DelegateCommand(ExportData, () => DataReadyForExport);
            LSFVisibility = true;

            // Set valid spacings based on CT img z resolution
            // ValidSpacings = new List<Spacing>();
            for (int i = 1; i < 80; i++) // changed to 80 to allow larger spacings with small slice thicknesses (0.625mm slices gives up to 50mm)
            {
                ValidSpacings.Add(new Spacing(spacing * i));
            }

            // Default to first value
            SpacingSelected = ValidSpacings.FirstOrDefault();

            // Target structures
            targetStructures = new List<string>();

            //string planTargetId = null;

            SetStructures();

        }

        private void SetStructures()
        {
            _esapiWorker.Run(sc =>
            {
                //consider removing plantargetid as its not used in the following loop. (stays null)
                string planTargetId = null;
                foreach (var i in sc.StructureSet.Structures)
                {
                    if (i.DicomType.Contains("TV") && !i.IsEmpty)
                    {
                        targetStructures.Add(i.Id);
                    }

                }
                if (targetStructures.Any())
                {
                    targetSelected = targetStructures.Last();
                }
            });
        }

        private bool CanCreateLattice()
        {
            if (radius == 0)
            {
                LatticeValidationText = "Please set radius.";
                LVVis = true;
            }
            else if (radius > spacingSelected.Value / 2)
            {
                LatticeValidationText = "Radius must be less than half of the spacing value.";
                LVVis = true;
            }
            else
            {
                LVVis = false;
            }
            return radius > 0 && spacingSelected != null && radius <= spacingSelected.Value / 2;

        }

        private void AddContoursToMain(int zSize, ref Structure PrimaryStructure, ref Structure SecondaryStructure)
        {
            // Loop through each image plane
            // { foreach (var segment in contours) { lowResSSource.AddContourOnImagePlane(segment, j); } }
            for (int z = 0; z < zSize; ++z)
            {
                var contours = SecondaryStructure.GetContoursOnImagePlane(z);
                foreach (var seg in contours)
                {
                    PrimaryStructure.AddContourOnImagePlane(seg, z);
                }
            }
        }

        private static double SliceSign(VVector row, VVector col)
        {
            return Math.Sign(row.x * col.y - row.y * col.x);   // ±1
        }


        private void BuildSphere(Structure parentStruct, VVector center, float r, VMS.TPS.Common.Model.API.Image image)
        {
            double sliceSign = SliceSign(image.XDirection, image.YDirection);

            for (int z = 0; z < image.ZSize; ++z)
            {
                double zCoord = sliceSign * z * (image.ZRes) + image.Origin.z;

                // For each slice find in plane radius
                var z_diff = Math.Abs(zCoord - center.z);
                if (z_diff > r) // If we are out of range of the sphere continue
                {
                    continue;
                }

                // Otherwise do the thing (make spheres)
                var r_z = Math.Sqrt(Math.Pow(r, 2.0) - Math.Pow(z_diff, 2.0));
                var contour = CreateContour(center, r_z, 500);
                parentStruct.AddContourOnImagePlane(contour, z);
            }
        }

        private List<double> Arange(double start, double stop, double step)
        {
            //log.Debug($"Arange with start stop step = {start} {stop} {step}\n");
            var retval = new List<double>();
            var currentval = start;
            while (currentval < stop)
            {
                retval.Add(currentval);
                currentval += step;
            }
            return retval;
        }


        private List<seedPointModel> BuildGrid(double progressMax, List<double> xcoords, List<double> ycoords, List<double> zcoords,
            Structure ptvRetract, Structure ptvRetractVoid, ExportDataContainer exportData)
        {
            var retval = new List<seedPointModel>();

            // First pass: Generate all potential positions and store them temporarily
            var tempSpherePositions = new List<VVector>();
            var tempVoidPositions = new List<VVector>();

            foreach (var x in xcoords)
            {
                foreach (var y in ycoords)
                {
                    foreach (var z in zcoords)
                    {
                        var pt = new VVector(x * LateralScalingFactor, y * LateralScalingFactor, z);
                        var ptVoid = new VVector((x + spacingSelected.Value / 2.0) * LateralScalingFactor,
                                                (y + spacingSelected.Value / 2) * LateralScalingFactor,
                                                z + spacingSelected.Value / 2);

                        // Check if positions are inside respective structures
                        bool isInsideptvRetract = ptvRetract.IsPointInsideSegment(pt);
                        if (isInsideptvRetract)
                        {
                            tempSpherePositions.Add(pt);
                        }

                        bool voidInsideptvRetractVoid = ptvRetractVoid.IsPointInsideSegment(ptVoid);
                        if (voidInsideptvRetractVoid)
                        {
                            tempVoidPositions.Add(ptVoid);
                        }
                    }
                }
            }

            // Calculate centroid of the target structure (ptvRetract for spheres)
            VVector targetCentroid = CalculateVolumeCentroid(ptvRetract, null);

            // Calculate centroid of the sphere positions
            VVector latticeSphereCentroid = new VVector(0, 0, 0);
            if (tempSpherePositions.Count > 0)
            {
                double avgX = tempSpherePositions.Average(p => p.x);
                double avgY = tempSpherePositions.Average(p => p.y);
                double avgZ = tempSpherePositions.Average(p => p.z);
                latticeSphereCentroid = new VVector(avgX, avgY, avgZ);
            }

            // Calculate offset to align centroids
            VVector offset = new VVector(
                targetCentroid.x - latticeSphereCentroid.x,
                targetCentroid.y - latticeSphereCentroid.y,
                targetCentroid.z - latticeSphereCentroid.z);

            // Apply offset to all positions and add XShift/YShift if specified
            foreach (var pos in tempSpherePositions)
            {
                VVector adjustedPos = new VVector(
                    pos.x + offset.x + XShift,
                    pos.y + offset.y + YShift,
                    pos.z + offset.z);

                // Re-verify position is still inside after adjustment
                if (ptvRetract.IsPointInsideSegment(adjustedPos))
                {
                    retval.Add(new seedPointModel(adjustedPos, SeedTypeEnum.Sphere));
                }

                ProgressValue += progressMax / ((double)xcoords.Count() * (double)ycoords.Count() * (double)zcoords.Count());
            }

            foreach (var pos in tempVoidPositions)
            {
                VVector adjustedPos = new VVector(
                    pos.x + offset.x + XShift,
                    pos.y + offset.y + YShift,
                    pos.z + offset.z);

                // Re-verify position is still inside after adjustment
                if (ptvRetractVoid.IsPointInsideSegment(adjustedPos))
                {
                    retval.Add(new seedPointModel(adjustedPos, SeedTypeEnum.Void));
                }

                ProgressValue += progressMax / ((double)xcoords.Count() * (double)ycoords.Count() * (double)zcoords.Count());
            }

            // Optional: Log the offset for debugging
            Output += $"\nCentroid alignment offset (Rect): X={Math.Round(offset.x, 2)}, Y={Math.Round(offset.y, 2)}, Z={Math.Round(offset.z, 2)}";

            // Store offset for export
            exportData.CentroidOffset = offset;

            return retval;
        }
        private VVector CalculateVolumeCentroid(Structure structure, VMS.TPS.Common.Model.API.Image image)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            int pointCount = 0;

            // Get bounds for sampling
            var bounds = structure.MeshGeometry.Bounds;

            // Use a sampling resolution - finer sampling gives more accurate centroid
            // Sample at approximately 2mm intervals (adjust based on your needs)
            double sampleSpacing = 2.0;

            // Sample points throughout the bounding box
            for (double x = bounds.X; x < bounds.X + bounds.SizeX; x += sampleSpacing)
            {
                for (double y = bounds.Y; y < bounds.Y + bounds.SizeY; y += sampleSpacing)
                {
                    for (double z = bounds.Z; z < bounds.Z + bounds.SizeZ; z += sampleSpacing)
                    {
                        var point = new VVector(x, y, z);
                        if (structure.IsPointInsideSegment(point))
                        {
                            sumX += x;
                            sumY += y;
                            sumZ += z;
                            pointCount++;
                        }
                    }
                }
            }

            if (pointCount > 0)
            {
                return new VVector(sumX / pointCount, sumY / pointCount, sumZ / pointCount);
            }

            // Fallback to bounding box center if no points found (shouldn't happen)
            return new VVector(
                bounds.X + bounds.SizeX / 2,
                bounds.Y + bounds.SizeY / 2,
                bounds.Z + bounds.SizeZ / 2);
        }

        // Revised BuildAlternatingCubicGrid method with centroid alignment
        private List<seedPointModel> BuildAlternatingCubicGrid(double progressMax, double Xstart, double Xsize,
            double Ystart, double Ysize, double Zstart, double Zsize,
            Structure ptvRetract, Structure ptvRetractVoid, ExportDataContainer exportData)
        {
            var retval = new List<seedPointModel>();
            double spacing = SpacingSelected.Value;

            // Calculate grid dimensions
            int nx = (int)Math.Ceiling(Xsize / spacing) + 1;
            int ny = (int)Math.Ceiling(Ysize / spacing) + 1;
            int nz = (int)Math.Ceiling(Zsize / spacing) + 1;

            // First pass: Generate all potential positions and store them temporarily
            var tempSpherePositions = new List<VVector>();
            var tempVoidPositions = new List<VVector>();

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    for (int k = 0; k < nz; k++)
                    {
                        double x = Xstart + i * spacing;
                        double y = Ystart + j * spacing;
                        double z = Zstart + k * spacing;

                        // Apply lateral scaling
                        double scaledX = x * LateralScalingFactor;
                        double scaledY = y * LateralScalingFactor;

                        // Determine if this is a sphere or void position
                        bool isSpherePosition = (i + j + k) % 2 == 0;

                        VVector position = new VVector(scaledX, scaledY, z);

                        if (isSpherePosition)
                        {
                            // Check if sphere center is inside the retracted PTV
                            if (ptvRetract.IsPointInsideSegment(position))
                            {
                                tempSpherePositions.Add(position);
                            }
                        }
                        else
                        {
                            // Check if void center is inside the void boundary structure
                            if (ptvRetractVoid.IsPointInsideSegment(position))
                            {
                                tempVoidPositions.Add(position);
                            }
                        }
                    }
                }
            }

            // Calculate centroid of the target structure (ptvRetract for spheres)
            VVector targetCentroid = CalculateVolumeCentroid(ptvRetract, null); // Pass image if needed

            // Calculate centroid of the sphere positions
            VVector latticeSphereCentroid = new VVector(0, 0, 0);
            if (tempSpherePositions.Count > 0)
            {
                double avgX = tempSpherePositions.Average(p => p.x);
                double avgY = tempSpherePositions.Average(p => p.y);
                double avgZ = tempSpherePositions.Average(p => p.z);
                latticeSphereCentroid = new VVector(avgX, avgY, avgZ);
            }

            // Calculate offset to align centroids
            VVector offset = new VVector(
                targetCentroid.x - latticeSphereCentroid.x,
                targetCentroid.y - latticeSphereCentroid.y,
                targetCentroid.z - latticeSphereCentroid.z);

            // Apply offset to all positions and add XShift/YShift if specified
            foreach (var pos in tempSpherePositions)
            {
                VVector adjustedPos = new VVector(
                    pos.x + offset.x + XShift,
                    pos.y + offset.y + YShift,
                    pos.z + offset.z);

                // Re-verify position is still inside after adjustment
                if (ptvRetract.IsPointInsideSegment(adjustedPos))
                {
                    retval.Add(new seedPointModel(adjustedPos, SeedTypeEnum.Sphere));
                }

                ProgressValue += progressMax / (double)(nx * ny * nz);
            }

            foreach (var pos in tempVoidPositions)
            {
                VVector adjustedPos = new VVector(
                    pos.x + offset.x + XShift,
                    pos.y + offset.y + YShift,
                    pos.z + offset.z);

                // Re-verify position is still inside after adjustment
                if (ptvRetractVoid.IsPointInsideSegment(adjustedPos))
                {
                    retval.Add(new seedPointModel(adjustedPos, SeedTypeEnum.Void));
                }

                ProgressValue += progressMax / (double)(nx * ny * nz);
            }

            // Optional: Log the offset for debugging
            Output += $"\nCentroid alignment offset: X={Math.Round(offset.x, 2)}, Y={Math.Round(offset.y, 2)}, Z={Math.Round(offset.z, 2)}";

            // Store offset for export
            exportData.CentroidOffset = offset;

            return retval;
        }
        struct Vec3
        {
            public double X, Y, Z;
            public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

            // Vector addition
            public static Vec3 operator +(Vec3 a, Vec3 b)
            {
                return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            }

            // Scalar multiplication
            public static Vec3 operator *(double s, Vec3 v)
            {
                return new Vec3(s * v.X, s * v.Y, s * v.Z);
            }

            public override string ToString()
            {
                return String.Format("({0:F3}, {1:F3}, {2:F3})", X, Y, Z);
            }
        }


        private List<seedPointModel> BuildHexGrid(double progressMax, double Xstart, double Xsize, double Ystart, double Ysize,
            double Zstart, double Zsize, Structure ptvRetract, Structure ptvRetractVoid, ExportDataContainer exportData)
        {
            double A = SpacingSelected.Value * (Math.Sqrt(3) / 2.0);

            var retval = new List<seedPointModel>();

            // Initial parameters for HCP lattice
            var r = SpacingSelected.Value / 2.0;
            var ipA = 2.0 * r;
            var c_over_a = Math.Sqrt(8.0 / 3.0);
            var c = c_over_a * ipA;

            // Lattice vectors with lateral scaling
            var a1 = new Vec3(ipA * LateralScalingFactor, 0.0, 0.0);
            var a2 = new Vec3(-0.5 * ipA * LateralScalingFactor, (Math.Sqrt(3) / 2) * ipA * LateralScalingFactor, 0.0);
            var a2void = new Vec3(-0.5 * ipA * LateralScalingFactor, (Math.Sqrt(3) / 2) * ipA * LateralScalingFactor, 0.0);
            var a3 = new Vec3(0.0, 0.0, c);

            // Base motif - 2 atoms per unit cell
            var atomFrac = new List<Vec3>()
            {
                new Vec3(0.0, 0.0, 0.0),
                new Vec3(1.0/3.0, 2.0/3.0, 0.5)
            };

            // Modified octahedral void positions
            var octaFrac = new List<Vec3>()
            {
                //new Vec3(0.5, 0.5, 0.25),
                //new Vec3(0.0, 0.0, 0.75)
                new Vec3(2.0/3.0, 1.0/3.0, 0.25),
                new Vec3(2.0/3.0, 1.0/3.0, 0.75)

            };

            Func<Vec3, Vec3> frac2cart = (f) =>
            {
                return (f.X * a1) + (f.Y * a2) + (f.Z * a3);
            };

            Func<Vec3, Vec3> frac2cartVoid = (f) =>
            {
                //var basePos = (f.X * a1) + (f.Y * a2void) + (f.Z * a3);
                //return basePos + new Vec3(ipA * 0.25 * LateralScalingFactor, ipA * 0.0, 0.0);
                return (f.X * a1) + (f.Y * a2) + (f.Z * a3);  // Same as spheres, no offsets
            };

            // First pass: generate all positions
            var tempSpherePositions = new List<VVector>();
            var tempVoidPositions = new List<VVector>();

            var nx = (int)(Math.Ceiling(Xsize / (ipA * LateralScalingFactor)) + 2);
            var ny = (int)(Math.Ceiling(Ysize / ((Math.Sqrt(3) / 2) * ipA * LateralScalingFactor)) + 2);
            var nz = (int)(Math.Ceiling(Zsize / c) + 2);
            Vec3 globalOffset = new Vec3(Xstart, Ystart, Zstart);

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    for (int k = 0; k < nz; k++)
                    {
                        Vec3 cellShift = globalOffset + (i * a1) + (j * a2) + (k * a3);
                        Vec3 cellShiftVoid = globalOffset + (i * a1) + (j * a2void) + (k * a3);

                        foreach (var fA in atomFrac)
                        {
                            var atomPos = cellShift + frac2cart(fA);
                            var Pt = new VVector(atomPos.X, atomPos.Y, atomPos.Z);
                            if (ptvRetract.IsPointInsideSegment(Pt))
                            {
                                tempSpherePositions.Add(Pt);
                            }
                        }

                        foreach (var fO in octaFrac)
                        {
                            Vec3 pos = cellShiftVoid + frac2cartVoid(fO);
                            var vPt = new VVector(pos.X, pos.Y, pos.Z);
                            if (ptvRetractVoid.IsPointInsideSegment(vPt))
                            {
                                tempVoidPositions.Add(vPt);
                            }
                        }
                    }
                }
            }

            // Calculate centroid of the target structure
            VVector targetCentroid = CalculateVolumeCentroid(ptvRetract, null);

            // Calculate centroid of the sphere positions
            VVector latticeSphereCentroid = new VVector(0, 0, 0);
            if (tempSpherePositions.Count > 0)
            {
                double avgX = tempSpherePositions.Average(p => p.x);
                double avgY = tempSpherePositions.Average(p => p.y);
                double avgZ = tempSpherePositions.Average(p => p.z);
                latticeSphereCentroid = new VVector(avgX, avgY, avgZ);
            }

            // Calculate offset to align centroids
            VVector offset = new VVector(
                targetCentroid.x - latticeSphereCentroid.x,
                targetCentroid.y - latticeSphereCentroid.y,
                targetCentroid.z - latticeSphereCentroid.z);

            // Apply offset to all positions
            foreach (var pos in tempSpherePositions)
            {
                VVector adjustedPos = new VVector(
                    pos.x + offset.x + XShift,
                    pos.y + offset.y + YShift,
                    pos.z + offset.z);

                // Re-verify position is still inside after adjustment
                if (ptvRetract.IsPointInsideSegment(adjustedPos))
                {
                    retval.Add(new seedPointModel(adjustedPos, SeedTypeEnum.Sphere));
                }
            }

            foreach (var pos in tempVoidPositions)
            {
                VVector adjustedPos = new VVector(
                    pos.x + offset.x + XShift,
                    pos.y + offset.y + YShift,
                    pos.z + offset.z);

                // Re-verify position is still inside after adjustment
                if (ptvRetractVoid.IsPointInsideSegment(adjustedPos))
                {
                    retval.Add(new seedPointModel(adjustedPos, SeedTypeEnum.Void));
                }
            }

            // Optional: Log the offset for debugging
            Output += $"\nCentroid alignment offset (Hex): X={Math.Round(offset.x, 2)}, Y={Math.Round(offset.y, 2)}, Z={Math.Round(offset.z, 2)}";

            // Store offset for export
            exportData.CentroidOffset = offset;

            return retval;
        }


        // this is a presphere sanity check -- may want to add something like this to make sure number does not exceed 99?
        private bool PreSpheres()
        {
            // Check if we are ready to make spheres
            if (!IsHex && !IsRect && !IsRectAlt && !IsCVT3D)
            {
                var msg = "No pattern selected. Returning.";
                Output += "\n" + msg;
                Thread.Sleep(100);
                MessageBox.Show(msg);
                return false;
            }

            // Check vol thresh for spheres
            if (VThresh > 100 || VThresh < 0)
            {
                MessageBox.Show("Volume threshold must be between 0 and 100");
                return false;
            }

            // Check target
            if (targetSelected == null)
            {
                MessageBox.Show("Must have target selected, canceling operation.");
                return false;
            }

            if (Radius <= 0)
            {
                MessageBox.Show("Radius must be greater than zero.");
                return false;
            }

            // this checks for sphere spacing - let's make this 1.1 x to be safer otherwise spheres will touch and we don't want that
            // at some point we should also check whether spheres are larger than a value - there are a bunch of values given in the drop down value
            // may need to clean them up a little bit and only show values that make sense for the given PTV - JP

            if (SpacingSelected.Value < 1.1 * (Radius * 2))
            {
                var buttons = MessageBoxButton.OKCancel;
                var result = MessageBox.Show($"WARNING: Sphere center spacing is less than sphere diameter ({Radius * 2}) mm.\n Continue?", "", buttons);
                return result == MessageBoxResult.OK;
            }

            return true;
        }

        public void BuildSpheres(bool alignGrid)
        {

            if (!PreSpheres())
            {
                return;
            }

            // Create local export container
            var exportData = new ExportDataContainer();
            exportData.TargetSelected = targetSelected;

            //begin modifications call is already in the DelegateCommand action method.
            //scriptContext.Patient.BeginModifications();
            // Make a new structure
            // Matt email 7/15/24
            // https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/06%20Apr%202018%20Webinar/Eclipse%20Scripting%20API/Projects/CreateOptStructures/CreateOptStructures.cs

            _esapiWorker.Run(sc =>
            {
                // Capture patient info for export
                exportData.PatientId = sc.Patient.Id;
                exportData.PlanId = sc.PlanSetup.Id;

                // Retrieve the structure set from the plan
                var plan = sc.PlanSetup;
                var structureSet = plan.StructureSet;

                // Define the sphere radius for the margin
                double sphereRadius = Radius; // Change this value as needed

                // Make shrunk volume structure --  
                // Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == "PTV_High");
                // var target_named = targetStructures[targetSelected]; // this is used to create PTV retract without having to pass target_name everywhere over and over again
                // Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == target_named);
                Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == targetSelected);
                // Structure ptvRetract = structureSet.AddStructure("CONTROL", "ptvRetract");
                Structure ptvRetract = null;
                if (structureSet.Structures.Any(st => st.Id.Equals("ptvRetract", StringComparison.OrdinalIgnoreCase)))
                {
                    ptvRetract = structureSet.Structures.First(st => st.Id.Equals("ptvRetract", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    ptvRetract = structureSet.AddStructure("CONTROL", "ptvRetract");
                }
                double margin = vThresh == 100 ? -1.01 * sphereRadius : -sphereRadius * vThresh / 100.0;
                ptvRetract.SegmentVolume = ptv.Margin(margin);

                // void structure
                Structure ptvRetractVoid = null;
                if (structureSet.Structures.Any(st => st.Id.Equals("ptvRetractVoid", StringComparison.OrdinalIgnoreCase)))
                {
                    ptvRetractVoid = structureSet.Structures.First(st => st.Id.Equals("ptvRetractVoid", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    ptvRetractVoid = structureSet.AddStructure("CONTROL", "ptvRetractVoid");
                }
                double marginVoid = vThresh == 100 ? -1.35 * 2.0 * sphereRadius : -sphereRadius * 2.0 * vThresh / 100.0;
                ptvRetractVoid.SegmentVolume = ptv.LargeMargin(marginVoid);

                //double marginVoid = vThresh == 100 ? -1.01 * (spacingSelected.Value - 2 * Radius) / 2 : -(spacingSelected.Value - 2 * Radius) * vThresh / 200.0;
                //ptvRetractVoid.SegmentVolume = ptv.LargeMargin(marginVoid);

                // Total lattice structure with all spheres
                Structure structMain = null;

                var target_name = targetSelected;
                var target_initial = sc.StructureSet.Structures.Where(x => x.Id == target_name).First();
                Structure target = null;
                bool deleteAutoTarget = false;

                if (!target_initial.IsHighResolution)
                {
                    target = sc.StructureSet.AddStructure("PTV", "AutoTarget");
                    AddContoursToMain(sc.Image.ZSize, ref target, ref target_initial);
                    target.ConvertToHighResolution();
                    deleteAutoTarget = true;
                    // MessageBox.Show("Created HiRes target.");
                }
                else
                {
                    target = target_initial;
                }

                if (target == null)
                {
                    //MessageBox.Show($"Could not find target with Id: {target_name}");
                    return;
                }

                // Generate a regular grid accross the dummie bounding box 
                var bounds = target.MeshGeometry.Bounds;

                // If alignGrid calculate z to snap to
                double z0 = bounds.Z;
                double zf = bounds.Z + bounds.SizeZ;
                if (alignGrid)
                {
                    // Snap z to nearest z slice
                    // where z slices = img.origin.z + (c * zres)
                    // x, y, z --> dropdown all equal
                    // z0 --> rounded to nearest grid slice
                    var zSlices = new List<double>();
                    var plane_idx = (bounds.Z - sc.Image.Origin.z) / sc.Image.ZRes;
                    int plane_int = (int)Math.Round(plane_idx);

                    z0 = sc.Image.Origin.z + (plane_int * sc.Image.ZRes);
                    //MessageBox.Show($"Original z | Snapped z = {bounds.Z} | {Math.Round(z0, 2)}");
                    Output += $"\nOriginal z | Snapped z = {Math.Round(bounds.Z, 2)} | {Math.Round(z0, 2)}";
                    Thread.Sleep(100);
                }

                // Get points that are not in the image
                List<seedPointModel> grid = null;

                if (IsHex)
                {
                    //grid = BuildHexGrid(25.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptv, ptvRetractVoid);
                    grid = BuildHexGrid(25.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract, ptvRetractVoid, exportData);
                    structMain = CreateStructure(sc.StructureSet, "LatticeHex", false, true);
                    exportData.GridData = new List<seedPointModel>(grid);
                }
                else if (IsRect)
                {
                    var xcoords = Arange(bounds.X + XShift, bounds.X + bounds.SizeX + XShift, SpacingSelected.Value);
                    var ycoords = Arange(bounds.Y + XShift, bounds.Y + bounds.SizeY + YShift, SpacingSelected.Value);
                    var zcoords = Arange(z0, zf, SpacingSelected.Value);

                    grid = BuildGrid(25.0, xcoords, ycoords, zcoords, ptvRetract, ptvRetractVoid, exportData);
                    structMain = CreateStructure(sc.StructureSet, "LatticeRect", false, true);
                    exportData.GridData = new List<seedPointModel>(grid);
                }
                else if (IsRectAlt)
                {
                    var xcoords = Arange(bounds.X + XShift, bounds.X + bounds.SizeX + XShift, SpacingSelected.Value);
                    var ycoords = Arange(bounds.Y + XShift, bounds.Y + bounds.SizeY + YShift, SpacingSelected.Value);
                    var zcoords = Arange(z0, zf, SpacingSelected.Value);

                    //grid = BuildGrid(25.0, xcoords, ycoords, zcoords, ptvRetract, ptvRetractVoid);
                    //structMain = CreateStructure(sc.StructureSet, "LatticeRect", false, true);
                    grid = BuildAlternatingCubicGrid(25.0, bounds.X + XShift, bounds.SizeX,
                                bounds.Y + YShift, bounds.SizeY,
                                z0, bounds.SizeZ,
                                ptvRetract, ptvRetractVoid, exportData);
                    structMain = CreateStructure(sc.StructureSet, "LatticeAltCubic", false, true);
                    exportData.GridData = new List<seedPointModel>(grid);
                }
                else if (IsCVT3D)
                {
                    // Extra dialog box for calculating number of points for seed placement CVT
                    // MessageBox.Show("Calculating number of spheres needed.");
                    // Output += "\nEvaluating number of spheres, this could take several minutes ...";
                    // spacingSelected.Value = spacingSelected.Value * 2;
                    // Create a temporary export data for the BuildHexGrid call
                    var tempExportData = new ExportDataContainer();
                    var gridhex = BuildHexGrid(10.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract, ptvRetractVoid, tempExportData);

                    // make list of the points in gridhex_sph, gridhex_void
                    List<Point3D> gridhexSph = new List<Point3D>();
                    List<Point3D> gridhexVoid = new List<Point3D>();
                    Random rand = new Random();

                    foreach (VVector pos in gridhex.Where(r => r.SeedType == SeedTypeEnum.Sphere).Select(r => r.Position))
                    {
                        gridhexSph.Add(new Point3D(pos.x, pos.y, pos.z));
                        //if (rand.Next(1, 10) % 2 == 0)
                        //{
                        //    gridhexSph.Add(new Point3D(pos.x, pos.y, pos.z));
                        //}

                    }



                    // MessageBox.Show("Total seeds in gridhex", gridhex.Count.ToString());
                    Output += "\nEvaluating sphere locations using 3D CVT, this could take several minutes ...";
                    // var cvt = new CVT3D(target.MeshGeometry, new CVTSettings(gridhex.Count));
                    //var cvt = new CVT3D(ptvRetract.MeshGeometry, new CVTSettings(gridhex.Count(g => g.SeedType == SeedTypeEnum.Sphere)));
                    // var cvt = new CVT3D(ptvRetract.MeshGeometry, new CVTSettings(gridhex.Count(g => g.SeedType == SeedTypeEnum.Sphere), bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, SpacingSelected.Value, Radius));
                    var cvt = new CVT3D(ptvRetract.MeshGeometry, new CVTSettings(gridhexSph, gridhex.Count(g => g.SeedType == SeedTypeEnum.Sphere)));

                    //var cvt = new CVT3D(ptvRetract.MeshGeometry, new CVTSettings(gridhexSph, gridhexVoid, gridhex.Count(g => g.SeedType == SeedTypeEnum.Sphere)));
                    var cvtGenerators = cvt.CalculateGenerators();
                    ProgressValue += 15.0;
                    // Check to make sure each point is at least SelectedSpacing distance away from every other point. If not  
                    // remove that point from the list. We could search for another point if one gets rejected to preserve
                    // total number of points but for that we'd have to change Voronio3D. Alternatively, we could add another option
                    // in Voronoi3D to be able to use cubic or hex grids. But that would also require modification of Voronoi3D which we 
                    // will look into later. For now we just do a simple check to make sure included point is at least a minimum distance away from
                    // every other point.

                    var retval = new List<seedPointModel>();
                    int idx = -1;
                    double d = 0;
                    //check to make sure cvt spheres don't overlap
                    foreach (var i in cvtGenerators)
                    {
                        idx++;
                        var cvtpt = new VVector(i.X, i.Y, i.Z);

                        if (idx > 0)
                        {
                            int num_points = retval.Count;
                            double[] dists = Enumerable.Repeat(1.0, num_points).ToArray();
                            int j = 0;
                            // foreach (int j = 0; j < num_points; j++)
                            foreach (VVector pos in retval.Where(r => r.SeedType == SeedTypeEnum.Sphere).Select(r => r.Position))
                            {
                                double dist = Math.Sqrt(
                                    Math.Pow(cvtpt[0] - pos.x, 2) +
                                    Math.Pow(cvtpt[1] - pos.y, 2) +
                                    Math.Pow(cvtpt[2] - pos.z, 2)
                                );

                                dists[j] = dist;
                                j++;
                            }

                            if (num_points > 0)
                            {
                                d = dists.Min();
                            }

                        }
                        else
                        {
                            d = 2.10 * sphereRadius;
                            // d = SpacingSelected.Value;
                        }

                        // Uncomment below if CVT uses random sampling to avoid spheres clubbing together

                        // if (SpacingSelected.Value <= d)
                        if (2.10 * sphereRadius <= d)

                        {
                            retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Sphere));
                        }
                        //retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Sphere));

                    }

                    grid = retval; // cvtGenerators.Select(p => new VVector(p.X, p.Y, p.Z)).ToList();
                    foreach (var pos in gridhex.Where(r => r.SeedType == SeedTypeEnum.Void))
                    {
                        grid.Add(pos);

                    }
                    Output += $"Total seeds in gridCVT: {grid.Count.ToString()}";
                    structMain = CreateStructure(sc.StructureSet, "CVT3D", false, true);
                    exportData.GridData = new List<seedPointModel>(grid);
                    // CVT3D doesn't calculate centroids same way
                    exportData.CentroidOffset = new VVector(0, 0, 0);
                }

                // Store configuration and statistics for export
                exportData.Statistics["TargetStructure"] = targetSelected;
                exportData.Statistics["Pattern"] = IsHex ? "Hexagonal" : IsRect ? "SimpleCubic" : IsRectAlt ? "AlternatingCubic" : IsCVT3D ? "CVT3D" : "Unknown";
                exportData.Statistics["Radius"] = sphereRadius;
                exportData.Statistics["Spacing"] = SpacingSelected.Value;
                exportData.Statistics["XShift"] = XShift;
                exportData.Statistics["YShift"] = YShift;
                exportData.Statistics["LateralScalingFactor"] = LateralScalingFactor;
                exportData.Statistics["VolumeThreshold"] = VThresh;
                exportData.Statistics["CreateSingleStructure"] = createSingle;
                exportData.Statistics["CreateVoids"] = createNullsVoids;

                Output += $"\nPTV volume: {target.Volume.ToString()}";
                Output += $"\nTotal spheres: {grid.Count(g => g.SeedType == SeedTypeEnum.Sphere).ToString()}";
                Output += $"\nSphere radius: {sphereRadius.ToString()}";
                Output += $"\nSphere volume: {((0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius).ToString()}";
                Output += $"\nApproximate sphere volume: {((0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius * grid.Count(g => g.SeedType == SeedTypeEnum.Sphere)).ToString()}";
                Output += $"\nRatio (total sphere Volume/PTV volume): {(((100 * 0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius * grid.Count(g => g.SeedType == SeedTypeEnum.Sphere)) / (target.Volume)).ToString()} %";

                // Store statistics
                exportData.Statistics["PTVVolume"] = target.Volume;
                exportData.Statistics["TotalSpheres"] = grid.Count(g => g.SeedType == SeedTypeEnum.Sphere);
                exportData.Statistics["TotalVoids"] = grid.Count(g => g.SeedType == SeedTypeEnum.Void);
                exportData.Statistics["IndividualSphereVolume"] = (0.987053856 * 4.0 / 3.0) * Math.PI * 0.001 * sphereRadius * sphereRadius * sphereRadius;
                exportData.Statistics["TotalSphereVolume"] = ((double)exportData.Statistics["IndividualSphereVolume"]) * ((int)exportData.Statistics["TotalSpheres"]);
                exportData.Statistics["VolumeRatio"] = (((double)exportData.Statistics["TotalSphereVolume"]) / target.Volume) * 100;
                exportData.Statistics["CentroidOffsetX"] = Math.Round(exportData.CentroidOffset.x, 3);
                exportData.Statistics["CentroidOffsetY"] = Math.Round(exportData.CentroidOffset.y, 3);
                exportData.Statistics["CentroidOffsetZ"] = Math.Round(exportData.CentroidOffset.z, 3);


                // Set a message box to add display the total sphere volume and give users choice of 
                // going forward or cancelling run
                MessageBoxResult result = MessageBox.Show("Approx sphere volume ratio", (((0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius * grid.Count(g => g.SeedType == SeedTypeEnum.Sphere)) / (target.Volume)).ToString(),
                MessageBoxButton.OKCancel, MessageBoxImage.Question);

                // Check the user's response
                if (result == MessageBoxResult.Cancel)
                {
                    // User chose to cancel; close the application
                    // Environment.Exit(0);
                    Output += "\n Sphere creation has been cancelled. Please close the window!";

                    return;
                }

                // 4. Make spheres
                // This loop removes any already existing spheres prior to creating new spheres
                int sphere_count = 0;

                var prevSpheres = sc.StructureSet.Structures.Where(x => x.Id.Contains("Sphere")).ToList();
                int deleted_spheres = 0;
                foreach (var sp in prevSpheres)
                {
                    sc.StructureSet.RemoveStructure(sp);
                    deleted_spheres++;
                }
                if (deleted_spheres > 0) { MessageBox.Show($"{deleted_spheres} pre-existing spheres deleted "); }


                // Hold on to single sphere ids
                var singleIds = new List<string>();
                var singleVols = new List<double>();

                // Starting message
                Output += "\nCreating spheres, this could take several minutes ...";
                //MessageBox.Show("About to create spheres.");

                // Create all individual spheres
                double progressUpdate = createNullsVoids ? 70.0 : 80.0;
                foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Sphere).Select(g => g.Position))
                {
                    Structure currentSphere = null;

                    if (!createSingle)
                    {
                        // Create a new structure and build sphere on that
                        var singleId = $"Sphere_{sphere_count}";
                        currentSphere = CreateStructure(sc.StructureSet, singleId, false, true);

                    }
                    else
                    {
                        currentSphere = structMain;

                    }
                    BuildSphere(currentSphere, ctr, Radius, sc.Image);

                    // Crop to target
                    currentSphere.SegmentVolume = currentSphere.SegmentVolume.And(target);

                    if (!createSingle)
                    {

                        structMain.SegmentVolume = structMain.Or(currentSphere.SegmentVolume);

                        // Store individual sphere data for export
                        exportData.IndividualSpheres.Add((currentSphere.Id, currentSphere.Volume));

                    }
                    sphere_count++;

                    singleIds.Add(currentSphere.Id);
                    singleVols.Add(currentSphere.Volume);
                    ProgressValue += progressUpdate / (double)grid.Count(g => g.SeedType == SeedTypeEnum.Sphere);
                }


                // Nulls and voids using complement
                if (createNullsVoids)
                {
                    Output += "\nCreating a bounding box for void placement checks ... ";

                    var sphereBox = structMain.MeshGeometry.Bounds;
                    var voidRadius = ((float)spacingSelected.Value - 2 * Radius) / 4;

                    var coreRadius = voidRadius / 2;

                    try
                    {
                        Output += "\nCreating nulls and voids ... ";
                        string structName = "Voids";
                        var prevStruct = structureSet.Structures.FirstOrDefault(x => x.Id == structName);
                        bool isCore = false;
                        if (prevStruct != null)
                        {
                            structureSet.RemoveStructure(prevStruct);

                        }
                        var voidStructure = structureSet.AddStructure("CONTROL", structName);
                        voidStructure.ConvertToHighResolution(); // all structures are high res - if structures are made not hi-res comment this
                        Structure coreVoid = null;
                        if (voidRadius > Radius)
                        {
                            isCore = true;
                            coreVoid = structureSet.AddStructure("CONTROL", "coreVoid");
                            coreVoid.ConvertToHighResolution();
                        }


                        if (isRect)
                        {

                            int voidCount = 0;


                            foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                            {
                                if (isPointInsideBBox(sphereBox, ctr))
                                {
                                    Structure currentVoid = null;

                                    currentVoid = voidStructure;

                                    BuildSphere(currentVoid, ctr, voidRadius, sc.Image);

                                    // Crop to target
                                    currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                                    voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                                    voidCount++;

                                    if (isCore)
                                    {
                                        Structure currentCore = null;
                                        currentCore = coreVoid;
                                        BuildSphere(currentCore, ctr, coreRadius, sc.Image);
                                        currentCore.SegmentVolume = currentCore.SegmentVolume.And(target);
                                        coreVoid.SegmentVolume = coreVoid.Or(currentCore.SegmentVolume);

                                    }

                                }
                            }

                        }

                        if (isRectAlt)
                        {

                            int voidCount = 0;


                            foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                            {
                                if (isPointInsideBBox(sphereBox, ctr))
                                {
                                    Structure currentVoid = null;

                                    currentVoid = voidStructure;

                                    BuildSphere(currentVoid, ctr, voidRadius, sc.Image);

                                    // Crop to target
                                    currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                                    voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                                    voidCount++;

                                    if (isCore)
                                    {
                                        Structure currentCore = null;
                                        currentCore = coreVoid;
                                        BuildSphere(currentCore, ctr, coreRadius, sc.Image);
                                        currentCore.SegmentVolume = currentCore.SegmentVolume.And(target);
                                        coreVoid.SegmentVolume = coreVoid.Or(currentCore.SegmentVolume);

                                    }

                                }
                            }

                        }


                        if (isHex)
                        {
                            //string structName = "Voids";
                            int voidCount = 0;
                            ////var prevStruct = structureSet.Structures.FirstOrDefault(x => x.Id == structName);
                            //if (prevStruct != null)
                            //{
                            //    structureSet.RemoveStructure(prevStruct);

                            //}

                            //var voidStructure = structureSet.AddStructure("CONTROL", structName);
                            //voidStructure.ConvertToHighResolution(); // all structures are high res - if structures are made not hi-res comment this

                            foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                            {
                                if (isPointInsideBBox(sphereBox, ctr))
                                {
                                    Structure currentVoid = null;

                                    currentVoid = voidStructure;

                                    BuildSphere(currentVoid, ctr, voidRadius, sc.Image);

                                    // Crop to target
                                    currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                                    voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                                    voidCount++;


                                    if (isCore)
                                    {
                                        Structure currentCore = null;
                                        currentCore = coreVoid;
                                        BuildSphere(currentCore, ctr, coreRadius, sc.Image);
                                        currentCore.SegmentVolume = currentCore.SegmentVolume.And(target);
                                        coreVoid.SegmentVolume = coreVoid.Or(currentCore.SegmentVolume);

                                    }
                                }
                            }


                            Output += "\n Voids have been created";
                            // voidStructureL3.SegmentVolume = target.Margin(-1 * spacingSelected.Value / 2).Sub(structMain.Margin(1.2 * voidFactor));


                        }


                        if (isCVT3D)
                        {
                            //var gridhex = BuildHexGrid(10.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract, ptvRetractVoid);

                            // make list of the points in gridhex_sph, gridhex_void
                            List<Point3D> gridcvtVoid = new List<Point3D>();
                            Random rand = new Random();

                            foreach (VVector pos in grid.Where(r => r.SeedType == SeedTypeEnum.Void).Select(r => r.Position))
                            {
                                gridcvtVoid.Add(new Point3D(pos.x, pos.y, pos.z));
                                //if (rand.Next(1, 10) % 2 == 0)
                                //{
                                //    gridhexVoid.Add(new Point3D(pos.x, pos.y, pos.z));
                                //}

                            }

                            List<Point3D> gridcvtSph = new List<Point3D>();

                            foreach (VVector pos in grid.Where(r => r.SeedType == SeedTypeEnum.Sphere).Select(r => r.Position))
                            {
                                gridcvtSph.Add(new Point3D(pos.x, pos.y, pos.z));
                                //if (rand.Next(1, 10) % 2 == 0)
                                //{
                                //    gridhexVoid.Add(new Point3D(pos.x, pos.y, pos.z));
                                //}

                            }

                            // var cvt = new CVT3D(ptvRetractVoid.MeshGeometry, new CVTSettings(gridhexVoid, gridhexVoid.Count()));
                            var cvt = new CVT3D(ptvRetract.MeshGeometry, new CVTSettings(
                                                                            gridcvtSph,
                                                                            bounds.X + XShift,
                                                                            bounds.SizeX,
                                                                            bounds.Y + YShift,
                                                                            bounds.SizeY,
                                                                            z0,
                                                                            bounds.SizeZ,
                                                                            SpacingSelected.Value,
                                                                            sphereRadius,
                                                                            true, // Set this to false initially until the void calculation is fully working
                                                                            gridcvtSph.Count > 0 ? gridcvtSph.Count : 32)); var cvtGenerators = cvt.CalculateGenerators();
                            var retval = grid.Where(r => r.SeedType == SeedTypeEnum.Sphere).ToList();

                            double d = 0;
                            //check to make sure cvt spheres don't overlap
                            //foreach (var i in cvtGenerators)
                            foreach (var i in gridcvtVoid)
                            {

                                var cvtpt = new VVector(i.X, i.Y, i.Z);

                                int num_points = retval.Count();
                                double[] dists = Enumerable.Repeat(1.0, num_points).ToArray();
                                int j = 0;
                                // foreach (int j = 0; j < num_points; j++)
                                foreach (VVector pos in retval.Select(r => r.Position))
                                {
                                    double dist = Math.Sqrt(
                                        Math.Pow(cvtpt[0] - pos.x, 2) +
                                        Math.Pow(cvtpt[1] - pos.y, 2) +
                                        Math.Pow(cvtpt[2] - pos.z, 2)
                                    );

                                    dists[j] = dist;
                                    j++;
                                }

                                if (num_points > 0)
                                {
                                    d = dists.Min();
                                }

                                // Uncomment below if CVT uses random sampling to avoid spheres clubbing together

                                // if (SpacingSelected.Value <= d)
                                if (1.01 * sphereRadius <= d)
                                {
                                    bool isInsideptvRetractVoid = ptvRetractVoid.IsPointInsideSegment(cvtpt);
                                    if (isInsideptvRetractVoid)
                                    {
                                        retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Void));
                                    }
                                    //   retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Void));
                                }
                                //retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Sphere));

                            }

                            grid = retval; // cvtGenerators.Select(p => new VVector(p.X, p.Y, p.Z)).ToList();

                            //string structName = "Voids";
                            int voidCount = 0;

                            var cvtSphereBox = new Rect3D(sphereBox.X + SpacingSelected.Value / 4,
                                sphereBox.Y + SpacingSelected.Value / 4,
                                sphereBox.Z + SpacingSelected.Value / 4,
                                sphereBox.SizeX - SpacingSelected.Value / 2,
                                sphereBox.SizeY - SpacingSelected.Value / 2,
                                sphereBox.SizeZ - SpacingSelected.Value / 2);
                            foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                            {
                                if (isPointInsideBBox(cvtSphereBox, ctr))
                                {

                                    Structure currentVoid = null;

                                    currentVoid = voidStructure;

                                    BuildSphere(currentVoid, ctr, voidRadius, sc.Image);

                                    // Crop to target
                                    currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                                    voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                                    voidCount++;


                                    if (isCore)
                                    {
                                        Structure currentCore = null;
                                        currentCore = coreVoid;
                                        BuildSphere(currentCore, ctr, coreRadius, sc.Image);
                                        currentCore.SegmentVolume = currentCore.SegmentVolume.And(target);
                                        coreVoid.SegmentVolume = coreVoid.Or(currentCore.SegmentVolume);

                                    }

                                }

                            }

                            Output += "\nVoidCVT has been created";

                        }


                        ProgressValue += 100 - ProgressValue;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.InnerException.ToString());

                    }
                }

                if (!createNullsVoids)
                {
                    ProgressValue = 100.0;
                }

                Output += "\nCreated spheres. Please close the tool to view";

                // Delete the autogenerated target if it exists
                if (deleteAutoTarget)
                {
                    sc.StructureSet.RemoveStructure(target);
                    sc.StructureSet.RemoveStructure(ptvRetract);
                    sc.StructureSet.RemoveStructure(ptvRetractVoid);
                }
            });

            // After the worker completes, assign the export data
            _exportData = exportData;
            DataReadyForExport = true;
            ExportDataCommand.RaiseCanExecuteChanged();

            // And the main structure with target
            // Output += "\nCreated spheres. Please close the tool to view";
            //MessageBox.Show("Created spheres close tool to view. \nFor different sphere locations rerun with different x and y shift values.");

        }

        private bool isPointInsideBBox(Rect3D bbox, VVector point)
        {
            Point3D p = new Point3D(point.x, point.y, point.z);
            return bbox.Contains(p);
            // Check if the point's coordinates are within the bounds of the 3D rectangle
            //return (point.x >= bbox.X && point.x <= bbox.X + bbox.SizeX) &&
            //        (point.y >= bbox.Y && point.y <= bbox.Y + bbox.SizeY) &&
            //        (point.z >= bbox.Z && point.z <= bbox.Z + bbox.SizeZ);
        }


        static (double minX, double maxX, double minY, double maxY, double minZ, double maxZ) GetNormalizationParams(List<(double x, double y, double z)> points)
        {
            return (
                minX: points.Min(p => p.x),
                maxX: points.Max(p => p.x),
                minY: points.Min(p => p.y),
                maxY: points.Max(p => p.y),
                minZ: points.Min(p => p.z),
                maxZ: points.Max(p => p.z)
            );
        }

        static List<(double x, double y, double z)> NormalizePoints(List<(double x, double y, double z)> points, (double minX, double maxX, double minY, double maxY, double minZ, double maxZ) normalizationParams)
        {
            return points.Select(p => (
                x: (p.x - normalizationParams.minX) / (normalizationParams.maxX - normalizationParams.minX),
                y: (p.y - normalizationParams.minY) / (normalizationParams.maxY - normalizationParams.minY),
                z: (p.z - normalizationParams.minZ) / (normalizationParams.maxZ - normalizationParams.minZ)
            )).ToList();
        }

        static (double x, double y, double z) DenormalizePoint((double x, double y, double z) point, (double minX, double maxX, double minY, double maxY, double minZ, double maxZ) normalizationParams)
        {
            return (
                x: point.x * (normalizationParams.maxX - normalizationParams.minX) + normalizationParams.minX,
                y: point.y * (normalizationParams.maxY - normalizationParams.minY) + normalizationParams.minY,
                z: point.z * (normalizationParams.maxZ - normalizationParams.minZ) + normalizationParams.minZ
            );
        }

        static List<List<(double x, double y, double z)>> KNearestNeighbors(List<(double x, double y, double z)> points, int k)
        {
            var clusters = new List<List<(double x, double y, double z)>>();
            var visited = new HashSet<(double x, double y, double z)>();

            foreach (var point in points)
            {
                if (visited.Contains(point))
                    continue;

                // Find k-nearest neighbors for the point, excluding the point itself
                var neighbors = points
                    .Where(p => p != point && !visited.Contains(p))
                    .OrderBy(p => Distance(point, p))
                    .Take(k) // Take only k neighbors
                    .ToList();

                clusters.Add(neighbors);

                // Mark neighbors as visited
                foreach (var neighbor in neighbors)
                {
                    visited.Add(neighbor);
                }
            }

            return clusters;
        }

        static (double x, double y, double z) FindCentroid(List<(double x, double y, double z)> cluster, double perturbation)
        {
            var random = new Random();
            var centroid = (
                x: cluster.Average(p => p.x),
                y: cluster.Average(p => p.y),
                z: cluster.Average(p => p.z)
            );

            // Add a small random perturbation to the centroid
            centroid = (
                x: centroid.x + (random.NextDouble() - 0.5) * 2 * perturbation,
                y: centroid.y + (random.NextDouble() - 0.5) * 2 * perturbation,
                z: centroid.z + (random.NextDouble() - 0.5) * 2 * perturbation
            );

            return centroid;
        }

        static (double x, double y, double z) AdjustCentroid((double x, double y, double z) centroid, List<(double x, double y, double z)> otherCentroids, double minDistance)
        {
            foreach (var other in otherCentroids)
            {
                if (Distance(centroid, other) < minDistance)
                {
                    centroid = (
                        x: centroid.x + minDistance,
                        y: centroid.y + minDistance,
                        z: centroid.z + minDistance
                    );
                }
            }
            return centroid;
        }

        static double Distance((double x, double y, double z) p1, (double x, double y, double z) p2)
        {
            return Math.Sqrt(Math.Pow(p1.x - p2.x, 2) + Math.Pow(p1.y - p2.y, 2) + Math.Pow(p1.z - p2.z, 2));
        }
        // -----------------------------------

        private VVector[] CreateContour(VVector center, double radius, int nOfPoints)
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

        private Structure CreateStructure(StructureSet ss, string structName, bool showMessage, bool makeHiRes)
        {
            string msg = $"New structure ({structName}) created.";
            var prevStruct = ss.Structures.FirstOrDefault(x => x.Id == structName);
            if (prevStruct != null)
            {
                ss.RemoveStructure(prevStruct);
                msg += " Old structure overwritten.";
            }

            var structure = ss.AddStructure("PTV", structName);
            if (makeHiRes)
            {
                structure.ConvertToHighResolution();
                msg += " Converted to Hi-Res";
            }

            if (showMessage) { MessageBox.Show(msg); }
            return structure;
        }

        public void CreateLattice()
        {
            // Reset export readiness
            DataReadyForExport = false;
            ExportDataCommand.RaiseCanExecuteChanged();

            _esapiWorker.RunWithWait(sc => { sc.Patient.BeginModifications(); });
            // Make a new structure
            // Matt email 7/15/24
            // https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/06%20Apr%202018%20Webinar/Eclipse%20Scripting%20API/Projects/CreateOptStructures/CreateOptStructures.cs


            // Build spheres
            BuildSpheres(true);
        }

        #region Export Methods
        private void ExportData()
        {
            try
            {
                // Create base folder path
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string dataPath = Path.Combine(documentsPath, "Data");

                // Sanitize names for folder/file use
                string safePatientId = SanitizeFileName(_exportData.PatientId);
                string safeTargetName = SanitizeFileName(_exportData.TargetSelected);
                string safePattern = SanitizeFileName(_exportData.Statistics["Pattern"].ToString());
                string dateStr = DateTime.Now.ToString("yyyyMMdd");

                // Create patient-specific folder WITHOUT pattern name
                string patientFolder = Path.Combine(dataPath, $"{safePatientId}_{safeTargetName}_{dateStr}");
                Directory.CreateDirectory(patientFolder);

                // Generate base filename WITH pattern
                string baseFileName = $"{safePatientId}_{safeTargetName}_{safePattern}";

                // Export Configuration CSV
                ExportConfiguration(Path.Combine(patientFolder, $"{baseFileName}_Configuration_{dateStr}.csv"));

                // Export Positions CSV
                ExportPositions(Path.Combine(patientFolder, $"{baseFileName}_Positions_{dateStr}.csv"));

                // Export Statistics CSV
                ExportStatistics(Path.Combine(patientFolder, $"{baseFileName}_Statistics_{dateStr}.csv"));

                // Export Individual Spheres CSV (if applicable)
                if (_exportData.IndividualSpheres != null && _exportData.IndividualSpheres.Count > 0)
                {
                    ExportIndividualSpheres(Path.Combine(patientFolder, $"{baseFileName}_IndividualSpheres_{dateStr}.csv"));
                }

                // Show success message
                MessageBox.Show($"Data exported successfully to:\n{patientFolder}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Output += $"\n\nData exported to: {patientFolder}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Unknown";

            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid)
            {
                fileName = fileName.Replace(c.ToString(), "_");
            }
            return fileName;
        }

        private void ExportConfiguration(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Parameter,Value");
                writer.WriteLine($"TargetStructure,{_exportData.Statistics["TargetStructure"]}");
                writer.WriteLine($"Pattern,{_exportData.Statistics["Pattern"]}");
                writer.WriteLine($"Radius,{((double)_exportData.Statistics["Radius"]):F3}");
                writer.WriteLine($"Spacing,{((double)_exportData.Statistics["Spacing"]):F3}");
                writer.WriteLine($"XShift,{((double)_exportData.Statistics["XShift"]):F3}");
                writer.WriteLine($"YShift,{((double)_exportData.Statistics["YShift"]):F3}");
                writer.WriteLine($"LateralScalingFactor,{((double)_exportData.Statistics["LateralScalingFactor"]):F3}");
                writer.WriteLine($"VolumeThreshold,{_exportData.Statistics["VolumeThreshold"]}");
                writer.WriteLine($"CreateSingleStructure,{_exportData.Statistics["CreateSingleStructure"]}");
                writer.WriteLine($"CreateVoids,{_exportData.Statistics["CreateVoids"]}");
            }
        }

        private void ExportPositions(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Index,Type,X,Y,Z");
                int index = 1;
                foreach (var point in _exportData.GridData)
                {
                    writer.WriteLine($"{index},{point.SeedType},{point.Position.x:F3},{point.Position.y:F3},{point.Position.z:F3}");
                    index++;
                }
            }
        }

        private void ExportStatistics(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Metric,Value,Unit");
                writer.WriteLine($"PTVVolume,{((double)_exportData.Statistics["PTVVolume"]):F3},cc");
                writer.WriteLine($"TotalSpheres,{_exportData.Statistics["TotalSpheres"]},count");
                writer.WriteLine($"TotalVoids,{_exportData.Statistics["TotalVoids"]},count");
                writer.WriteLine($"SphereRadius,{((double)_exportData.Statistics["Radius"]):F3},mm");
                writer.WriteLine($"IndividualSphereVolume,{((double)_exportData.Statistics["IndividualSphereVolume"]):F3},cc");
                writer.WriteLine($"TotalSphereVolume,{((double)_exportData.Statistics["TotalSphereVolume"]):F3},cc");
                writer.WriteLine($"VolumeRatio,{((double)_exportData.Statistics["VolumeRatio"]):F3},%");

                if (_exportData.Statistics.ContainsKey("CentroidOffsetX"))
                {
                    writer.WriteLine($"CentroidOffsetX,{((double)_exportData.Statistics["CentroidOffsetX"]):F3},mm");
                    writer.WriteLine($"CentroidOffsetY,{((double)_exportData.Statistics["CentroidOffsetY"]):F3},mm");
                    writer.WriteLine($"CentroidOffsetZ,{((double)_exportData.Statistics["CentroidOffsetZ"]):F3},mm");
                }
            }
        }

        private void ExportIndividualSpheres(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("SphereID,Volume");
                foreach (var sphere in _exportData.IndividualSpheres)
                {
                    writer.WriteLine($"{sphere.Id},{sphere.Volume:F3}");
                }
            }
        }
        #endregion
    }
}
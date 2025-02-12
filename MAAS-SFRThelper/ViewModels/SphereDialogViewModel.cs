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
                /* if (isHex)
                 {
                     createNullsVoids = false;
                     nullVoidsEnabled = false;
                 }
                 else
                 {
                     nullVoidsEnabled = true;
                 }*/
                //// MessageBox.Show("IsHex" + IsHex);
                //if (IsHex)
                //{
                //    IsRect = false;
                //    UpdateValidSpacings();
                //}
            }
        }


        private bool isRect;
        public bool IsRect
        {
            get { return isRect; }
            set
            {
                SetProperty(ref isRect, value);
                // MessageBox.Show("IsRect" + IsRect);
                //if (IsRect){
                //    IsHex = false;
                //    UpdateValidSpacings();
                //}                
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
                    // createNullsVoids = false;
                    // nullVoidsEnabled = false;
                }
                else
                {
                    LSFVisibility = true;
                    // nullVoidsEnabled = true;
                }
                //// MessageBox.Show("IsHex" + IsHex);
                //if (IsHex)
                //{
                //    IsRect = false;
                //    UpdateValidSpacings();
                //}
            }
        }
        private bool _LSFVisibility;
        public bool LSFVisibility
        {
            get { return _LSFVisibility; }
            set { SetProperty(ref _LSFVisibility, value); }
        }

        //private Visibility _lsfVisibility; // = Visibility.Hidden;
        //public Visibility LSFVisibility
        //{
        //    get => _lsfVisibility;
        //    set
        //    {
        //        if (IsCVT3D)
        //        {
        //            _lsfVisibility = Visibility.Collapsed;

        //        }
        //        else
        //        {
        //            _lsfVisibility = Visibility.Visible;
        //            // OnPropertyChanged(nameof(LSFVisibility)); // Notify the UI

        //        }

        //    }

        //}

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

        private int targetSelected;
        public int TargetSelected
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
        //{
        //    get { return validSpacings; }
        //    set { SetProperty(ref validSpacings, value); }
        //}

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
            LSFVisibility = true;

            // Set valid spacings based on CT img z resolution
            // ValidSpacings = new List<Spacing>();
            for (int i = 1; i < 40; i++) // changed 30 to 40 to include 30 for WashU method @ 7/5 - Matt
            {
                ValidSpacings.Add(new Spacing(spacing * i));
            }

            // Default to first value
            SpacingSelected = ValidSpacings.FirstOrDefault();

            // Target structures
            targetStructures = new List<string>();
            targetSelected = -1;
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
                    if (i.DicomType != "PTV") continue;
                    targetStructures.Add(i.Id);
                    if (planTargetId == null) continue;
                    if (i.Id == planTargetId) targetSelected = targetStructures.Count() - 1;
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

        /*
        private void UpdateValidSpacings()
        {
            ValidSpacings.Clear();
            var spacing = scriptContext.Image.ZRes;
             for (int i = 1; i < 60; i++) // changed 30 to 40 to include 30 for WashU method @ 7/5 - Matt
                {
                if (IsHex)
                {
                   // ValidSpacings.Add(new Spacing(spacing * Math.Sqrt(3) * i)); // this actually does not make sense because Zres determines Zcoords, and these should not have a sqrt 3
                   ValidSpacings.Add(new Spacing(spacing * i));
                }
                if (IsRect)
                {
                    ValidSpacings.Add(new Spacing(spacing * i));

                }

            }
      
             // Default to first value
             SpacingSelected = ValidSpacings.FirstOrDefault();
        }
        */

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

        private void BuildSphere(Structure parentStruct, VVector center, float r, VMS.TPS.Common.Model.API.Image image)
        {
            for (int z = 0; z < image.ZSize; ++z)
            {
                double zCoord = z * (image.ZRes) + image.Origin.z;

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

        private List<seedPointModel> BuildGrid(double progressMax,List<double> xcoords, List<double> ycoords, List<double> zcoords, Structure ptvRetract, Structure ptvRetractVoid) // this sets up points around which spheres are built
        {
            var retval = new List<seedPointModel>();
            //double progressMax = 50.0;
            foreach (var x in xcoords)
            {
                foreach (var y in ycoords)
                {
                    foreach (var z in zcoords)
                    {
                        var pt = new VVector(x * LateralScalingFactor, y * LateralScalingFactor, z);
                        var ptVoid = new VVector((x + spacingSelected.Value / 2.0) * LateralScalingFactor, (y + spacingSelected.Value/2) * LateralScalingFactor, z + spacingSelected.Value/2);

                        // We want to elminate partial spheres - so if we put a check in here - if the point is in ptvRetract, we add it to retval
                        // if it is not inside sphere, we don't add this point to retval

                        bool isInsideptvRetract = ptvRetract.IsPointInsideSegment(pt);

                        if (isInsideptvRetract)
                        {
                            retval.Add(new seedPointModel(pt, SeedTypeEnum.Sphere));
                        }

                        bool voidInsideptvRetractVoid = ptvRetractVoid.IsPointInsideSegment(ptVoid);

                        if (voidInsideptvRetractVoid)
                        {
                            retval.Add(new seedPointModel(ptVoid, SeedTypeEnum.Void));
                        }
                        ProgressValue += progressMax / ((double)xcoords.Count() * (double)ycoords.Count() * (double)zcoords.Count());
                    }
                }
            }

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


        private List<seedPointModel> BuildHexGrid(double progressMax, double Xstart, double Xsize, double Ystart, double Ysize, double Zstart, double Zsize, Structure ptvRetract, Structure ptvRetractVoid) // this will setup coords for points on hex grid
        {
            double A = SpacingSelected.Value * (Math.Sqrt(3) / 2.0); // what is A? why is it this value?
            // https://www.omnicalculator.com/math/hexagon
            // the height of a triangle will be h = √3/2 × a

            var retval = new List<seedPointModel>();
            //void CreateLayer(double zCoord, double x0, double y0)
            //{

            //    // create planar hexagonal sphere packing grid
            //    var yeven = Arange(y0, y0 + Ysize, 2.0 * A * LateralScalingFactor); // Tenzin - make a drop down menu and rather than having a 2.0, put some variable in it
            //    // 2 is the scaling factor --- changed to 4 and tested -- Matt - 2 and 4 reduces number of spheres overall (makes sense - verified by measurements?)

            //    var xeven = Arange(x0, x0 + Xsize, LateralScalingFactor * SpacingSelected.Value);
            //    // int yRow = 0;

            //    foreach (var y in yeven)
            //    {
            //        // int xSpot = yRow%2 == 0 ? 1 : 0; // start x spot counter at 1 if y is even and start x spot counter at 0 is y is odd
            //        foreach (var x in xeven)
            //        {

            //            var pt1 = new VVector(x, y, zCoord);
            //            var pt2 = new VVector(x + (SpacingSelected.Value / 2.0) * LateralScalingFactor, y + A * LateralScalingFactor, zCoord);

            //            bool isInsideptvRetract1 = ptvRetract.IsPointInsideSegment(pt1);
            //            bool isInsideptvRetract2 = ptvRetract.IsPointInsideSegment(pt2);

            //            if (isInsideptvRetract1)
            //            {
            //                retval.Add(new seedPointModel(pt1, SeedTypeEnum.Sphere));
            //            }

            //            if (isInsideptvRetract2)
            //            {
            //                retval.Add(new seedPointModel(pt2, SeedTypeEnum.Sphere));
            //            }

            //            // Void setup
            //            // Both layers A and B find equidistant point from A and B above and below and use these points

            //            // Pt 1

            //            //var mxA = (x + (x - LateralScalingFactor * SpacingSelected.Value)) / 2.0;  // midpoint of x
            //            //var myA = (y + (y + 2.0 * A * LateralScalingFactor)) / 2.0;  // midpoint of y
            //            //var slopeA = (2.0 * A) / (SpacingSelected.Value); // slope
            //            //var pt1VoidA = new VVector(mxA + (Math.Sqrt(3) / 2.0) * 2.0 * A * LateralScalingFactor, myA - (3 / 2.0) * LateralScalingFactor * SpacingSelected.Value, zCoord + A / 3);
            //            //var pt2VoidA = new VVector(mxA - (Math.Sqrt(3) / 2.0) * 2.0 * A * LateralScalingFactor, myA + (3 / 2.0) * LateralScalingFactor * SpacingSelected.Value, zCoord - A / 3);

            //            // Pt 2
            //            //var mxB = (x + (SpacingSelected.Value / 2.0) * LateralScalingFactor + x + (SpacingSelected.Value / 2.0) * LateralScalingFactor - LateralScalingFactor * SpacingSelected.Value) / 2.0;
            //            //var myB = (y + A * LateralScalingFactor + y + A * LateralScalingFactor + 2.0 * A * LateralScalingFactor) / 2.0;
            //            //var slopeB = (2.0 * A * LateralScalingFactor) / (LateralScalingFactor * SpacingSelected.Value); // slope
            //            //var pt1VoidB = new VVector(mxB + (Math.Sqrt(3) / 2.0) * 2.0 * A * LateralScalingFactor, myB - (3 / 2.0) * LateralScalingFactor * SpacingSelected.Value, zCoord + A * Math.Sqrt(2) / 6);
            //            //var pt2VoidB = new VVector(mxB - (Math.Sqrt(3) / 2.0) * 2.0 * A * LateralScalingFactor, myB + (3 / 2.0) * LateralScalingFactor * SpacingSelected.Value, zCoord - A * Math.Sqrt(2) / 6);

            //            // var ptVoid = new VVector((x + x + (SpacingSelected.Value / 2.0))/2, ( y + y + A * LateralScalingFactor)/2, zCoord);
            //            // We want to elminate partial spheres - so if we put a check in here - if the point is in ptvRetract, we add it to retval
            //            // if it is not inside sphere, we don't add this point to retval


            //            // Check for void pts
            //            //bool isInsideptvRetractVoid1A = ptvRetractVoid.IsPointInsideSegment(pt1VoidA); // change to ptvVoid later ptvRetract for testing
            //            //bool isInsideptvRetractVoid2A = ptvRetractVoid.IsPointInsideSegment(pt2VoidA);

            //            //bool isInsideptvRetractVoid1A = ptvRetractVoid.IsPointInsideSegment(pt1VoidA); // change to ptvVoid later ptvRetract for testing
            //            //bool isInsideptvRetractVoid2A = ptvRetractVoid.IsPointInsideSegment(pt2VoidA);


            //            //if (isInsideptvRetractVoid1A)
            //            //{
            //            //    retval.Add(new seedPointModel(pt1VoidA, SeedTypeEnum.Void));
            //            //}

            //            //if (isInsideptvRetractVoid2A)
            //            //{
            //            //    retval.Add(new seedPointModel(pt2VoidA, SeedTypeEnum.Void));
            //            //}

            //            //bool isInsideptvRetractVoid1B = ptvRetractVoid.IsPointInsideSegment(pt1VoidB); // change to ptvVoid later ptvRetract for testing
            //            //bool isInsideptvRetractVoid2B = ptvRetractVoid.IsPointInsideSegment(pt2VoidB);


            //            //if (isInsideptvRetractVoid1B)
            //            //{
            //            //    retval.Add(new seedPointModel(pt1VoidB, SeedTypeEnum.Void));
            //            //}

            //            //if (isInsideptvRetractVoid2B)
            //            //{
            //            //    retval.Add(new seedPointModel(pt2VoidB, SeedTypeEnum.Void));
            //            //}


            //            // Old code
            //            // retval.Add(new VVector(x, y, zCoord));
            //            // retval.Add(new VVector(x + (SpacingSelected.Value / 2.0), y + A, zCoord ));
            //            // messy sphere change
            //            // retval.Add(new VVector(x + (SpacingSelected.Value / 2.0), y + A, zCoord + A/4)); 

            //            // xSpot++;
            //            //ProgressValue += progressMax / ((double)yeven.Count() * (double)xeven.Count());
            //        }
            //        //  yRow++;
            //    }
            //}
            //var zRange = Arange(Zstart, Zstart + Zsize, 2.0 * A);
            //foreach (var z in zRange)
            //{
            //    CreateLayer(z, Xstart, Ystart);
            //    CreateLayer(z + A, Xstart + (SpacingSelected.Value / 2.0), Ystart + (A / 2.0));
            //    ProgressValue += progressMax / (double)zRange.Count();
            //}

            // Initial parameters
            var r = SpacingSelected.Value / 2.0;  // sphere radius
            var ipA = 2.0 * r;                    // in-plane spacing
            var c_over_a = Math.Sqrt(8.0 / 3.0);    // ideal c/a ratio
            var c = c_over_a * ipA;               // out of plane spacing

            // Lattice vectors
            var a1 = new Vec3(ipA, 0.0, 0.0);
            var a2 = new Vec3(-0.5 * ipA, (Math.Sqrt(3) / 2) * ipA, 0.0);
            // Modified a2void to maintain hexagonal symmetry while being offset
            var a2void = new Vec3(-0.5 * ipA, (Math.Sqrt(3) / 2) * ipA, 0.0);
            var a3 = new Vec3(0.0, 0.0, c);

            // Base motif - 2 atoms per unit cell
            var atomFrac = new List<Vec3>()
{
    new Vec3(0.0, 0.0, 0.0),         // Atom at origin
    new Vec3(1.0/3.0, 2.0/3.0, 0.5)  // Atom in upper layer
};

            // Modified octahedral void positions
            var octaFrac = new List<Vec3>()
{
    new Vec3(0.5, 0.5, 0.25),    // Adjusted position
    new Vec3(0.0, 0.0, 0.75)     // Adjusted position
};

            Func<Vec3, Vec3> frac2cart = (f) =>
            {
                return (f.X * a1) + (f.Y * a2) + (f.Z * a3);
            };

            Func<Vec3, Vec3> frac2cartVoid = (f) =>
            {
                var basePos = (f.X * a1) + (f.Y * a2void) + (f.Z * a3);
                // Add slight offset to position voids correctly
                return basePos + new Vec3(ipA * 0.25, ipA * 0.0, 0.0);
            };

            var atoms = new List<Vec3>();
            var voidVec = new List<Vec3>();

            var nx = (int)(Math.Ceiling(Xsize / ipA) + 2);
            var ny = (int)(Math.Ceiling(Ysize / (Math.Sqrt(3) / 2) * ipA) + 2);
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
                            atoms.Add(cellShift + frac2cart(fA));
                        }

                        foreach (var fO in octaFrac)
                        {
                            Vec3 pos = cellShiftVoid + frac2cartVoid(fO);
                            voidVec.Add(pos);
                        }
                    }
                }
            }

            // Boundary checking remains the same
            foreach (var atom in atoms)
            {
                var Pt = new VVector(atom.X, atom.Y, atom.Z);
                bool isInsideptvRetract = ptvRetract.IsPointInsideSegment(Pt);
                if (isInsideptvRetract)
                {
                    retval.Add(new seedPointModel(Pt, SeedTypeEnum.Sphere));
                }
            }

            foreach (var vec in voidVec)
            {
                var vPt = new VVector(vec.X, vec.Y, vec.Z);
                bool isInsideptvRetractVoid = ptvRetractVoid.IsPointInsideSegment(vPt);
                if (isInsideptvRetractVoid)
                {
                    retval.Add(new seedPointModel(vPt, SeedTypeEnum.Void));
                }
            }

            // RETURN SEED AND VOID LOCATIONS

            return retval;
        }
        // this is a presphere sanity check -- may want to add something like this to make sure number does not exceed 99?
        private bool PreSpheres()
        {
            // Check if we are ready to make spheres
            if (!IsHex && !IsRect && !IsCVT3D)
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
            if (targetSelected == -1)
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
            //begin modifications call is already in the DelegateCommand action method.
            //scriptContext.Patient.BeginModifications();
            // Make a new structure
            // Matt email 7/15/24
            // https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/06%20Apr%202018%20Webinar/Eclipse%20Scripting%20API/Projects/CreateOptStructures/CreateOptStructures.cs

            _esapiWorker.Run(sc =>
            {
            // Retrieve the structure set from the plan
            var plan = sc.PlanSetup;
            var structureSet = plan.StructureSet;

            // Define the sphere radius for the margin
            double sphereRadius = Radius; // Change this value as needed

            // Make shrunk volume structure --  
            // Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == "PTV_High");
            // var target_named = targetStructures[targetSelected]; // this is used to create PTV retract without having to pass target_name everywhere over and over again
            // Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == target_named);
            Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == targetStructures[targetSelected]);
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
            //double marginVoid = vThresh == 100 ? -1.1 * 2.0 * sphereRadius : -sphereRadius * 2.0 * vThresh / 100.0;
            //ptvRetractVoid.SegmentVolume = ptv.Margin(marginVoid);

            double marginVoid = vThresh == 100 ? -1.01 * (spacingSelected.Value - 2 * Radius) / 2 : -(spacingSelected.Value - 2 * Radius) * vThresh / 200.0;
            ptvRetractVoid.SegmentVolume = ptv.LargeMargin(marginVoid);

            // Total lattice structure with all spheres
            Structure structMain = null;

            var target_name = targetStructures[targetSelected];
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
                    grid = BuildHexGrid(25.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract, ptvRetractVoid);
                    structMain = CreateStructure(sc.StructureSet, "LatticeHex", false, true);
            }
            else if (IsRect)
            {
                var xcoords = Arange(bounds.X + XShift, bounds.X + bounds.SizeX + XShift, SpacingSelected.Value);
                var ycoords = Arange(bounds.Y + XShift, bounds.Y + bounds.SizeY + YShift, SpacingSelected.Value);
                var zcoords = Arange(z0, zf, SpacingSelected.Value);

                grid = BuildGrid(25.0, xcoords, ycoords, zcoords, ptvRetract, ptvRetractVoid);
                structMain = CreateStructure(sc.StructureSet, "LatticeRect", false, true);
            }
            else if (IsCVT3D)
            {
                // Extra dialog box for calculating number of points for seed placement CVT
                // MessageBox.Show("Calculating number of spheres needed.");
                // Output += "\nEvaluating number of spheres, this could take several minutes ...";
                // spacingSelected.Value = spacingSelected.Value * 2;
                var gridhex = BuildHexGrid(10.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract, ptvRetractVoid);

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
            }

            Output += $"\nPTV volume: {target.Volume.ToString()}";
            Output += $"\nTotal spheres: {grid.Count(g => g.SeedType == SeedTypeEnum.Sphere).ToString()}";
            Output += $"\nSphere radius: {sphereRadius.ToString()}";
            Output += $"\nSphere volume: {((0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius).ToString()}";
            Output += $"\nApproximate sphere volume: {((0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius * grid.Count(g => g.SeedType == SeedTypeEnum.Sphere)).ToString()}";
            Output += $"\nRatio (total sphere Volume/PTV volume): {(((100 * 0.987053856 * 4 / 3) * Math.PI * 0.1 * 0.1 * 0.1 * sphereRadius * sphereRadius * sphereRadius * grid.Count(g => g.SeedType == SeedTypeEnum.Sphere)) / (target.Volume)).ToString()} %";


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

                }
                sphere_count++;

                singleIds.Add(currentSphere.Id);
                singleVols.Add(currentSphere.Volume);
                ProgressValue += progressUpdate / (double)grid.Count(g => g.SeedType == SeedTypeEnum.Sphere);
            }


            // Nulls and voids using complement
            if (createNullsVoids)
            {
                try
                {
                    Output += "\nCreating nulls and voids ... ";

                    if (isRect)
                    {
                        string structName = "Voids";
                        int voidCount = 0;
                        var prevStruct = structureSet.Structures.FirstOrDefault(x => x.Id == structName);
                        if (prevStruct != null)
                        {
                            structureSet.RemoveStructure(prevStruct);

                        }

                        var voidStructure = structureSet.AddStructure("CONTROL", structName);
                        voidStructure.ConvertToHighResolution(); // all structures are high res - if structures are made not hi-res comment this

                        foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                        {
                            Structure currentVoid = null;

                            currentVoid = voidStructure;

                            BuildSphere(currentVoid, ctr, Radius / 2, sc.Image);

                            // Crop to target
                            currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                            voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                            voidCount++;

                        }

                    }

                    if (isHex)
                    {
                        string structName = "Voids";
                        int voidCount = 0;
                        var prevStruct = structureSet.Structures.FirstOrDefault(x => x.Id == structName);
                        if (prevStruct != null)
                        {
                            structureSet.RemoveStructure(prevStruct);

                        }

                        var voidStructure = structureSet.AddStructure("CONTROL", structName);
                        voidStructure.ConvertToHighResolution(); // all structures are high res - if structures are made not hi-res comment this

                        foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                        {
                            Structure currentVoid = null;

                            currentVoid = voidStructure;

                            BuildSphere(currentVoid, ctr, ((float)spacingSelected.Value - 2 * Radius) / 4, sc.Image);

                            // Crop to target
                            currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                            voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                            voidCount++;

                        }

                        //var voidFactor = (spacingSelected.Value - 2.0 * radius) / 2.0;
                        //Output += "\nCreating nulls and voids ... ";
                        //Output += $"\nVoidFactor = {voidFactor}";

                        //var voidStructureL1 = sc.StructureSet.AddStructure("CONTROL", "VoidL1");
                        //voidStructureL1.SegmentVolume = structMain.LargeMargin(voidFactor).And(target.LargeMargin(-1 * sphereRadius/2));
                        //voidStructureL1.SegmentVolume = target.LargeMargin(-1 * sphereRadius/2).Sub(voidStructureL1.SegmentVolume);
                        //Output += "\nL1 has been created";

                        //var voidStructureL2 = sc.StructureSet.AddStructure("CONTROL", "VoidL2");
                        //voidStructureL2.Color = System.Windows.Media.Color.FromRgb(160, 32, 240);
                        //voidStructureL2.SegmentVolume = target.LargeMargin(-1.75 * sphereRadius).And(structMain.LargeMargin(voidFactor));
                        //// Output += $"\nVoid Stucture L2 AND volume = {voidStructureL2.Volume}";
                        //voidStructureL2.SegmentVolume = target.LargeMargin(-1.75 * sphereRadius).Sub(voidStructureL2.SegmentVolume);
                        //Output += "\nL2 has been created";

                        //var voidStructureL3 = sc.StructureSet.AddStructure("CONTROL", "VoidL3");
                        //voidStructureL3.Color = System.Windows.Media.Color.FromRgb(0, 255, 255);
                        //voidStructureL3.SegmentVolume = voidStructureL2.LargeMargin(-sphereRadius / 4);

                        Output += "\n Voids have been created";
                        // voidStructureL3.SegmentVolume = target.Margin(-1 * spacingSelected.Value / 2).Sub(structMain.Margin(1.2 * voidFactor));


                    }

                    //if (isCVT3D)
                    //{

                    //    var cvtSeeds = new List<(double x, double y, double z)>();
                    //    foreach (VVector pos in grid.Where(r => r.SeedType == SeedTypeEnum.Sphere).Select(r => r.Position))
                    //    {
                    //        cvtSeeds.Add((pos.x, pos.y, pos.z));
                    //    }
                    //    Output += $"Total seeds in in cvtSeeds: {cvtSeeds.Count().ToString()}";
                    //    //int k = (int) Math.Floor(cvtSeeds.Count()/2.0); // Number of clusters
                    //    //List<(double x, double y, double z)> centroids = KMeans(cvtSeeds, k);

                    //    var normalizationParams = GetNormalizationParams(cvtSeeds);
                    //    cvtSeeds = NormalizePoints(cvtSeeds, normalizationParams);
                    //    int k = 3; // Number of neighbors

                    //    double minDistance = spacingSelected.Value; // Minimum distance between centroids

                    //    var clusters = KNearestNeighbors(cvtSeeds, k);
                    //    var centroids = new List<(double x, double y, double z)>();
                    //    foreach (var cluster in clusters)
                    //    {
                    //        var centroid = FindCentroid(cluster, 0.1); // Add random perturbation
                    //        centroid = AdjustCentroid(centroid, centroids, minDistance); // Ensure separation                                
                    //        var denormalizedCentroid = DenormalizePoint(centroid, normalizationParams);
                    //        centroids.Add(denormalizedCentroid);
                    //    }
                    //    //foreach (var centroid in centroids)
                    //    //{
                    //    //    Console.WriteLine($"({centroid.x}, {centroid.y}, {centroid.z})");
                    //    //}


                    //    foreach (var centroid in centroids)
                    //    {
                    //        double cx = centroid.x;
                    //        double cy = centroid.y;
                    //        double cz = centroid.z;
                    //        var voidpt = new VVector(cx, cy, cz);
                    //        grid.Add(new seedPointModel(voidpt, SeedTypeEnum.Void));
                    //    }
                    //    Output += $"Total voids in gridCVT: {grid.Where(r => r.SeedType == SeedTypeEnum.Void).Count().ToString()}";


                    //    string structName = "Voids";
                    //    int voidCount = 0;
                    //    var prevStruct = structureSet.Structures.FirstOrDefault(x => x.Id == structName);
                    //    if (prevStruct != null)
                    //    {
                    //        structureSet.RemoveStructure(prevStruct);

                    //    }

                    //    var voidStructure = structureSet.AddStructure("CONTROL", structName);
                    //    voidStructure.ConvertToHighResolution(); // all structures are high res - if structures are made not hi-res comment this

                    //    foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))

                    //    {
                    //        Structure currentVoid = null;

                    //        currentVoid = voidStructure;

                    //        BuildSphere(currentVoid, ctr, ((float)spacingSelected.Value - 2 * Radius) / 4, sc.Image);

                    //        // Crop to target
                    //        currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                    //        voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                    //        voidCount++;

                    //    }
                    //    Output += "\nVoidCVT has been created";

                    //}


                    if (isCVT3D)
                    {
                        //var gridhex = BuildHexGrid(10.0, bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract, ptvRetractVoid);

                        // make list of the points in gridhex_sph, gridhex_void
                        List<Point3D> gridhexVoid = new List<Point3D>();
                        Random rand = new Random();

                        foreach (VVector pos in grid.Where(r => r.SeedType == SeedTypeEnum.Void).Select(r => r.Position))
                        {
                             gridhexVoid.Add(new Point3D(pos.x, pos.y, pos.z));
                            //if (rand.Next(1, 10) % 2 == 0)
                            //{
                            //    gridhexVoid.Add(new Point3D(pos.x, pos.y, pos.z));
                            //}

                        }
                        //var cvt = new CVT3D(ptvRetractVoid.MeshGeometry, new CVTSettings(gridhexVoid, gridhexVoid.Count()));
                        //var cvtGenerators = cvt.CalculateGenerators();
                        var retval = grid.Where(r=>r.SeedType==SeedTypeEnum.Sphere).ToList();
                        
                        double d = 0;
                            //check to make sure cvt spheres don't overlap
                            //foreach (var i in cvtGenerators)
                            foreach (var i in gridhexVoid)
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
                            if (1.2*sphereRadius <= d)

                            {
                                retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Void));
                            }
                            //retval.Add(new seedPointModel(cvtpt, SeedTypeEnum.Sphere));

                        }

                        grid = retval; // cvtGenerators.Select(p => new VVector(p.X, p.Y, p.Z)).ToList();

                        string structName = "Voids";
                        int voidCount = 0;
                        var prevStruct = structureSet.Structures.FirstOrDefault(x => x.Id == structName);
                        if (prevStruct != null)
                        {
                            structureSet.RemoveStructure(prevStruct);

                        }

                        var voidStructure = structureSet.AddStructure("CONTROL", structName);
                        voidStructure.ConvertToHighResolution(); // all structures are high res - if structures are made not hi-res comment this

                        foreach (VVector ctr in grid.Where(g => g.SeedType == SeedTypeEnum.Void).Select(g => g.Position))
                        {
                            Structure currentVoid = null;

                            currentVoid = voidStructure;

                            var voidRadius = ((float)spacingSelected.Value - 2 * Radius) / 4;

                            if (voidRadius >= (float)Radius)
                            {
                                voidRadius = (float)Radius;

                            }

                            BuildSphere(currentVoid, ctr, voidRadius, sc.Image);

                            // Crop to target
                            currentVoid.SegmentVolume = currentVoid.SegmentVolume.And(target);

                            voidStructure.SegmentVolume = voidStructure.Or(currentVoid.SegmentVolume);
                            voidCount++;

                        }
                        //var voidFactor = (spacingSelected.Value - 2.0 * radius) / 2.0;
                        //var voidStructureCVT = sc.StructureSet.AddStructure("CONTROL", "VoidCVT");
                        //voidStructureCVT.Color = System.Windows.Media.Color.FromRgb(0, 255, 255);
                        //voidStructureCVT.SegmentVolume = (target.LargeMargin(-1.75 * sphereRadius).And(structMain.LargeMargin(voidFactor)));
                        //voidStructureCVT.SegmentVolume = voidStructureCVT.LargeMargin(-sphereRadius/4);
                        Output += "\nVoidCVT has been created";
                        // voidStructureL3.SegmentVolume = target.Margin(-1 * spacingSelected.Value / 2).Sub(structMain.Margin(1.2 * voidFactor));

                    }

                        ProgressValue += 100 - ProgressValue;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.InnerException.ToString());

                    }
                }
                //{
                //    var voidFactor = (spacingSelected.Value - 2.0 * radius) / 2.0;
                //    Output += "\nCreating nulls and voids ... ";
                //    Output += $"\nVoidFactor = {voidFactor}";

                //    var voidStructureL1 = sc.StructureSet.AddStructure("CONTROL", "VoidL1");
                //    voidStructureL1.SegmentVolume = structMain.LargeMargin(0.8 * voidFactor).And(target.LargeMargin(-1 * sphereRadius / 2));
                //    voidStructureL1.SegmentVolume = target.LargeMargin(-1 * sphereRadius / 2).Sub(voidStructureL1.SegmentVolume);
                //    Output += "\nL1 has been created";

                //    var voidStructureL2 = sc.StructureSet.AddStructure("CONTROL", "VoidL2");
                //    voidStructureL2.Color = System.Windows.Media.Color.FromRgb(160, 32, 240);
                //    voidStructureL2.SegmentVolume = target.LargeMargin(-1.75 * sphereRadius).And(structMain.LargeMargin(voidFactor));
                //    // Output += $"\nVoid Stucture L2 AND volume = {voidStructureL2.Volume}";
                //    voidStructureL2.SegmentVolume = target.LargeMargin(-1.75 * sphereRadius).Sub(voidStructureL2.SegmentVolume);
                //    Output += "\nL2 has been created";

                //    var voidStructureL3 = sc.StructureSet.AddStructure("CONTROL", "VoidL3");
                //    voidStructureL3.Color = System.Windows.Media.Color.FromRgb(0, 255, 255);
                //    voidStructureL3.SegmentVolume = voidStructureL2.LargeMargin(-sphereRadius / 4);
                //    Output += "\nL3 has been created";
                //    // voidStructureL3.SegmentVolume = target.Margin(-1 * spacingSelected.Value / 2).Sub(structMain.Margin(1.2 * voidFactor));

                //    ProgressValue += 5.0;
                //}
                Output += "\nCreated spheres. Please close the tool to view";

                // var volThresh = singleVols.Max() * (VThresh / 100);



                //foreach (string id_ in singleIds.Distinct()) // distinct does 
                //{
                //    // delete small spheres
                //    var singleSphere = scriptContext.StructureSet.Structures.Where(x => x.Id == id_).FirstOrDefault();
                //    if (singleSphere != null)
                //    {
                //        if (singleSphere.Volume <= volThresh || singleSphere.Volume == 0)
                //        {
                //            // Delete
                //            //MessageBox.Show($"Deleted sphere based on volume threshold: {singleSphere.Volume} >= {volThresh}");
                //            scriptContext.StructureSet.RemoveStructure(singleSphere);
                //            continue;
                //        }
                //    }

                //    // If here sphere is big enough
                //    // Set the lattice struct segment = lattice struct segment.
                //    // TODO: try other boolean ops to create mainStructure

                //    //structMain.SegmentVolume = structMain.SegmentVolume.Or(singleSphere); // OLD method
                //    AddContoursToMain(ref structMain, ref singleSphere);

                //    // If delete individual delete 
                //    if (!createSingle)
                //    {
                //        scriptContext.StructureSet.RemoveStructure(singleSphere);
                //    }


                //}

                // Delete the autogenerated target if it exists
                if (deleteAutoTarget)
                {
                    sc.StructureSet.RemoveStructure(target);
                    sc.StructureSet.RemoveStructure(ptvRetract);
                    sc.StructureSet.RemoveStructure(ptvRetractVoid);
                }
            });
            // And the main structure with target
            // Output += "\nCreated spheres. Please close the tool to view";
            //MessageBox.Show("Created spheres close tool to view. \nFor different sphere locations rerun with different x and y shift values.");

        }

        //static List<(double x, double y, double z)> KMeans(List<(double x, double y, double z)> points, int k)
        //{
        //    Random random = new Random();

        //    // Initialize centroids randomly from the points
        //    List<(double x, double y, double z)> centroids = points.OrderBy(_ => random.Next()).Take(k).ToList();
        //    Console.WriteLine("Initial centroids:");
        //    centroids.ForEach(c => Console.WriteLine($"({c.x}, {c.y}, {c.z})"));

        //    List<(double x, double y, double z)> previousCentroids;

        //    while (true)
        //    {
        //        // Assign points to the nearest centroid
        //        var clusters = points.GroupBy(p =>
        //            centroids.OrderBy(c => Distance(p, c)).First()).ToList();

        //        Console.WriteLine("Clusters:");
        //        foreach (var cluster in clusters)
        //        {
        //            Console.WriteLine($"Cluster around centroid ({cluster.Key.x}, {cluster.Key.y}, {cluster.Key.z}):");
        //            foreach (var point in cluster)
        //            {
        //                Console.WriteLine($"  Point: ({point.x}, {point.y}, {point.z})");
        //            }
        //        }

        //        // Update centroids
        //        previousCentroids = new List<(double x, double y, double z)>(centroids);

        //        centroids = clusters.Select(cluster =>
        //        {
        //            if (cluster.Any())
        //            {
        //                return (
        //                    x: cluster.Average(p => p.x),
        //                    y: cluster.Average(p => p.y),
        //                    z: cluster.Average(p => p.z)
        //                );
        //            }
        //            else
        //            {
        //                // If a cluster is empty, keep the previous centroid
        //                int index = clusters.IndexOf(cluster);
        //                return previousCentroids[index];
        //            }
        //        }).ToList();

        //        Console.WriteLine("Updated centroids:");
        //        centroids.ForEach(c => Console.WriteLine($"({c.x}, {c.y}, {c.z})"));

        //        // Check for convergence (if centroids do not change)
        //        if (!centroids.Where((c, i) => c != previousCentroids[i]).Any())
        //            break;
        //    }

        //    return centroids;
        //}

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
            _esapiWorker.RunWithWait(sc => { sc.Patient.BeginModifications(); });
            // Make a new structure
            // Matt email 7/15/24
            // https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/06%20Apr%202018%20Webinar/Eclipse%20Scripting%20API/Projects/CreateOptStructures/CreateOptStructures.cs


            // Retrieve the structure set from the plan
            // var plan = scriptContext.PlanSetup;
            // var structureSet = plan.StructureSet;

            // Define the sphere radius for the margin
            // double sphereRadius = Radius; // Change this value as needed

            // Make shrunk volume structure
            // Structure ptv = structureSet.Structures.FirstOrDefault(x => x.Id == "PTV_High");
            // Structure ptvRetract = structureSet.AddStructure("PTV", "ptvRetract");
            // ptvRetract.SegmentVolume = ptv.Margin(-1.25*sphereRadius);

            // Build spheres
            BuildSpheres(true);
        }
    }
}

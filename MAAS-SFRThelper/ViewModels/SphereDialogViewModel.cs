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
using Voronoi;


namespace MAAS_SFRThelper.ViewModels
{
    public class SphereDialogViewModel : BindableBase
    {
        public List <string> Templates { get; set; }
        private string _selectedTemplate;

        public string SelectedTemplate
        {
            get { return _selectedTemplate; }
            set { SetProperty(ref _selectedTemplate, value);
                if (SelectedTemplate == "WashU")
                {
                    PatternEnabled = false;
                    ShiftEnabled = false;
                    ThresholdEnabled = false;
                    SingleSphereEnabled = false;
                    Radius = 7.5f; // Units = mm 
                    SpacingSelected = ValidSpacings.FirstOrDefault(s=>s.Value==30); // Selected spacing is in a list of valid spacings which we default to 30 using linq
                    
                }
            }
        }

        private bool _patternEnabled;
        private double _LateralScalingFactor;

        public double LateralScalingFactor
        {
            get { return _LateralScalingFactor; }
            set { SetProperty(ref _LateralScalingFactor, value); }
        }

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

        private bool isHex;
        public bool IsHex
        {
            get { return isHex; }
            set { 
                SetProperty(ref isHex, value);
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
            set { 
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
                //// MessageBox.Show("IsHex" + IsHex);
                //if (IsHex)
                //{
                //    IsRect = false;
                //    UpdateValidSpacings();
                //}
            }
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
            set { SetProperty(ref radius, value);
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
            set { SetProperty(ref spacingSelected, value);
                CreateLatticeCommand.RaiseCanExecuteChanged();
            }
        }

        private readonly ScriptContext scriptContext;

        private string _partialSphereText;

        public string PartialSphereText
        {
            get { return _partialSphereText; }
            set { SetProperty(ref _partialSphereText, value); }
        }

        public DelegateCommand CreateLatticeCommand { get; set; }

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


        public SphereDialogViewModel(ScriptContext context)
        {
            // constructor
            scriptContext = context;
            Templates = new List<string> { "WashU" };
            // Set UI value defaults
            VThresh = 100;
            ValidSpacings = new ObservableCollection<Spacing>();
            IsHex = true; // default to hex
            IsRect = false;
            createSingle = true; // default to keeping individual structures
            XShift = 0;
            YShift = 0;
            Output = "Welcome to the SFRT-Helper";
            PatternEnabled = true;
            ThresholdEnabled = true;
            ShiftEnabled = true;
            SingleSphereEnabled = true;
            LateralScalingFactor = 1.0;
            CreateLatticeCommand = new DelegateCommand(CreateLattice, CanCreateLattice);

            // Set valid spacings based on CT img z resolution
            // ValidSpacings = new List<Spacing>();
            var spacing = scriptContext.Image.ZRes;
            for (int i = 1; i < 40; i++) // changed 30 to 40 to include 30 for WashU method @ 7/5 - Matt
            {
                ValidSpacings.Add(new Spacing(spacing * i));
            }
            
            // Default to first value
            SpacingSelected = ValidSpacings.FirstOrDefault();

            // Target structures
            targetStructures = new List<string>();
            targetSelected = -1;
            string planTargetId = null;

            foreach (var i in context.StructureSet.Structures)
            {
                if (i.DicomType != "PTV") continue;
                targetStructures.Add(i.Id);
                if (planTargetId == null) continue;
                if (i.Id == planTargetId) targetSelected = targetStructures.Count() - 1;
            }


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
            else { 
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

        private void AddContoursToMain(ref Structure PrimaryStructure, ref Structure SecondaryStructure)
        {
            // Loop through each image plane
            // { foreach (var segment in contours) { lowResSSource.AddContourOnImagePlane(segment, j); } }
            for (int z = 0; z < scriptContext.Image.ZSize; ++z)
            {
                var contours = SecondaryStructure.GetContoursOnImagePlane(z);
                foreach (var seg in contours)
                {
                    PrimaryStructure.AddContourOnImagePlane(seg, z);
                }
            }
        }

        private void BuildSphere(Structure parentStruct, VVector center, float r)
        {
            for (int z = 0; z < scriptContext.Image.ZSize; ++z)
            {
                double zCoord = z * (scriptContext.Image.ZRes) + scriptContext.Image.Origin.z;

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

        private List<VVector> BuildGrid(List<double> xcoords, List<double> ycoords, List<double> zcoords, Structure ptvRetract) // this sets up points around which spheres are built
        {
            var retval = new List<VVector>();
            foreach (var x in xcoords)
            {
                foreach (var y in ycoords)
                {
                    foreach (var z in zcoords)
                    {
                        var pt = new VVector(x * LateralScalingFactor, y * LateralScalingFactor, z);

                        // We want to elminate partial spheres - so if we put a check in here - if the point is in ptvRetract, we add it to retval
                        // if it is not inside sphere, we don't add this point to retval

                        bool isInsideptvRetract = ptvRetract.IsPointInsideSegment(pt);

                        if (isInsideptvRetract)
                        { 
                            retval.Add(pt);
                        }
                        
                    }
                }
            }

            return retval;
        }

        private List<VVector> BuildHexGrid(double Xstart, double Xsize, double Ystart, double Ysize, double Zstart, double Zsize, Structure ptvRetract) // this will setup coords for points on hex grid
        {
            double A = SpacingSelected.Value * (Math.Sqrt(3) / 2.0); // what is A? why is it this value?
            // https://www.omnicalculator.com/math/hexagon
            // the height of a triangle will be h = √3/2 × a

            var retval = new List<VVector>();

            void CreateLayer(double zCoord, double x0, double y0)
            {
                
                // create planar hexagonal sphere packing grid
                var yeven = Arange(y0, y0 + Ysize, 2.0 * A * LateralScalingFactor); // Tenzin - make a drop down menu and rather than having a 2.0, put some variable in it
                // 2 is the scaling factor --- changed to 4 and tested -- Matt - 2 and 4 reduces number of spheres overall (makes sense - verified by measurements?)

                var xeven = Arange(x0, x0 + Xsize, LateralScalingFactor * SpacingSelected.Value);
                // int yRow = 0;

                foreach (var y in yeven)
                {
                    // int xSpot = yRow%2 == 0 ? 1 : 0; // start x spot counter at 1 if y is even and start x spot counter at 0 is y is odd
                    foreach (var x in xeven)
                    {

                        var pt1 = new VVector(x, y, zCoord);
                        var pt2 = new VVector(x + (SpacingSelected.Value / 2.0) * LateralScalingFactor, y + A * LateralScalingFactor, zCoord);

                        // We want to elminate partial spheres - so if we put a check in here - if the point is in ptvRetract, we add it to retval
                        // if it is not inside sphere, we don't add this point to retval

                        bool isInsideptvRetract1 = ptvRetract.IsPointInsideSegment(pt1);
                        bool isInsideptvRetract2 = ptvRetract.IsPointInsideSegment(pt2);

                        if (isInsideptvRetract1)
                        {
                            retval.Add(pt1);
                        }

                        if (isInsideptvRetract2)
                        {
                            retval.Add(pt2);
                        }

                        // Old code
                        // retval.Add(new VVector(x, y, zCoord));
                        // retval.Add(new VVector(x + (SpacingSelected.Value / 2.0), y + A, zCoord ));
                        // messy sphere change
                        // retval.Add(new VVector(x + (SpacingSelected.Value / 2.0), y + A, zCoord + A/4)); 

                        // xSpot++;
                    }
                    //  yRow++;
                }
            }

            foreach (var z in Arange(Zstart, Zstart + Zsize, 2.0 * A))
            {
                CreateLayer(z, Xstart, Ystart);
                CreateLayer(z + A, Xstart + (SpacingSelected.Value / 2.0), Ystart + (A / 2.0));

            }

            return retval;
        }
        // this is a presphere sanity check -- may want to add something like this to make sure number does not exceed 99?
        private bool PreSpheres()  
        {
            // Check if we are ready to make spheres
            if (!IsHex && !IsRect)
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

            if (SpacingSelected.Value < 1.1*(Radius * 2))
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

            scriptContext.Patient.BeginModifications();
            // Make a new structure
            // Matt email 7/15/24
            // https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/06%20Apr%202018%20Webinar/Eclipse%20Scripting%20API/Projects/CreateOptStructures/CreateOptStructures.cs


            // Retrieve the structure set from the plan
            var plan = scriptContext.PlanSetup;
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

            // Total lattice structure with all spheres
            Structure structMain = null;

            var target_name = targetStructures[targetSelected];
            var target_initial = scriptContext.StructureSet.Structures.Where(x => x.Id == target_name).First();
            Structure target = null;
            bool deleteAutoTarget = false;

            if (!target_initial.IsHighResolution)
            {
                target = scriptContext.StructureSet.AddStructure("PTV", "AutoTarget");
                AddContoursToMain(ref target, ref target_initial);
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
                var plane_idx = (bounds.Z - scriptContext.Image.Origin.z) / scriptContext.Image.ZRes;
                int plane_int = (int)Math.Round(plane_idx);

                z0 = scriptContext.Image.Origin.z + (plane_int * scriptContext.Image.ZRes);
                //MessageBox.Show($"Original z | Snapped z = {bounds.Z} | {Math.Round(z0, 2)}");
                Output += $"\nOriginal z | Snapped z = {Math.Round(bounds.Z, 2)} | {Math.Round(z0, 2)}";
                Thread.Sleep(100);
            }

            // Get points that are not in the image
            List<VVector> grid = null;

            if (IsHex)
            {
                grid = BuildHexGrid(bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ, ptvRetract);
                structMain = CreateStructure("LatticeHex", false, true);
            }
            else if (IsRect)
            {
                var xcoords = Arange(bounds.X + XShift, bounds.X + bounds.SizeX + XShift, SpacingSelected.Value);
                var ycoords = Arange(bounds.Y + XShift, bounds.Y + bounds.SizeY + YShift, SpacingSelected.Value);
                var zcoords = Arange(z0, zf, SpacingSelected.Value);

                grid = BuildGrid(xcoords, ycoords, zcoords, ptvRetract);
                structMain = CreateStructure("LatticeRect", false, true);
            } 
            else if (IsCVT3D)
            {
                var cvt = new CVT3D(target, CVTSettings.DefaultSettings());
                var cvtGenerators = cvt.CalculateGenerators();
                grid = cvtGenerators.Select(x => new VVector(x[0], x[1], x[2])).ToList();
                structMain = CreateStructure("CVT3D", false, true);
            }

            // 4. Make spheres
            // This loop removes any already existing spheres prior to creating new spheres
            int sphere_count = 0;

            var prevSpheres = scriptContext.StructureSet.Structures.Where(x => x.Id.Contains("Sphere")).ToList();
            int deleted_spheres = 0;
            foreach (var sp in prevSpheres)
            {
                scriptContext.StructureSet.RemoveStructure(sp);
                deleted_spheres++;
            }
            if (deleted_spheres > 0) { MessageBox.Show($"{deleted_spheres} pre-existing spheres deleted "); }


            // Hold on to single sphere ids
            var singleIds = new List<string>();
            var singleVols = new List<double>();


            // Starting message
            Output += "\nCreating spheres, this could take several minutes ...";
            MessageBox.Show("About to create spheres.");

            // Create all individual spheres
            foreach (VVector ctr in grid)
            {
                Structure currentSphere = null;

                if (!createSingle)
                {
                    // Create a new structure and build sphere on that
                    var singleId = $"Sphere_{sphere_count}";
                    currentSphere = CreateStructure(singleId, false, true);

                }
                else
                {
                    currentSphere = structMain;

                }
                BuildSphere(currentSphere, ctr, Radius);

                // Crop to target
                currentSphere.SegmentVolume = currentSphere.SegmentVolume.And(target);
                
                if (!createSingle) {

                    structMain.SegmentVolume = structMain.Or(currentSphere.SegmentVolume);

                }
                sphere_count++;

                singleIds.Add(currentSphere.Id);
                singleVols.Add(currentSphere.Volume);

            }

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
                scriptContext.StructureSet.RemoveStructure(target);
                scriptContext.StructureSet.RemoveStructure(ptvRetract);
            }

            // And the main structure with target
            Output += "\nCreated spheres";
            MessageBox.Show("Created spheres close tool to view. Rerun with different x and y shift values to change location of spheres.");

        }

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

        private Structure CreateStructure(string structName, bool showMessage, bool makeHiRes)
        {
            string msg = $"New structure ({structName}) created.";
            var prevStruct = scriptContext.StructureSet.Structures.FirstOrDefault(x => x.Id == structName);
            if (prevStruct != null)
            {
                scriptContext.StructureSet.RemoveStructure(prevStruct);
                msg += " Old structure overwritten.";
            }

            var structure = scriptContext.StructureSet.AddStructure("PTV", structName);
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
            scriptContext.Patient.BeginModifications();
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

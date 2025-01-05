using System.Collections.Generic;
using System.Windows.Media.Media3D;
using Voronoi3d.RandomEngines;
using Voronoi3d.StoppingCriteria;

namespace Voronoi3d
{
    public class CVTSettings
    {
        public int NumberOfGenerators { get; protected set; }
        public int NumberOfSamplingPoints { get; protected set; }
        public RandomEngine SelectedSamplingMethod { get; protected set; }
        public int MaxNumberOfIterations { get; protected set; }
        public IStoppingCriterion StoppingCriterion { get; protected set; }
        public List<Point3D> SphereLocations { get; private set; }
        public List<Point3D> VoidLocations { get; private set; }
        public double StartX { get; private set; }
        public double SizeX { get; private set; }
        public double StartY { get; private set; }
        public double SizeY { get; private set; }
        public double StartZ { get; private set; }
        public double SizeZ { get; private set; }
        public double Spacing { get; private set; }
        public double SphereRadius { get; private set; }

        public CVTSettings()
        {
            InitializeDefaults();
        }

        public CVTSettings(int n_generators)
        {
            InitializeDefaults(n_generators);
        }

        private void InitializeDefaults(int n_generators = 32)
        {

            NumberOfGenerators = n_generators;
            NumberOfSamplingPoints = 3000;
            SelectedSamplingMethod = RandomEngine.HEXGRID;
            MaxNumberOfIterations = 1500;
            StoppingCriterion = new CounterDecorator(new NoDiffAfterThreeIterationsStoppingCriterion());
        }

        public CVTSettings(List<Point3D> gridhexSph, List<Point3D> gridhexVoid, int n_generators)
        {
            InitializeDefaults(gridhexSph, gridhexVoid, n_generators);
        }

        private void InitializeDefaults(List<Point3D> gridhexSph, List<Point3D> gridhexVoid, int n_generators = 32)
        {

            NumberOfGenerators = n_generators;
            NumberOfSamplingPoints = 3000;
            SelectedSamplingMethod = RandomEngine.HEXGRID;
            MaxNumberOfIterations = 1500;
            StoppingCriterion = new CounterDecorator(new NoDiffAfterThreeIterationsStoppingCriterion());
            SphereLocations = gridhexSph;
            VoidLocations = gridhexVoid;
        }

        public CVTSettings(double Xstart, double Xsize, double Ystart, double Ysize, double Zstart, double Zsize, double spacing, double rad, int n_generators)
        {
            InitializeDefaults(Xstart, Xsize, Ystart, Ysize, Zstart, Zsize, spacing, rad, n_generators);
        }

        private void InitializeDefaults(double Xstart, double Xsize, double Ystart, double Ysize, double Zstart, double Zsize, double spacing, double rad, int n_generators = 32)
        {

            NumberOfGenerators = n_generators;
            NumberOfSamplingPoints = 3000;
            SelectedSamplingMethod = RandomEngine.HEXGRID;
            MaxNumberOfIterations = 1500;
            StoppingCriterion = new CounterDecorator(new NoDiffAfterThreeIterationsStoppingCriterion());
            StartX = Xstart;
            SizeX = Xsize;
            StartY = Ystart;
            SizeY = Ysize;
            StartZ = Zstart;
            SizeZ = Zsize;
            Spacing = spacing;
            SphereRadius = rad;
        }

        public static CVTSettings DefaultSettings()
        {
            return new CVTSettings();
        }
    }
}
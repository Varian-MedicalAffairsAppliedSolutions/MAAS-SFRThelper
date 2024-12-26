using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media.Media3D;

//using VMS.TPS.Common.Model.Types;
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
        //public List<Point3D> StartingGridSph { get; private set; }
        //public List<Point3D> StartingGridVoid { get; private set; }

        //public static List<VVector> gridhexSph;
        //publlic static List<VVector> gridhexSph;
        //public static List<VVector> gridhexVoid;

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
            SelectedSamplingMethod = RandomEngine.HALTONSEQUENCE;
            MaxNumberOfIterations = 1500;
            StoppingCriterion = new CounterDecorator(new NoDiffAfterThreeIterationsStoppingCriterion());
        }

        public static CVTSettings DefaultSettings()
        {
            return new CVTSettings();
        }


        // edit this to add hex grid as default points - want to pass it from SFRT to voronoi and then maybe add pts on surface
        // inside hex grid which then gets passed to cvt3d for Voronoi

        //public CVTSettings()
        //{
        //    InitializeDefaults(0, new List<Point3D>(), new List<Point3D>());
        //}

        //public CVTSettings(int n_generators, List<Point3D> gridhexSph, List<Point3D> gridhexVoid)
        //{
        //        InitializeDefaults(n_generators, gridhexSph, gridhexVoid);

        //}

        //public void InitializeDefaults(int n_generators, List<Point3D> gridhexSph, List<Point3D> gridhexVoid)
        //{

        //    NumberOfGenerators = n_generators;
        //    NumberOfSamplingPoints = 3000;
        //    SelectedSamplingMethod = RandomEngine.HEXGRID;
        //    MaxNumberOfIterations = 1500;
        //    StoppingCriterion = new CounterDecorator(new NoDiffAfterThreeIterationsStoppingCriterion());
        //    StartingGridSph = gridhexSph;
        //    StartingGridVoid = gridhexVoid;
        //}

        //public static CVTSettings DefaultSettings()
        //{
        //    return new CVTSettings();
        //}
    }
}
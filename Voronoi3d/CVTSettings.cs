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
    }
}
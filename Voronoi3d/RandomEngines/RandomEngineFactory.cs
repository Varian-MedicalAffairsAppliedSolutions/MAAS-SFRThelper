
using System;

namespace Voronoi3d.RandomEngines
{
    public class RandomEngineFactory
    {
        public static IRandom3D Create(RandomEngine randomEngine)
        {
            switch (randomEngine)
            {
                case RandomEngine.HALTONSEQUENCE:
                    return new HaltonSequence3D();
                case RandomEngine.UNIFORMDISTRIBUTION:
                    throw new NotImplementedException("RandomEngine.UNIFORMDISTRIBUTION not implemented, yet.");
                default:
                    throw new ArgumentException("You should never received this exception (RandomEngineFactory)");
            }
        }
    }
}

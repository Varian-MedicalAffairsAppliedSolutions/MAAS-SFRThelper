using System.Collections.Generic;

namespace Voronoi.StoppingCriteria
{
    public interface IStoppingCriterion
    {
        bool CanStop(List<CVT3D.Point3D> previousPoints, List<CVT3D.Point3D> currentPoints);
    }
}
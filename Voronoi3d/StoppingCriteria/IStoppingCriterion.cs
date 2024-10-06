using System.Collections.Generic;

namespace Voronoi3d.StoppingCriteria
{
    public interface IStoppingCriterion
    {
        bool CanStop(List<CVT3D.Point3D> previousPoints, List<CVT3D.Point3D> currentPoints);
    }
}
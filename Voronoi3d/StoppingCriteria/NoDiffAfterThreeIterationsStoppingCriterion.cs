using System.Collections.Generic;

namespace Voronoi3d.StoppingCriteria
{
    public class NoDiffAfterThreeIterationsStoppingCriterion : IStoppingCriterion
    {
        int _unchangedIterations = 0;

        public bool CanStop(List<CVT3D.Point3D> previousPoints, List<CVT3D.Point3D> currentPoints)
        {
            if (AreListsEqual(previousPoints, currentPoints))
            {
                _unchangedIterations++;
            }
            else
            {
                _unchangedIterations = 0;
                return false;
            }

            return _unchangedIterations >= 3;
        }

        private bool AreListsEqual(List<CVT3D.Point3D> list1, List<CVT3D.Point3D> list2)
        {
            if (list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (!list1[i].Equals(list2[i])) return false;
            }

            return true;
        }

    }
}
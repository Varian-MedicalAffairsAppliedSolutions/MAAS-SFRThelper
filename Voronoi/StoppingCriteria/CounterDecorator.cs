using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Voronoi.StoppingCriteria
{
    public class CounterDecorator : IStoppingCriterion
    {
        private long _counter = 0;

        private IStoppingCriterion _stoppingCriterionImplementation;

        public CounterDecorator(IStoppingCriterion stoppingCriterionImplementation)
        {
            _stoppingCriterionImplementation = stoppingCriterionImplementation ??
                                               throw new ArgumentNullException(nameof(stoppingCriterionImplementation));
        }

        public bool CanStop(List<CVT3D.Point3D> previousPoints, List<CVT3D.Point3D> currentPoints)
        {
            _counter++;
            var canStop =  _stoppingCriterionImplementation.CanStop(previousPoints, currentPoints);
            if (canStop == true)
            {
                Debug.WriteLine($"Process stopped after {_counter} iterations.");
            }

            return canStop;
        }
    }
}
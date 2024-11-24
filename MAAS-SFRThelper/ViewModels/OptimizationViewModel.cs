using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAAS_SFRThelper.ViewModels
{
    public class OptimizationViewModel
    {
        private EsapiWorker _esapiworker;

        public OptimizationViewModel(EsapiWorker esapi)
        {
            _esapiworker = esapi;
        }
    }
}

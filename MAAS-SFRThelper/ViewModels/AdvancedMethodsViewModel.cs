using MAAS_SFRThelper.Services;
using Prism.Mvvm;

namespace MAAS_SFRThelper.ViewModels
{
    /// <summary>
    /// Container ViewModel for the Research tab. Owns one child ViewModel
    /// per research page. New methods (e.g. joint optimization) are added
    /// as further child ViewModels + nested tab pages, not by growing
    /// any single page.
    /// </summary>
    public class AdvancedMethodsViewModel : BindableBase
    {
        public InfluenceMatrixViewModel InfluenceMatrixViewModel { get; }

        public AdvancedMethodsViewModel(EsapiWorker esapi)
        {
            InfluenceMatrixViewModel = new InfluenceMatrixViewModel(esapi);
        }
    }
}
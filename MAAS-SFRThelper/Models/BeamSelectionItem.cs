using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Mvvm;

namespace MAAS_SFRThelper.Models
{
    public class BeamSelectionItem : BindableBase
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        public string BeamId { get; set; }
        public string Description { get; set; }
        public string Technique { get; set; }
        public bool IsVMAT { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.ComponentModel;
using System.Xml;

using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Collections.ObjectModel;

namespace GridBlockJoiner
{

    public class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    };

    public class MaskCandidate : Notifier
    {
        public Structure structure { get; set; }
        public String Id { get; set; }
        bool included;
        public bool Included
        {
            get { return included; }
            set { included = value; OnPropertyChanged("Included"); }
        }

        public MaskCandidate(Structure newStructure)
        {
            structure = newStructure;
            Id = structure.Id; // +(structure.IsHighResolution ? " (h)" : " (l)");
            included = true;
        }
    };
     
    public class TargetSelectorVM : Notifier
    {


        string targetHelper;
        public string TargetHelper
        {
            get { return targetHelper; }
            set { targetHelper = value; OnPropertyChanged("TargetHelper"); }
        }

        ObservableCollection<MaskCandidate> rodMasks;

        public ObservableCollection<MaskCandidate> RodMasks
        {
            get { return rodMasks; }
            set { rodMasks = value; OnPropertyChanged("RodMasks"); }
        }

        private List<string> targetStructures;
        public List<string> TargetStructures
        {
            get { return targetStructures; }
            set
            {
                targetStructures = value;
                OnPropertyChanged("TargetStructures");
            }
        }

        private int targetSelected;
        public int TargetSelected
        {
            get { return targetSelected; }
            set
            {
                targetSelected = value;
                updateRodMasks();
                OnPropertyChanged("TargetSelected");
            }
        }

        public Structure getTarget()
        {
            Structure target = null;

            string targetName = targetStructures.ElementAt(TargetSelected);
            var structures = context.StructureSet.Structures;

            foreach (var s in structures)
            {
                if (s.Id == targetName)
                {
                    target = s;

                }
            }

            return target;
        }

        private double radius;
        public double Radius
        {
            get { return radius; }
            set
            {
                radius = value;
                OnPropertyChanged("Radius");
            }
        }

        public void updateRodMasks()
        {

            Structure target = getTarget();

            
            if (target != null)
            {
                List<Structure> candidateMasks = ContourTools.getCandidateStructures(context, target);
                ObservableCollection<MaskCandidate> mk = new ObservableCollection<MaskCandidate>();
                foreach(Structure s in candidateMasks)
                    mk.Add(new MaskCandidate(s));
                //TargetHelper = "n of cand " + candidateMasks.Count().ToString();
                RodMasks = mk;

            }
            else
            {
                //TargetHelper = "target NULL";
            }

            return;
        }


        ScriptContext context;

        public TargetSelectorVM(ScriptContext newContext)
        {
            context = newContext;

            radius = 3.0;
            //update list of potential targets
            //target structures
            targetStructures = new List<string>();
            targetSelected = -1;
            //plan target
            string planTargetId = null;
            //for 15.x or later
            //if (context.PlanSetup != null)
            //{
            //    List<ProtocolPhasePrescription> p = new List<ProtocolPhasePrescription>();
            //    List<ProtocolPhaseMeasure> m = new List<ProtocolPhaseMeasure>();
            //    context.PlanSetup.GetProtocolPrescriptionsAndMeasures(ref p, ref m);
            //    if (p.Count() > 0) planTargetId = p.First().StructureId;
            //}


            foreach (var i in context.StructureSet.Structures)
            {
                if (i.DicomType != "PTV" || i.IsHighResolution == false) continue;
                targetStructures.Add(i.Id);
                if (planTargetId == null) continue;
                if (i.Id == planTargetId) targetSelected = targetStructures.Count() - 1;
                
            }

            rodMasks = new ObservableCollection<MaskCandidate>();

            targetHelper = "Targets of ss \"" + context.StructureSet.Id + "\""; 

        }

        public void CreateGrid()
        {

            context.Patient.BeginModifications();
            string gridName = ContourTools.getFreeName(context.StructureSet, "Grid");
            var grid = context.StructureSet.AddStructure("PTV", gridName);

            List<Structure> selectedRods = new List<Structure>();
            Structure hires = null;
            foreach (var m in rodMasks)
                if (m.Included)
                {
                    selectedRods.Add(m.structure);
                    if (m.structure.IsHighResolution) hires = m.structure;
                }

            //if (hires != null){
                grid.SegmentVolume = getTarget().Sub(getTarget());
            //}
            ContourTools.createGridStructureFromCandidates( context, selectedRods, radius, ref  grid);
            grid.SegmentVolume = grid.And(getTarget());
        }
            
            
    }

}

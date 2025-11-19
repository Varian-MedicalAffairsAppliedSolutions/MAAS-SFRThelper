using System;
using System.Collections.Generic;
using System.ComponentModel;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media;

namespace MAAS_SFRThelper.Models
{
    /// <summary>
    /// Represents a single optimization objective definition that can be Peak, Valley, or OAR
    /// </summary>
    public class ObjectiveDefinition : INotifyPropertyChanged
    {
        private bool _isIncluded;
        private string _structureName;
        private string _objectiveType;
        private double _dose;
        private double _volume;
        private int _priority;
        private OptimizationObjectiveOperator _operator;

        /// <summary>
        /// Whether this objective should be included in optimization
        /// </summary>
        public bool IsIncluded
        {
            get { return _isIncluded; }
            set
            {
                if (_isIncluded != value)
                {
                    _isIncluded = value;
                    OnPropertyChanged(nameof(IsIncluded));
                }
            }
        }

        /// <summary>
        /// Structure name (e.g., "Rectum", "LatticeAltCubic", "Valley")
        /// </summary>
        public string StructureName
        {
            get { return _structureName; }
            set
            {
                if (_structureName != value)
                {
                    _structureName = value;
                    OnPropertyChanged(nameof(StructureName));
                }
            }
        }

        /// <summary>
        /// Type of objective: "Point" or "Mean"
        /// </summary>
        public string ObjectiveType
        {
            get { return _objectiveType; }
            set
            {
                if (_objectiveType != value)
                {
                    _objectiveType = value;
                    OnPropertyChanged(nameof(ObjectiveType));
                    OnPropertyChanged(nameof(IsVolumeEnabled));
                    OnPropertyChanged(nameof(IsOperatorEnabled));
                }
            }
        }

        /// <summary>
        /// Dose value in Gy
        /// </summary>
        public double Dose
        {
            get { return _dose; }
            set
            {
                if (_dose != value)
                {
                    _dose = value;
                    OnPropertyChanged(nameof(Dose));
                }
            }
        }

        /// <summary>
        /// Volume percentage (0-100)
        /// </summary>
        public double Volume
        {
            get { return _volume; }
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    OnPropertyChanged(nameof(Volume));
                }
            }
        }

        /// <summary>
        /// Priority (0-1000+)
        /// </summary>
        public int Priority
        {
            get { return _priority; }
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged(nameof(Priority));
                }
            }
        }

        /// <summary>
        /// Upper or Lower constraint
        /// </summary>
        public OptimizationObjectiveOperator Operator
        {
            get { return _operator; }
            set
            {
                if (_operator != value)
                {
                    _operator = value;
                    OnPropertyChanged(nameof(Operator));
                }
            }
        }

        /// <summary>
        /// Role: Peak, Valley, or OAR
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Background color based on role
        /// </summary>
        public Brush BackgroundColor
        {
            get
            {
                switch (Role)
                {
                    case "Peak":
                        return new SolidColorBrush(Colors.LightYellow);
                    case "Valley":
                        return new SolidColorBrush(Colors.LightCyan);
                    default:
                        return new SolidColorBrush(Colors.White);
                }
            }
        }

        /// <summary>
        /// Whether volume field should be enabled (disabled for Mean objectives)
        /// </summary>
        public bool IsVolumeEnabled
        {
            get { return ObjectiveType == "Point"; }
        }

        /// <summary>
        /// Whether operator field should be enabled (disabled for Mean objectives)
        /// </summary>
        public bool IsOperatorEnabled
        {
            get { return ObjectiveType == "Point"; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ObjectiveDefinition()
        {
            IsIncluded = true;
            ObjectiveType = "Point";
            Operator = OptimizationObjectiveOperator.Upper;
            Role = "OAR";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Models
{
    public class seedPointModel
    {
        public VVector Position { get; set; }
        public SeedTypeEnum SeedType { get; set; }
        public seedPointModel(VVector position, SeedTypeEnum stype)
        {
            Position = position;
            SeedType = stype;
        }
    }

    public enum SeedTypeEnum
    {
        Sphere,
        Void 
    }
}

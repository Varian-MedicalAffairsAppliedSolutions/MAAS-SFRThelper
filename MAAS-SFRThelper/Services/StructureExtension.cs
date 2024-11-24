using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace MAAS_SFRThelper.Services
{
    public static class StructureExtension
    {
        /// <summary>
        /// Because Structure.Margin() has upper limit of 50mm for the margin, this
        /// extension allows larger values.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="ss"></param>
        /// <param name="mm"></param>
        /// <returns></returns>
        public static SegmentVolume LargeMargin(this Structure target, double mm)
        {
            if (mm < 0.0)
            {
                double mmLeft = mm;
                SegmentVolume targetLeft = target.SegmentVolume;
                while (mmLeft < -50)
                {
                    mmLeft += 50;
                    targetLeft = targetLeft.Margin(-50);
                }
                SegmentVolume result = targetLeft.Margin(mmLeft);

                return result;
            }
            else
            {
                double mmLeft = mm;
                SegmentVolume targetLeft = target.SegmentVolume;
                while (mmLeft > 50)
                {
                    mmLeft -= 50;
                    targetLeft = targetLeft.Margin(50);
                }
                SegmentVolume result = targetLeft.Margin(mmLeft);

                return result;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CalculateInfluenceMatrix
{
    public class DosePoint
    {
        public DosePoint() { }
        public DosePoint(int idx, double dose)
        {
            iPtIndex = idx;
            doseValue = dose;
        }
        public int iPtIndex = 0;
        public double doseValue = 0;
    }

    public class DoseData
    {
        public DoseData() { }
        public DoseData(List<DosePoint> points, double dSumCutoffValues, int iNumCutoffValues)
        {
            dosePoints = points;
            m_iNumCutoffValues = iNumCutoffValues;
            m_dSumCutoffValues = dSumCutoffValues;
        }
        public List<DosePoint> dosePoints = new List<DosePoint>();
        public double m_dSumCutoffValues;
        public int m_iNumCutoffValues;
    };
    public abstract class DisplayProgress
    {
        public abstract void Message(string szMsg);
    }
    static class Helpers
    {
        public static void WriteJSONFile(Object hObj, string szPath)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (StreamWriter sw = new StreamWriter(szPath))
            {
                using (JsonTextWriter writer = new JsonTextWriter(sw))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 1;
                    writer.IndentChar = '\t';

                    serializer.Serialize(writer, hObj);
                }
            }
        }
    }
}

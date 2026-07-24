using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CalculateInfluenceMatrix
{
    // SFRThelper patch 17: the "grader". Opens a finished run folder, adds up
    // every small piece of the matrix for each field, and compares that sum
    // against the "answer key" (the one-shot Eclipse dose saved by the
    // reference-dose checkbox). It reports how close the two are, writes
    // small spreadsheet files (CSV) with the details so figures can be made
    // in any plotting tool, and saves a summary file with all the numbers.
    // This code only reads files - Eclipse is not involved at all.
    public static class Reconstruction_Validator
    {
        // How we decide which points to grade: only points where the answer
        // key shows at least this fraction of its own maximum. Points far
        // outside the beam carry almost no dose and would drown the report
        // in meaningless tiny numbers.
        private const double RegionFractionOfMax = 0.10;

        // If the average disagreement is worse than this fraction of the
        // maximum dose, the report raises a flag. Adding pieces one at a
        // time is expected to be close but not perfect; a few percent is
        // normal, much more than that deserves a look.
        private const double FlagFractionOfMax = 0.05;

        public static void ValidateRunFolder(string szRunFolder, Action<string> report)
        {
            report?.Invoke($"Grading run folder: {szRunFolder}");
            string szBeamsDir = Path.Combine(szRunFolder, "Beams");
            if (!Directory.Exists(szBeamsDir))
            {
                report?.Invoke("FAIL: no 'Beams' folder here - this does not look like a finished run.");
                return;
            }

            // The point positions file lets us draw dose profiles (dose along
            // a straight line through the hottest point) for the figures.
            float[,] arrCoords = null;
            string szVoxFile = Path.Combine(szRunFolder, "OptimizationVoxels_Data.h5");
            if (File.Exists(szVoxFile))
            {
                try { arrCoords = ReadFloats2D(szVoxFile, "/voxel_coordinate_XYZ_mm", 3); }
                catch (Exception ex) { report?.Invoke("Note: could not read point positions (" + ex.Message + "); profiles will be skipped."); }
            }
            else
                report?.Invoke("Note: point-positions file not found; profiles will be skipped.");

            var summary = new Dictionary<string, object>();
            bool bAnyGraded = false;

            foreach (string szBeamFile in Directory.GetFiles(szBeamsDir, "Beam_*_Data.h5").OrderBy(s => s))
            {
                string szName = Path.GetFileName(szBeamFile);
                string szFieldId = szName.Substring("Beam_".Length, szName.Length - "Beam_".Length - "_Data.h5".Length);
                report?.Invoke($"--- Field '{szFieldId}'");

                // 1) Read the matrix pieces and add them all up into one
                //    "everything at once" dose, point by point.
                float[] arrData; int[] arrIndices; int[] arrIndPtr; int[] arrShape;
                try
                {
                    arrData = ReadFloats1D(szBeamFile, "/inf_matrix_sparse/data");
                    arrIndices = ReadInts1D(szBeamFile, "/inf_matrix_sparse/indices");
                    arrIndPtr = ReadInts1D(szBeamFile, "/inf_matrix_sparse/indptr");
                    arrShape = ReadInts1D(szBeamFile, "/inf_matrix_sparse/shape");
                }
                catch (Exception ex)
                {
                    report?.Invoke("  FAIL: could not read the matrix from this file (" + ex.Message + ").");
                    continue;
                }
                int nVox = arrShape.Length > 0 ? arrShape[0] : 0;
                int nBeamlets = arrShape.Length > 1 ? arrShape[1] : 0;
                if (nVox <= 0 || arrIndPtr.Length != nBeamlets + 1 || arrIndPtr[nBeamlets] != arrData.Length)
                {
                    report?.Invoke("  FAIL: the matrix bookkeeping in this file is inconsistent - run Inspect Output for details.");
                    continue;
                }

                double[] arrRecon = new double[nVox];
                for (int i = 0; i < arrData.Length; i++)
                    arrRecon[arrIndices[i]] += arrData[i];
                report?.Invoke($"  summed {nBeamlets:N0} pieces ({arrData.Length:N0} saved values) into one dose.");

                // 2) Read the answer key for this field, if it was saved.
                string szRefFile = Path.Combine(szRunFolder, $"Reference_{szFieldId}_Data.h5");
                if (!File.Exists(szRefFile))
                {
                    report?.Invoke("  no answer key found for this field - run again with 'Export reference dose' " +
                        "ticked to enable the comparison. (Sum-only info: max of summed dose = " +
                        $"{arrRecon.Max():E3} Gy/MU.)");
                    continue;
                }
                float[] arrRef;
                try { arrRef = ReadFloats1D(szRefFile, "/reference_dose"); }
                catch (Exception ex)
                {
                    report?.Invoke("  FAIL: could not read the answer key (" + ex.Message + ").");
                    continue;
                }
                if (arrRef.Length != nVox)
                {
                    report?.Invoke($"  FAIL: the answer key has {arrRef.Length:N0} points but the matrix has {nVox:N0} - " +
                        "these files are not from compatible runs.");
                    continue;
                }

                // 3) Compare, inside the region that actually matters.
                double dRefMax = 0;
                int iRefMaxIdx = 0;
                for (int v = 0; v < nVox; v++)
                    if (arrRef[v] > dRefMax) { dRefMax = arrRef[v]; iRefMaxIdx = v; }
                if (dRefMax <= 0)
                {
                    report?.Invoke("  FAIL: the answer key is empty (all zero) - something went wrong in its export.");
                    continue;
                }
                double dRegionFloor = RegionFractionOfMax * dRefMax;

                long nRegion = 0;
                double dSumAbs = 0, dSumSigned = 0, dMaxAbs = 0, dSumRef = 0, dSumRecon = 0;
                long nWithin1 = 0, nWithin2 = 0, nWithin3 = 0, nWithin5 = 0;
                const int HistBins = 81;                       // -10% .. +10% in 0.25% steps
                long[] arrHist = new long[HistBins];
                for (int v = 0; v < nVox; v++)
                {
                    if (arrRef[v] < dRegionFloor) continue;
                    nRegion++;
                    double dDiff = arrRecon[v] - arrRef[v];
                    double dAbs = Math.Abs(dDiff);
                    dSumAbs += dAbs; dSumSigned += dDiff;
                    if (dAbs > dMaxAbs) dMaxAbs = dAbs;
                    dSumRef += arrRef[v]; dSumRecon += arrRecon[v];
                    double dPctOfMax = 100.0 * dDiff / dRefMax;
                    if (Math.Abs(dPctOfMax) <= 1.0) nWithin1++;
                    if (Math.Abs(dPctOfMax) <= 2.0) nWithin2++;
                    if (Math.Abs(dPctOfMax) <= 3.0) nWithin3++;
                    if (Math.Abs(dPctOfMax) <= 5.0) nWithin5++;
                    int iBin = (int)Math.Round((dPctOfMax + 10.0) / 0.25);
                    if (iBin < 0) iBin = 0;
                    if (iBin >= HistBins) iBin = HistBins - 1;
                    arrHist[iBin]++;
                }
                if (nRegion == 0)
                {
                    report?.Invoke("  FAIL: no points inside the grading region - the answer key and the matrix do not overlap.");
                    continue;
                }

                double dMeanAbsPct = 100.0 * (dSumAbs / nRegion) / dRefMax;
                double dMeanSignedPct = 100.0 * (dSumSigned / nRegion) / dRefMax;
                double dMaxAbsPct = 100.0 * dMaxAbs / dRefMax;
                double dScaleRatio = dSumRecon / dSumRef;

                report?.Invoke($"  answer-key max: {dRefMax:E3} Gy/MU; summed-pieces max: {arrRecon.Max():E3} Gy/MU");
                report?.Invoke($"  graded {nRegion:N0} points (where the answer key is at least {RegionFractionOfMax:P0} of its max):");
                report?.Invoke($"    average disagreement: {dMeanAbsPct:F2}% of max (typical direction: {dMeanSignedPct:+0.00;-0.00}%)");
                report?.Invoke($"    worst single point:   {dMaxAbsPct:F2}% of max");
                report?.Invoke($"    within 1% / 2% / 3% / 5% of max: " +
                    $"{100.0 * nWithin1 / nRegion:F1}% / {100.0 * nWithin2 / nRegion:F1}% / " +
                    $"{100.0 * nWithin3 / nRegion:F1}% / {100.0 * nWithin5 / nRegion:F1}% of points");
                report?.Invoke($"    overall amount of dose, summed pieces vs answer key: {dScaleRatio:F4} (1.0000 = identical)");
                if (dMeanAbsPct > 100.0 * FlagFractionOfMax)
                    report?.Invoke("    FLAG: the average disagreement is larger than expected - worth investigating before " +
                        "building on this matrix.");
                else
                    report?.Invoke("    RESULT: agreement is within the expected range for adding pieces one at a time.");

                // 4) Write the spreadsheet files for figures.
                WriteHistogramCsv(Path.Combine(szRunFolder, $"Validation_{szFieldId}_histogram.csv"), arrHist);
                if (arrCoords != null && arrCoords.GetLength(0) == nVox)
                {
                    WriteProfileCsv(Path.Combine(szRunFolder, $"Validation_{szFieldId}_profile_X.csv"), arrCoords, arrRef, arrRecon, iRefMaxIdx, 0);
                    WriteProfileCsv(Path.Combine(szRunFolder, $"Validation_{szFieldId}_profile_Y.csv"), arrCoords, arrRef, arrRecon, iRefMaxIdx, 1);
                    WriteProfileCsv(Path.Combine(szRunFolder, $"Validation_{szFieldId}_profile_Z.csv"), arrCoords, arrRef, arrRecon, iRefMaxIdx, 2);
                    report?.Invoke("  saved: difference histogram + three dose profiles through the hottest point (CSV).");
                }
                else
                    report?.Invoke("  saved: difference histogram (CSV). Profiles skipped (no matching point positions).");

                summary[szFieldId] = new Dictionary<string, object>
                {
                    { "points_graded", nRegion },
                    { "answer_key_max_GyPerMU", dRefMax },
                    { "summed_pieces_max_GyPerMU", arrRecon.Max() },
                    { "average_disagreement_percent_of_max", dMeanAbsPct },
                    { "typical_direction_percent_of_max", dMeanSignedPct },
                    { "worst_point_percent_of_max", dMaxAbsPct },
                    { "fraction_within_2_percent", (double)nWithin2 / nRegion },
                    { "fraction_within_5_percent", (double)nWithin5 / nRegion },
                    { "total_dose_ratio", dScaleRatio },
                    { "grading_region", $"answer key >= {RegionFractionOfMax:P0} of its max" }
                };
                bAnyGraded = true;
            }

            if (bAnyGraded)
            {
                string szSummary = Path.Combine(szRunFolder, "Validation_Summary.json");
                Helpers.WriteJSONFile(summary, szSummary);
                report?.Invoke($"Summary saved: {Path.GetFileName(szSummary)}");
                report?.Invoke("=== GRADING DONE ===");
            }
            else
                report?.Invoke("=== GRADING DONE (no fields could be compared) ===");
        }

        // ---- small file helpers -------------------------------------------

        // A dose profile is the dose along one straight line through the
        // hottest point - the classic picture for comparing two dose
        // distributions. We collect every point that shares the hottest
        // point's position on the other two axes, sort along the chosen
        // axis, and write position + both doses per row.
        private static void WriteProfileCsv(string szPath, float[,] arrCoords, float[] arrRef, double[] arrRecon,
            int iCenterIdx, int iAxis)
        {
            int iA = iAxis, iB = (iAxis + 1) % 3, iC = (iAxis + 2) % 3;
            float fB = arrCoords[iCenterIdx, iB], fC = arrCoords[iCenterIdx, iC];
            // Points on the same line must match on the two other axes.
            // Half the grid spacing is a safe matching distance; the spacing
            // is measured from the data so any grid size works.
            float fTol = 0.45f * EstimateSpacing(arrCoords, iB);
            var rows = new List<Tuple<float, float, double>>();
            int n = arrRef.Length;
            for (int v = 0; v < n; v++)
            {
                if (Math.Abs(arrCoords[v, iB] - fB) < fTol && Math.Abs(arrCoords[v, iC] - fC) < fTol)
                    rows.Add(Tuple.Create(arrCoords[v, iA], arrRef[v], arrRecon[v]));
            }
            rows.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            using (var w = new StreamWriter(szPath))
            {
                w.WriteLine("position_mm,answer_key_GyPerMU,summed_pieces_GyPerMU");
                foreach (var r in rows)
                    w.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:E6},{2:E6}", r.Item1, r.Item2, r.Item3));
            }
        }

        // Works out the grid spacing along one axis by finding the smallest
        // real step between point positions in a sample of the data.
        private static float EstimateSpacing(float[,] arrCoords, int iAxis)
        {
            int n = Math.Min(arrCoords.GetLength(0), 200000);
            var vals = new List<float>(2048);
            for (int v = 0; v < n; v += 37)
                vals.Add(arrCoords[v, iAxis]);
            vals.Sort();
            float fMin = float.MaxValue;
            for (int i = 1; i < vals.Count; i++)
            {
                float d = vals[i] - vals[i - 1];
                if (d > 1e-3f && d < fMin) fMin = d;
            }
            return (fMin == float.MaxValue) ? 2.5f : fMin;
        }

        private static void WriteHistogramCsv(string szPath, long[] arrHist)
        {
            using (var w = new StreamWriter(szPath))
            {
                w.WriteLine("difference_percent_of_max,point_count");
                for (int i = 0; i < arrHist.Length; i++)
                {
                    double dCenter = -10.0 + 0.25 * i;
                    w.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2},{1}", dCenter, arrHist[i]));
                }
            }
        }

        // ---- small readers for the data files -----------------------------
        // These open a file, pull one named block of numbers into memory, and
        // close everything again. Any problem becomes an error message
        // instead of a wrong answer.

        private static float[] ReadFloats1D(string szFile, string szDataset)
        {
            long fileId = HDF.PInvoke.H5F.open(szFile, HDF.PInvoke.H5F.ACC_RDONLY);
            if (fileId < 0) throw new IOException("cannot open " + Path.GetFileName(szFile));
            try
            {
                ulong nLen = GetLength(fileId, szDataset);
                float[] arr = new float[nLen];
                ReadInto(fileId, szDataset, HDF.PInvoke.H5T.NATIVE_FLOAT, arr);
                return arr;
            }
            finally { HDF.PInvoke.H5F.close(fileId); }
        }

        private static int[] ReadInts1D(string szFile, string szDataset)
        {
            long fileId = HDF.PInvoke.H5F.open(szFile, HDF.PInvoke.H5F.ACC_RDONLY);
            if (fileId < 0) throw new IOException("cannot open " + Path.GetFileName(szFile));
            try
            {
                ulong nLen = GetLength(fileId, szDataset);
                int[] arr = new int[nLen];
                ReadInto(fileId, szDataset, HDF.PInvoke.H5T.NATIVE_INT32, arr);
                return arr;
            }
            finally { HDF.PInvoke.H5F.close(fileId); }
        }

        private static float[,] ReadFloats2D(string szFile, string szDataset, int iCols)
        {
            long fileId = HDF.PInvoke.H5F.open(szFile, HDF.PInvoke.H5F.ACC_RDONLY);
            if (fileId < 0) throw new IOException("cannot open " + Path.GetFileName(szFile));
            try
            {
                ulong nTotal = GetLength(fileId, szDataset);        // total number of values
                ulong nRows = nTotal / (ulong)iCols;
                float[,] arr = new float[nRows, iCols];
                ReadInto(fileId, szDataset, HDF.PInvoke.H5T.NATIVE_FLOAT, arr);
                return arr;
            }
            finally { HDF.PInvoke.H5F.close(fileId); }
        }

        private static ulong GetLength(long fileId, string szDataset)
        {
            long dsId = HDF.PInvoke.H5D.open(fileId, szDataset);
            if (dsId < 0) throw new IOException("missing block '" + szDataset + "'");
            long spaceId = -1;
            try
            {
                spaceId = HDF.PInvoke.H5D.get_space(dsId);
                int nDims = HDF.PInvoke.H5S.get_simple_extent_ndims(spaceId);
                ulong[] dims = new ulong[nDims];
                HDF.PInvoke.H5S.get_simple_extent_dims(spaceId, dims, null);
                ulong nTotal = 1;
                foreach (ulong d in dims) nTotal *= d;
                return nTotal;
            }
            finally
            {
                if (spaceId >= 0) HDF.PInvoke.H5S.close(spaceId);
                HDF.PInvoke.H5D.close(dsId);
            }
        }

        private static void ReadInto(long fileId, string szDataset, long typeId, Array arr)
        {
            long dsId = HDF.PInvoke.H5D.open(fileId, szDataset);
            if (dsId < 0) throw new IOException("missing block '" + szDataset + "'");
            GCHandle pin = default(GCHandle);
            try
            {
                pin = GCHandle.Alloc(arr, GCHandleType.Pinned);
                if (HDF.PInvoke.H5D.read(dsId, typeId, HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5S.ALL,
                        HDF.PInvoke.H5P.DEFAULT, pin.AddrOfPinnedObject()) < 0)
                    throw new IOException("could not read block '" + szDataset + "'");
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
                HDF.PInvoke.H5D.close(dsId);
            }
        }
    }
}
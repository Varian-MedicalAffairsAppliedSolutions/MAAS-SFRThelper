using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CalculateInfluenceMatrix
{
    /// <summary>
    /// SFRThelper-authored (not part of the upstream import). In-app
    /// verification of an extraction run folder: confirms the side files
    /// exist and, for every beam file, that the CSR sparse matrix is
    /// internally consistent (data/indices lengths agree, indptr is
    /// monotonic, starts at 0, ends at nnz, and has beamlets+1 entries
    /// matching the explicit shape). This replaces external scipy/h5py
    /// inspection - everything runs in-process via HDF.PInvoke.
    /// </summary>
    public static class Inspection_Helpers
    {
        public static bool InspectRunFolder(string szRunFolder, Action<string> report)
        {
            if (!Directory.Exists(szRunFolder))
            {
                report?.Invoke($"Folder not found: {szRunFolder}");
                return false;
            }
            report?.Invoke($"Inspecting: {szRunFolder}");

            bool bAllOk = true;
            string[] arrSideFiles = new string[]
            {
                "OptimizationVoxels_Data.h5", "OptimizationVoxels_MetaData.json",
                "StructureSet_Data.h5", "StructureSet_MetaData.json"
            };
            foreach (string szFile in arrSideFiles)
            {
                string szPath = Path.Combine(szRunFolder, szFile);
                if (File.Exists(szPath))
                    report?.Invoke($"Found {szFile} ({new FileInfo(szPath).Length:N0} bytes)");
                else
                {
                    report?.Invoke($"MISSING: {szFile}");
                    bAllOk = false;
                }
            }

            string szBeams = Path.Combine(szRunFolder, "Beams");
            if (!Directory.Exists(szBeams))
            {
                report?.Invoke("MISSING: Beams folder");
                return false;
            }
            string[] arrBeamFiles = Directory.GetFiles(szBeams, "Beam_*_Data.h5");
            if (arrBeamFiles.Length == 0)
            {
                report?.Invoke("No beam data files found in Beams folder.");
                return false;
            }
            foreach (string szFile in arrBeamFiles.OrderBy(s => s))
                bAllOk &= InspectBeamFile(szFile, report);

            report?.Invoke(bAllOk ? "=== INSPECTION PASSED ===" : "=== INSPECTION FOUND PROBLEMS ===");
            return bAllOk;
        }

        public static bool InspectBeamFile(string szPath, Action<string> report)
        {
            report?.Invoke($"--- {Path.GetFileName(szPath)}");
            long fileId = HDF.PInvoke.H5F.open(szPath, HDF.PInvoke.H5F.ACC_RDONLY);
            if (fileId < 0)
            {
                report?.Invoke("  FAIL: file does not open (unflushed or corrupt?).");
                return false;
            }
            try
            {
                if (HDF.PInvoke.H5L.exists(fileId, "/inf_matrix_sparse") <= 0)
                {
                    report?.Invoke("  FAIL: /inf_matrix_sparse group missing (run incomplete?).");
                    return false;
                }

                long[] arrShape = ReadIntVector(fileId, "/inf_matrix_sparse/shape", report);
                long[] arrIndPtr = ReadIntVector(fileId, "/inf_matrix_sparse/indptr", report);
                ulong nData = GetVectorLength(fileId, "/inf_matrix_sparse/data", report);
                ulong nIdx = GetVectorLength(fileId, "/inf_matrix_sparse/indices", report);
                if (arrShape == null || arrIndPtr == null ||
                    nData == ulong.MaxValue || nIdx == ulong.MaxValue)
                    return false;

                bool bOk = true;
                if (arrShape.Length != 2)
                {
                    report?.Invoke($"  FAIL: shape has {arrShape.Length} entries, expected 2.");
                    bOk = false;
                }
                else
                {
                    report?.Invoke($"  shape: {arrShape[0]:N0} voxels x {arrShape[1]:N0} beamlets");
                }
                if (nData != nIdx)
                {
                    report?.Invoke($"  FAIL: data length {nData:N0} != indices length {nIdx:N0}.");
                    bOk = false;
                }
                if (arrShape.Length == 2 && arrIndPtr.Length != arrShape[1] + 1)
                {
                    report?.Invoke($"  FAIL: indptr length {arrIndPtr.Length:N0} != beamlets+1 ({arrShape[1] + 1:N0}).");
                    bOk = false;
                }
                if (arrIndPtr.Length > 0 && arrIndPtr[0] != 0)
                {
                    report?.Invoke("  FAIL: indptr does not start at 0.");
                    bOk = false;
                }
                if (arrIndPtr.Length > 0 && (ulong)arrIndPtr[arrIndPtr.Length - 1] != nData)
                {
                    report?.Invoke($"  FAIL: indptr end {arrIndPtr[arrIndPtr.Length - 1]:N0} != nnz {nData:N0}.");
                    bOk = false;
                }
                for (int i = 1; i < arrIndPtr.Length; i++)
                {
                    if (arrIndPtr[i] < arrIndPtr[i - 1])
                    {
                        report?.Invoke($"  FAIL: indptr not monotonic at entry {i}.");
                        bOk = false;
                        break;
                    }
                }
                if (bOk && arrShape.Length == 2 && arrShape[0] > 0 && arrShape[1] > 0)
                {
                    double dDensity = nData / ((double)arrShape[0] * arrShape[1]);
                    report?.Invoke($"  nnz {nData:N0} ({dDensity:P2} dense); indptr consistent");
                }

                // SFRThelper: an empty matrix is a FAILURE, not a pass. With
                // nnz = 0 every structural check above is vacuously true -
                // this is exactly how the first-light all-zero run reported
                // PASSED. Zero nonzeros means the extraction captured no
                // dose at all (all values zero, or NaN upstream, since NaN
                // fails every threshold comparison and is never harvested).
                if (nData == 0)
                {
                    report?.Invoke("  FAIL: nnz = 0 - the matrix contains NO nonzero entries. " +
                        "Structurally valid but physically empty; do not use this file. " +
                        "Check the run log for readout diagnostics (dScale / MetersetPerGy / maxRawInt).");
                    bOk = false;
                }
                else
                {
                    bOk &= ScanSparseDataValues(fileId, nData, report);
                }

                bool bFull = HDF.PInvoke.H5L.exists(fileId, "/inf_matrix_full") > 0;
                bool bLeak = HDF.PInvoke.H5L.exists(fileId, "/closed_mlc_leakage") > 0;
                report?.Invoke($"  full matrix: {(bFull ? "present" : "absent")}; closed-MLC leakage: {(bLeak ? "present" : "absent")}");
                return bOk;
            }
            finally
            {
                HDF.PInvoke.H5F.close(fileId);
            }
        }

        // SFRThelper: value-level scan of /inf_matrix_sparse/data. Reports
        // min/max and fails on NaN, Infinity, or non-positive entries (the
        // sparse harvest keeps only values strictly above the cutoff, and
        // the cutoff is never negative, so any entry <= 0 is a bug). Guarded
        // by an in-memory cap so a pathological file cannot exhaust RAM.
        private const ulong MaxDataScanEntries = 64UL * 1024 * 1024;   // 256 MB of float32

        private static bool ScanSparseDataValues(long fileId, ulong nData, Action<string> report)
        {
            if (nData > MaxDataScanEntries)
            {
                report?.Invoke($"  value scan skipped: {nData:N0} entries exceeds the " +
                    $"{MaxDataScanEntries:N0}-entry in-memory cap (structure checks above still apply).");
                return true;
            }

            float[] arrData = new float[nData];
            long datasetId = HDF.PInvoke.H5D.open(fileId, "/inf_matrix_sparse/data");
            if (datasetId < 0)
            {
                report?.Invoke("  FAIL: cannot open /inf_matrix_sparse/data for value scan.");
                return false;
            }
            GCHandle pin = default(GCHandle);
            try
            {
                pin = GCHandle.Alloc(arrData, GCHandleType.Pinned);
                if (HDF.PInvoke.H5D.read(datasetId, HDF.PInvoke.H5T.NATIVE_FLOAT,
                        HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5S.ALL,
                        HDF.PInvoke.H5P.DEFAULT, pin.AddrOfPinnedObject()) < 0)
                {
                    report?.Invoke("  FAIL: cannot read /inf_matrix_sparse/data for value scan.");
                    return false;
                }
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
                HDF.PInvoke.H5D.close(datasetId);
            }

            long nNaN = 0, nInf = 0, nNonPositive = 0;
            float fMin = float.MaxValue, fMax = float.MinValue;
            for (long i = 0; i < (long)nData; i++)
            {
                float v = arrData[i];
                if (float.IsNaN(v)) { nNaN++; continue; }
                if (float.IsInfinity(v)) { nInf++; continue; }
                if (v <= 0f) nNonPositive++;
                if (v < fMin) fMin = v;
                if (v > fMax) fMax = v;
            }

            bool bOk = true;
            if (nNaN > 0)
            {
                report?.Invoke($"  FAIL: {nNaN:N0} NaN entries in sparse data (units/prescription problem upstream).");
                bOk = false;
            }
            if (nInf > 0)
            {
                report?.Invoke($"  FAIL: {nInf:N0} Infinity entries in sparse data (dWeight near zero upstream?).");
                bOk = false;
            }
            if (nNonPositive > 0)
            {
                report?.Invoke($"  FAIL: {nNonPositive:N0} entries <= 0 in sparse data (harvest should keep only values above the cutoff).");
                bOk = false;
            }
            if (bOk)
                report?.Invoke($"  data values: min={fMin:E3}, max={fMax:E3} (finite, positive)");
            return bOk;
        }

        private static ulong GetVectorLength(long fileId, string szDataset, Action<string> report)
        {
            if (HDF.PInvoke.H5L.exists(fileId, szDataset) <= 0)
            {
                report?.Invoke($"  FAIL: missing {szDataset}");
                return ulong.MaxValue;
            }
            long datasetId = HDF.PInvoke.H5D.open(fileId, szDataset);
            if (datasetId < 0)
            {
                report?.Invoke($"  FAIL: cannot open {szDataset}");
                return ulong.MaxValue;
            }
            long spaceId = -1;
            try
            {
                spaceId = HDF.PInvoke.H5D.get_space(datasetId);
                ulong[] dims = new ulong[1];
                HDF.PInvoke.H5S.get_simple_extent_dims(spaceId, dims, null);
                return dims[0];
            }
            finally
            {
                if (spaceId >= 0) HDF.PInvoke.H5S.close(spaceId);
                HDF.PInvoke.H5D.close(datasetId);
            }
        }

        private static long[] ReadIntVector(long fileId, string szDataset, Action<string> report)
        {
            ulong uLen = GetVectorLength(fileId, szDataset, report);
            if (uLen == ulong.MaxValue)
                return null;

            int[] arrBuffer = new int[uLen];
            if (uLen == 0)
                return new long[0];

            long datasetId = HDF.PInvoke.H5D.open(fileId, szDataset);
            if (datasetId < 0)
            {
                report?.Invoke($"  FAIL: cannot open {szDataset}");
                return null;
            }
            GCHandle pin = default(GCHandle);
            try
            {
                pin = GCHandle.Alloc(arrBuffer, GCHandleType.Pinned);
                if (HDF.PInvoke.H5D.read(datasetId, HDF.PInvoke.H5T.NATIVE_INT32,
                        HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5S.ALL,
                        HDF.PInvoke.H5P.DEFAULT, pin.AddrOfPinnedObject()) < 0)
                {
                    report?.Invoke($"  FAIL: cannot read {szDataset}");
                    return null;
                }
            }
            finally
            {
                if (pin.IsAllocated) pin.Free();
                HDF.PInvoke.H5D.close(datasetId);
            }
            return arrBuffer.Select(v => (long)v).ToArray();
        }
    }
}
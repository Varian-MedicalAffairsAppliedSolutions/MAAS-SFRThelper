using HDF.PInvoke;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Verifies that native HDF5 loads and works inside the current host
    /// process: writes a chunked, gzip-compressed dataset, closes all
    /// handles, reopens the file, reads back, compares. Exercises the
    /// exact storage features the influence-matrix export relies on.
    /// No ESAPI. Returns true on full round-trip success.
    /// </summary>
    public static class HdfSmokeTest
    {
        public static bool Run(IRunProgress progress)
        {
            string path = Path.Combine(Path.GetTempPath(), "SFRThelper_HdfSmokeTest.h5");

            // --- 1. Native resolution: the first P/Invoke is the moment of truth.
            try
            {
                uint maj = 0, min = 0, rel = 0;
                H5.open();
                H5.get_libversion(ref maj, ref min, ref rel);
                progress.Message($"Native HDF5 loaded, version {maj}.{min}.{rel}.");
            }
            catch (DllNotFoundException ex)
            {
                progress.Message("FAIL: hdf5.dll not found by the host process. " + ex.Message);
                return false;
            }
            catch (BadImageFormatException ex)
            {
                progress.Message("FAIL: hdf5.dll bitness mismatch (x86 vs x64). " + ex.Message);
                return false;
            }

            if (H5Z.filter_avail(H5Z.filter_t.DEFLATE) <= 0)
            {
                progress.Message("FAIL: gzip (deflate) filter unavailable in this build.");
                return false;
            }
            progress.Message("Gzip filter available.");

            // --- 2. Test data: deterministic pattern, 64 x 256 floats.
            const int rows = 64, cols = 256;
            float[] written = new float[rows * cols];
            for (int i = 0; i < written.Length; i++)
                written[i] = i * 0.5f;

            long fileId = -1, spaceId = -1, dcplId = -1, dsetId = -1;
            try
            {
                // --- 3. Write: chunked + gzip, mirroring the production layout.
                fileId = H5F.create(path, H5F.ACC_TRUNC);
                if (fileId < 0) { progress.Message("FAIL: H5F.create."); return false; }

                spaceId = H5S.create_simple(2, new ulong[] { rows, cols }, null);
                dcplId = H5P.create(H5P.DATASET_CREATE);
                H5P.set_chunk(dcplId, 2, new ulong[] { 16, 64 });
                H5P.set_deflate(dcplId, 4);

                dsetId = H5D.create(fileId, "smoke_test", H5T.NATIVE_FLOAT, spaceId,
                                    H5P.DEFAULT, dcplId, H5P.DEFAULT);
                if (dsetId < 0) { progress.Message("FAIL: H5D.create."); return false; }

                GCHandle pin = GCHandle.Alloc(written, GCHandleType.Pinned);
                try
                {
                    if (H5D.write(dsetId, H5T.NATIVE_FLOAT, H5S.ALL, H5S.ALL,
                                  H5P.DEFAULT, pin.AddrOfPinnedObject()) < 0)
                    { progress.Message("FAIL: H5D.write."); return false; }
                }
                finally { pin.Free(); }
            }
            finally
            {
                if (dsetId >= 0) H5D.close(dsetId);
                if (dcplId >= 0) H5P.close(dcplId);
                if (spaceId >= 0) H5S.close(spaceId);
                if (fileId >= 0) H5F.close(fileId);
            }
            progress.Message($"Wrote and closed: {path}");

            // --- 4. Reopen and read back into a fresh buffer.
            float[] read = new float[rows * cols];
            fileId = -1; dsetId = -1;
            try
            {
                fileId = H5F.open(path, H5F.ACC_RDONLY);
                if (fileId < 0) { progress.Message("FAIL: reopen."); return false; }

                dsetId = H5D.open(fileId, "smoke_test");
                GCHandle pin = GCHandle.Alloc(read, GCHandleType.Pinned);
                try
                {
                    if (H5D.read(dsetId, H5T.NATIVE_FLOAT, H5S.ALL, H5S.ALL,
                                 H5P.DEFAULT, pin.AddrOfPinnedObject()) < 0)
                    { progress.Message("FAIL: H5D.read."); return false; }
                }
                finally { pin.Free(); }
            }
            finally
            {
                if (dsetId >= 0) H5D.close(dsetId);
                if (fileId >= 0) H5F.close(fileId);
            }

            // --- 5. Verdict.
            for (int i = 0; i < written.Length; i++)
            {
                if (read[i] != written[i])
                {
                    progress.Message($"FAIL: mismatch at element {i}.");
                    return false;
                }
            }
            progress.Message("Round trip verified: all values match. HDF5 is ready.");
            return true;
        }
    }
}
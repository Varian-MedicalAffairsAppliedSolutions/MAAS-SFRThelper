using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CalculateInfluenceMatrix
{
    /// <summary>
    /// SFRThelper shim replacing the HDF5CSharp wrapper ("Hdf5" class) that
    /// upstream MAAS-DoseInfluenceMatrix referenced. Implements, over raw
    /// HDF.PInvoke, exactly the six methods the imported code calls, with
    /// matching semantics:
    ///   CreateFile  - create/truncate, returns file id
    ///   OpenFile    - open read-write (append semantics), returns file id
    ///   CreateOrOpenGroup - open group at absolute path, creating if absent
    ///   CloseGroup / CloseFile
    ///   WriteDatasetFromArray&lt;T&gt; - write a whole 1/2/3-D array of
    ///       int / float / double / byte as a contiguous dataset
    /// Large matrix datasets do NOT pass through here; they use the chunked,
    /// compressed path in HDFCompression_Helpers unchanged.
    /// This file is SFRThelper-authored (not part of the upstream import).
    /// </summary>
    public static class Hdf5
    {
        public static long CreateFile(string path)
        {
            long fileId = HDF.PInvoke.H5F.create(path, HDF.PInvoke.H5F.ACC_TRUNC);
            if (fileId < 0)
                throw new IOException($"HDF5: failed to create file '{path}'.");
            return fileId;
        }

        public static long OpenFile(string path)
        {
            long fileId = HDF.PInvoke.H5F.open(path, HDF.PInvoke.H5F.ACC_RDWR);
            if (fileId < 0)
                throw new IOException($"HDF5: failed to open file '{path}' for writing.");
            return fileId;
        }

        public static long CreateOrOpenGroup(long fileId, string groupPath)
        {
            long groupId;
            if (HDF.PInvoke.H5L.exists(fileId, groupPath) > 0)
                groupId = HDF.PInvoke.H5G.open(fileId, groupPath);
            else
                groupId = HDF.PInvoke.H5G.create(fileId, groupPath);
            if (groupId < 0)
                throw new IOException($"HDF5: failed to create or open group '{groupPath}'.");
            return groupId;
        }

        public static void CloseGroup(long groupId)
        {
            if (groupId >= 0)
                HDF.PInvoke.H5G.close(groupId);
        }

        public static void CloseFile(long fileId)
        {
            if (fileId >= 0)
                HDF.PInvoke.H5F.close(fileId);
        }

        public static void WriteDatasetFromArray<T>(long locationId, string datasetName, Array data)
            where T : struct
        {
            long dtype = NativeTypeFor(typeof(T));

            int rank = data.Rank;
            ulong[] dims = new ulong[rank];
            ulong totalElements = 1;
            for (int i = 0; i < rank; i++)
            {
                dims[i] = (ulong)data.GetLength(i);
                totalElements *= dims[i];
            }
            int elemSize = ElementSizeFor(typeof(T));

            long spaceId = -1, dcplId = -1, datasetId = -1;
            GCHandle pin = default(GCHandle);
            try
            {
                spaceId = HDF.PInvoke.H5S.create_simple(rank, dims, null);
                if (spaceId < 0)
                    throw new IOException($"HDF5: failed to create dataspace for '{datasetName}'.");

                // Datasets above 1 MB are chunked + shuffled + gzip-6. This
                // matters enormously for the structure masks (full-CT byte
                // arrays, mostly zeros) and the CT-to-dose voxel map; small
                // side datasets stay contiguous.
                long dcplForCreate = HDF.PInvoke.H5P.DEFAULT;
                if (totalElements * (ulong)elemSize >= (1UL << 20) && totalElements > 0)
                {
                    dcplId = HDF.PInvoke.H5P.create(HDF.PInvoke.H5P.DATASET_CREATE);
                    if (dcplId < 0)
                        throw new IOException($"HDF5: failed to create property list for '{datasetName}'.");
                    if (HDF.PInvoke.H5P.set_chunk(dcplId, rank, ChunkFor(dims)) < 0)
                        throw new IOException($"HDF5: failed to set chunking for '{datasetName}'.");
                    HDF.PInvoke.H5P.set_shuffle(dcplId);
                    HDF.PInvoke.H5P.set_deflate(dcplId, 6);
                    dcplForCreate = dcplId;
                }

                datasetId = HDF.PInvoke.H5D.create(locationId, datasetName, dtype, spaceId,
                    HDF.PInvoke.H5P.DEFAULT, dcplForCreate, HDF.PInvoke.H5P.DEFAULT);
                if (datasetId < 0)
                    throw new IOException($"HDF5: failed to create dataset '{datasetName}' " +
                        "(does it already exist at this location?).");

                // .NET arrays of any rank are one contiguous row-major block,
                // which is exactly HDF5's memory layout - pin and hand over.
                pin = GCHandle.Alloc(data, GCHandleType.Pinned);
                if (HDF.PInvoke.H5D.write(datasetId, dtype,
                        HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5S.ALL,
                        HDF.PInvoke.H5P.DEFAULT, pin.AddrOfPinnedObject()) < 0)
                    throw new IOException($"HDF5: failed to write dataset '{datasetName}'.");
            }
            finally
            {
                if (pin.IsAllocated)
                    pin.Free();
                if (datasetId >= 0)
                    HDF.PInvoke.H5D.close(datasetId);
                if (dcplId >= 0)
                    HDF.PInvoke.H5P.close(dcplId);
                if (spaceId >= 0)
                    HDF.PInvoke.H5S.close(spaceId);
            }
        }

        private static ulong[] ChunkFor(ulong[] dims)
        {
            // Per-rank caps keeping chunks at roughly 1M elements, never
            // exceeding the dataset extent on any axis.
            ulong[] caps = dims.Length == 1 ? new ulong[] { 262144 }
                         : dims.Length == 2 ? new ulong[] { 1024, 1024 }
                         : new ulong[] { 128, 128, 16 };
            ulong[] chunk = new ulong[dims.Length];
            for (int i = 0; i < dims.Length; i++)
                chunk[i] = Math.Min(dims[i] == 0 ? 1 : dims[i], caps[i]);
            return chunk;
        }

        private static int ElementSizeFor(Type t)
        {
            if (t == typeof(byte)) return 1;
            if (t == typeof(int) || t == typeof(float)) return 4;
            if (t == typeof(double)) return 8;
            return 8;
        }

        private static long NativeTypeFor(Type t)
        {
            if (t == typeof(int)) return HDF.PInvoke.H5T.NATIVE_INT32;
            if (t == typeof(float)) return HDF.PInvoke.H5T.NATIVE_FLOAT;
            if (t == typeof(double)) return HDF.PInvoke.H5T.NATIVE_DOUBLE;
            if (t == typeof(byte)) return HDF.PInvoke.H5T.NATIVE_UINT8;
            throw new NotSupportedException(
                $"Hdf5 shim: element type '{t.Name}' is not supported " +
                "(supported: int, float, double, byte).");
        }
    }
}
using System;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;
using H5S = HDF.PInvoke.H5S;

namespace CalculateInfluenceMatrix
{
    public static class HDFCompression_Helpers
    {
        public static void WriteCompressedMatrix<T>(long fileId, string datasetPath, T[,] matrix, bool isFirstSpot) where T : struct
        {
            long dataspaceId = -1;
            long dcpl = -1;
            long datasetId = -1;
            GCHandle handle = default;


            try
            {
                // Get dimensions for dataset
                ulong[] dims = new ulong[] {
            (ulong)matrix.GetLength(0),    // Number of voxels
            1                              // Start with one column for first spot
        };

                // For the first spot, we need to create the dataset with extensible dimensions
                if (isFirstSpot)
                {
                    // Create dataspace with maximum dimensions
                    // H5S.UNLIMITED allows infinite extension in the second dimension
                    ulong[] maxDims = new ulong[] {
                (ulong)matrix.GetLength(0),    // Fixed number of voxels
                HDF.PInvoke.H5S.UNLIMITED     // Unlimited spots
            };

                    dataspaceId = HDF.PInvoke.H5S.create_simple(2, dims, maxDims);
                    if (dataspaceId < 0) throw new Exception("Failed to create dataspace");

                    // Create property list for dataset creation
                    dcpl = HDF.PInvoke.H5P.create(HDF.PInvoke.H5P.DATASET_CREATE);
                    if (dcpl < 0) throw new Exception("Failed to create property list");

                    // Set chunk size for efficient expansion and compression
                    ulong[] chunkDims = new ulong[] {
                (ulong)Math.Min(matrix.GetLength(0), 1024), // Chunk height
                1                                           // One spot per chunk
            };

                    if (HDF.PInvoke.H5P.set_chunk(dcpl, 2, chunkDims) < 0)
                        throw new Exception("Failed to set chunk size");

                    // Enable compression
                    if (HDF.PInvoke.H5P.set_shuffle(dcpl) < 0)
                        throw new Exception("Failed to set shuffle filter");
                    if (HDF.PInvoke.H5P.set_deflate(dcpl, 9) < 0)
                        throw new Exception("Failed to set compression");

                    // Select appropriate data type
                    long datatype = typeof(T) == typeof(float) ?
                        HDF.PInvoke.H5T.NATIVE_FLOAT :
                        HDF.PInvoke.H5T.NATIVE_DOUBLE;

                    // Create the extensible dataset
                    datasetId = HDF.PInvoke.H5D.create(fileId, datasetPath, datatype, dataspaceId,
                        HDF.PInvoke.H5P.DEFAULT, dcpl, HDF.PInvoke.H5P.DEFAULT);

                    if (datasetId < 0) throw new Exception("Failed to create dataset");

                    //Log.Information($"Created new extensible dataset: {datasetPath}");
                }
                else
                {
                    // Open existing dataset
                    datasetId = HDF.PInvoke.H5D.open(fileId, datasetPath);
                    if (datasetId < 0) throw new Exception($"Failed to open dataset: {datasetPath}");

                    // Get current dimensions
                    long spaceId = HDF.PInvoke.H5D.get_space(datasetId);
                    ulong[] currentDims = new ulong[2];
                    HDF.PInvoke.H5S.get_simple_extent_dims(spaceId, currentDims, null);
                    HDF.PInvoke.H5S.close(spaceId);

                    // Extend dataset by one column
                    ulong[] newDims = new ulong[] { currentDims[0], currentDims[1] + 1 };
                    if (HDF.PInvoke.H5D.set_extent(datasetId, newDims) < 0)
                        throw new Exception("Failed to extend dataset");

                    //Log.Information($"Extended dataset to {newDims[1]} spots");
                }

                // Write the data
                handle = GCHandle.Alloc(matrix, GCHandleType.Pinned);
                IntPtr buf = handle.AddrOfPinnedObject();

                if (!isFirstSpot)
                {
                    // For subsequent spots, write to the last column
                    long memspace = HDF.PInvoke.H5S.create_simple(2, dims, null);
                    long filespace = HDF.PInvoke.H5D.get_space(datasetId);

                    ulong[] currentDims = new ulong[2];
                    HDF.PInvoke.H5S.get_simple_extent_dims(filespace, currentDims, null);

                    // Select the last column for writing
                    ulong[] start = new ulong[] { 0, currentDims[1] - 1 };
                    ulong[] count = new ulong[] { dims[0], 1 };
                    HDF.PInvoke.H5S.select_hyperslab(filespace, H5S.seloper_t.SET, start, null, count, null);

                    if (HDF.PInvoke.H5D.write(datasetId, HDF.PInvoke.H5T.NATIVE_FLOAT, memspace, filespace,
                        HDF.PInvoke.H5P.DEFAULT, buf) < 0)
                    {
                        throw new Exception("Failed to write data to dataset");
                    }

                    HDF.PInvoke.H5S.close(memspace);
                    HDF.PInvoke.H5S.close(filespace);
                }
                else
                {
                    // For first spot, we can write directly
                    if (HDF.PInvoke.H5D.write(datasetId, HDF.PInvoke.H5T.NATIVE_FLOAT,
                        HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5P.DEFAULT, buf) < 0)
                    {
                        throw new Exception("Failed to write initial data to dataset");
                    }
                }

                //Log.Information($"Successfully wrote data to {datasetPath}");
            }
            catch (Exception)
            {
                //Log.Error($"Error writing compressed matrix: {ex.Message}");
                throw;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
                if (datasetId >= 0)
                    HDF.PInvoke.H5D.close(datasetId);
                if (dataspaceId >= 0)
                    HDF.PInvoke.H5S.close(dataspaceId);
                if (dcpl >= 0)
                    HDF.PInvoke.H5P.close(dcpl);
            }
        }

        // Modified to accept dataset path as parameter
        public static void CreateInitialDataset(long fileId, string datasetPath, Array initialData)
        {
            long dataspaceId = -1;
            long dcpl = -1;
            long datasetId = -1;

            try
            {
                // First, determine if this is a sparse matrix by checking dimensions
                bool isSparseMatrix = initialData.GetLength(1) > 1;

                // Create initial dimensions based on the data
                ulong[] dims = new ulong[] {
                    (ulong)initialData.GetLength(0),
                    (ulong)initialData.GetLength(1)
                };

                // Here's the crucial part - we set maximum dimensions differently for sparse vs full
                ulong[] maxDims = new ulong[] {
                    isSparseMatrix ? HDF.PInvoke.H5S.UNLIMITED : dims[0],  // Allow rows to grow for sparse
                    isSparseMatrix ? dims[1] : HDF.PInvoke.H5S.UNLIMITED   // Allow columns to grow for full
                };

                // Create dataspace with extensible dimensions
                dataspaceId = HDF.PInvoke.H5S.create_simple(2, dims, maxDims);
                if (dataspaceId < 0) throw new Exception("Failed to create dataspace");

                // Set up chunking and compression
                dcpl = HDF.PInvoke.H5P.create(HDF.PInvoke.H5P.DATASET_CREATE);
                if (dcpl < 0) throw new Exception("Failed to create property list");

                // Choose chunk size based on matrix type
                ulong[] chunkDims;
                if (isSparseMatrix)
                {
                    // For sparse matrix, chunk by rows
                    chunkDims = new ulong[] { 1000, dims[1] };
                }
                else
                {
                    // For full matrix, chunk by columns
                    chunkDims = new ulong[] { dims[0], 1 };
                }

                if (HDF.PInvoke.H5P.set_chunk(dcpl, 2, chunkDims) < 0)
                    throw new Exception("Failed to set chunk size");

                // Enable compression
                if (HDF.PInvoke.H5P.set_shuffle(dcpl) < 0)
                    throw new Exception("Failed to set shuffle filter");
                if (HDF.PInvoke.H5P.set_deflate(dcpl, 9) < 0)
                    throw new Exception("Failed to set compression");

                // Create dataset with appropriate data type
                long datatype = initialData is float[,]?
                    HDF.PInvoke.H5T.NATIVE_FLOAT :
                    HDF.PInvoke.H5T.NATIVE_DOUBLE;

                datasetId = HDF.PInvoke.H5D.create(fileId, datasetPath, datatype, dataspaceId,
                    HDF.PInvoke.H5P.DEFAULT, dcpl, HDF.PInvoke.H5P.DEFAULT);

                if (datasetId < 0)
                    throw new Exception("Failed to create dataset");

                // Write initial data
                GCHandle handle = GCHandle.Alloc(initialData, GCHandleType.Pinned);
                try
                {
                    if (HDF.PInvoke.H5D.write(datasetId, datatype,
                        HDF.PInvoke.H5S.ALL, HDF.PInvoke.H5S.ALL,
                        HDF.PInvoke.H5P.DEFAULT, handle.AddrOfPinnedObject()) < 0)
                    {
                        throw new Exception("Failed to write initial data");
                    }
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }

                //Log.Information($"Successfully created extensible dataset: {datasetPath}");
            }
            finally
            {
                // Cleanup
                if (datasetId >= 0) HDF.PInvoke.H5D.close(datasetId);
                if (dataspaceId >= 0) HDF.PInvoke.H5S.close(dataspaceId);
                if (dcpl >= 0) HDF.PInvoke.H5P.close(dcpl);
            }
        }
        public static void AppendToDataset(long fileId, string datasetPath, Array newData)
        {
            // Initialize all our HDF5 identifiers to -1 (invalid)
            // This helps us track what needs cleanup in the finally block
            long datasetId = -1;
            long memspace = -1;
            long filespace = -1;
            GCHandle handle = default;

            try
            {
                // First determine if we're dealing with a sparse or full matrix
                // Sparse matrices have 3 columns (point index, spot index, dose value)
                // Full matrices have 1 column (just the dose value)
                bool isSparseMatrix = newData.GetLength(1) > 1;

                // Open the existing dataset that we want to append to
                datasetId = HDF.PInvoke.H5D.open(fileId, datasetPath);
                if (datasetId < 0)
                    throw new Exception($"Failed to open dataset: {datasetPath}");

                // Get the current dimensions of our dataset
                filespace = HDF.PInvoke.H5D.get_space(datasetId);
                ulong[] currentDims = new ulong[2];
                ulong[] maxDims = new ulong[2];
                HDF.PInvoke.H5S.get_simple_extent_dims(filespace, currentDims, maxDims);

                // We're done with this filespace, close it before creating a new one
                HDF.PInvoke.H5S.close(filespace);
                filespace = -1;  // Mark as closed

                // Calculate new dimensions based on matrix type
                ulong[] newDims;
                if (isSparseMatrix)
                {
                    // For sparse matrix, we add rows (each spot has variable number of points)
                    newDims = new ulong[] {
                        currentDims[0] + (ulong)newData.GetLength(0),  // Add new rows
                        3  // Keep 3 columns (fixed)
                    };
                }
                else
                {
                    // For full matrix, we add a column (one new spot)
                    newDims = new ulong[] {
                        currentDims[0],  // Keep same number of rows
                        currentDims[1] + 1  // Add one column
                    };
                }

                // Extend the dataset to accommodate new data
                if (HDF.PInvoke.H5D.set_extent(datasetId, newDims) < 0)
                    throw new Exception("Failed to extend dataset");

                // Get the new filespace after extending
                filespace = HDF.PInvoke.H5D.get_space(datasetId);

                // Create memory space describing our new data's shape
                ulong[] memDims = new ulong[] {
                    (ulong)newData.GetLength(0),
                    (ulong)newData.GetLength(1)
                };
                memspace = HDF.PInvoke.H5S.create_simple(2, memDims, null);

                // Calculate where in the dataset we should write
                ulong[] start;
                if (isSparseMatrix)
                {
                    // For sparse matrix, start at the end of existing rows
                    start = new ulong[] { currentDims[0], 0 };
                }
                else
                {
                    // For full matrix, start at the new column
                    start = new ulong[] { 0, currentDims[1] };
                }

                // Select the portion of the file where we'll write
                if (HDF.PInvoke.H5S.select_hyperslab(filespace, H5S.seloper_t.SET,
                    start, null, memDims, null) < 0)
                {
                    throw new Exception("Failed to select hyperslab");
                }

                // Pin our data so it doesn't move during the write operation
                handle = GCHandle.Alloc(newData, GCHandleType.Pinned);
                IntPtr buf = handle.AddrOfPinnedObject();

                // Determine the correct data type for our data
                long datatype = newData is float[,]?
                    HDF.PInvoke.H5T.NATIVE_FLOAT :
                    HDF.PInvoke.H5T.NATIVE_DOUBLE;

                // Write the new data
                if (HDF.PInvoke.H5D.write(datasetId, datatype, memspace, filespace,
                    HDF.PInvoke.H5P.DEFAULT, buf) < 0)
                {
                    throw new Exception("Failed to write data");
                }

                //Log.Information($"Successfully appended data to {datasetPath}");
            }
            catch (Exception ex)
            {
                //Log.Error($"Error in AppendToDataset: {ex.Message}");
                throw ex;
            }
            finally
            {
                // Clean up all HDF5 resources in reverse order of creation
                if (handle.IsAllocated)
                    handle.Free();
                if (memspace >= 0)
                    HDF.PInvoke.H5S.close(memspace);
                if (filespace >= 0)
                    HDF.PInvoke.H5S.close(filespace);
                if (datasetId >= 0)
                    HDF.PInvoke.H5D.close(datasetId);
            }
        }
        public static void VerifyCompression(string filePath)
        {
            long fileId = -1;
            try
            {
                fileId = HDF.PInvoke.H5F.open(filePath, HDF.PInvoke.H5F.ACC_RDONLY);
                if (fileId < 0) throw new Exception("Failed to open file");

                // Get file size
                long fileSize = new FileInfo(filePath).Length;
                Log.Information($"HDF5 file size: {fileSize:N0} bytes");

                // Check dataset properties
                void CheckDataset(string datasetPath)
                {
                    long datasetId = HDF.PInvoke.H5D.open(fileId, datasetPath);
                    if (datasetId < 0) return;

                    try
                    {
                        long dcpl_id = HDF.PInvoke.H5D.get_create_plist(datasetId);
                        int nfilters = HDF.PInvoke.H5P.get_nfilters(dcpl_id);

                        ulong[] dims = new ulong[2];
                        long dataspaceId = HDF.PInvoke.H5D.get_space(datasetId);
                        HDF.PInvoke.H5S.get_simple_extent_dims(dataspaceId, dims, null);

                        long storage_size = (long)HDF.PInvoke.H5D.get_storage_size(datasetId);
                        double compression_ratio = (dims[0] * dims[1] * sizeof(double)) / (double)storage_size;

                        Log.Information($"Dataset: {datasetPath}");
                        Log.Information($"Dimensions: {dims[0]} x {dims[1]}");
                        Log.Information($"Storage size: {storage_size:N0} bytes");
                        Log.Information($"Compression ratio: {compression_ratio:F2}:1");
                        Log.Information($"Number of filters: {nfilters}");

                        HDF.PInvoke.H5P.close(dcpl_id);
                        HDF.PInvoke.H5S.close(dataspaceId);
                    }
                    finally
                    {
                        HDF.PInvoke.H5D.close(datasetId);
                    }
                }

                CheckDataset("/inf_matrix_full");
                CheckDataset("/inf_matrix_sparse");
            }
            finally
            {
                if (fileId >= 0)
                    HDF.PInvoke.H5F.close(fileId);
            }
        }
    }
}

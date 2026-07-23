using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;

using CalculateInfluenceMatrix;

namespace PhotonCalculateInfluenceMatrix
{
    static class Helpers
    {

        // SFRThelper patch 4: voxel read and sparse harvest are separated so
        // thresholding runs on the LEAKAGE-CORRECTED matrix. Upstream's
        // GetDosePoints (below, now unused) did both in one pass over the raw
        // dose, so sparse entries kept leakage baked in while the full matrix
        // had it subtracted - two disagreeing matrices in one file.
        public static void FillDoseMatrix(BeamDose hDose, double dWeight, ref float[,] arrFullDoseMatrix)
        {
            if (hDose is null)
            {
                throw new ApplicationException("Dose does not exist.");
            }

            double dIntercept = hDose.VoxelToDoseValue(0).Dose;
            double dScale = hDose.VoxelToDoseValue(1).Dose - dIntercept;
            int iXSize = hDose.XSize;
            int iYSize = hDose.YSize;
            int iZSize = hDose.ZSize;
            int[,] doseBuffer = new int[iXSize, iYSize];
            int iIdxOffset = 0, iPtIndex;
            for (int sliceIndex = 0; sliceIndex < iZSize; sliceIndex++)
            {
                hDose.GetVoxels(sliceIndex, doseBuffer);
                iIdxOffset = sliceIndex * iYSize * iXSize;
                for (int j = 0; j < iYSize; j++)
                {
                    for (int i = 0; i < iXSize; i++)
                    {
                        iPtIndex = iIdxOffset + j * iXSize + i;
                        double pointDose = doseBuffer[i, j] * dScale + dIntercept;
                        pointDose = pointDose / dWeight;
                        arrFullDoseMatrix[iPtIndex, 0] = (float)pointDose;
                    }
                }
            }
        }

        // SFRThelper patch 4: harvest sparse entries from an already-corrected
        // matrix. The cutoff is the caller's (UI-controlled) value; with
        // cutoff = 0 the sparse set is exactly the nonzeros of the matrix.
        public static DoseData ExtractSparsePoints(float[,] arrDoseMatrix, double dCutoffValue)
        {
            List<DosePoint> lstBeamletDose = new List<DosePoint>();
            double dSumCutOffValues = 0;
            int iCutOffValueCnt = 0;
            int iCnt = arrDoseMatrix.GetLength(0);
            for (int iPtIndex = 0; iPtIndex < iCnt; iPtIndex++)
            {
                double pointDose = arrDoseMatrix[iPtIndex, 0];
                if (pointDose > dCutoffValue)
                {
                    lstBeamletDose.Add(new DosePoint(iPtIndex, pointDose));
                }
                else if (pointDose > 0)
                {
                    dSumCutOffValues += pointDose;
                    iCutOffValueCnt++;
                }
            }
            return new DoseData(lstBeamletDose, dSumCutOffValues, iCutOffValueCnt);
        }

        public static DoseData GetDosePoints(BeamDose hDose, double dWeight, double dCutoffValue, ref float[,] arrFullDoseMatrix)
        {
            if (hDose is null)
            {
                throw new ApplicationException("Dose does not exist.");
            }

            double dIntercept = hDose.VoxelToDoseValue(0).Dose;
            double dScale = hDose.VoxelToDoseValue(1).Dose - dIntercept;
            int iXSize = hDose.XSize;
            int iYSize = hDose.YSize;
            int iZSize = hDose.ZSize;
            int[,] doseBuffer = new int[iXSize, iYSize];
            List<DosePoint> lstBeamletDose = new List<DosePoint>();
            double dSumCutOffValues = 0;
            int iCutOffValueCnt = 0;
            int iIdxOffset = 0, iPtIndex;
            for (int sliceIndex = 0; sliceIndex < iZSize; sliceIndex++)
            {
                hDose.GetVoxels(sliceIndex, doseBuffer);    // values are in
                iIdxOffset = sliceIndex * iYSize * iXSize;
                for (int j = 0; j < iYSize; j++)
                {
                    for (int i = 0; i < iXSize; i++)
                    {
                        iPtIndex = iIdxOffset + j * iXSize + i;

                        double pointDose = doseBuffer[i, j] * dScale + dIntercept;
                        pointDose = pointDose / dWeight;

                        if (pointDose > dCutoffValue)
                        {
                            lstBeamletDose.Add(new DosePoint(iPtIndex, pointDose));
                        }
                        else if (pointDose > 0)
                        {
                            dSumCutOffValues += pointDose;
                            iCutOffValueCnt++;
                        }

                        arrFullDoseMatrix[iPtIndex, 0] = (float)pointDose;
                    }
                }
            }
            return new DoseData(lstBeamletDose, dSumCutOffValues, iCutOffValueCnt);
        }

        // SFRThelper patch 13a: szScratchPrescriptionNote records which
        // prescription the scratch plan carried during extraction (copied
        // from source, or the nominal fallback). Provenance only - the
        // matrix values do not depend on it.
        public static void WriteBeamMetaData(Beam b, MyBeamParameters beamParams, double dInfMatrixCutoffValue, float fDoseScalingFactor, string szOutputFile, string szScratchPrescriptionNote = null)
        {
            ControlPoint firstCP = b.ControlPoints[0];

            string szBeamID = b.Id;
            string szFilename = $"Beam_{szBeamID}_Data.h5";
            Dictionary<string, object> dctBeamData = new Dictionary<string, object>
            {
                { "ID", szBeamID },
                { "gantry_angle", (float)firstCP.GantryAngle },
                { "couch_angle", (float)firstCP.PatientSupportAngle },
                { "collimator_angle" , (float)firstCP.CollimatorAngle },
                { "iso_center", new Dictionary<string, float> {
                        { "x_mm", (float)b.IsocenterPosition.x },
                        { "y_mm", (float)b.IsocenterPosition.y },
                        { "z_mm", (float)b.IsocenterPosition.z }
                   }
                },
                { "beamlets", new Dictionary<string, string> {
                        { "id_File" , $"{szFilename}/beamlets/id" },
                        { "width_mm_File" , $"{szFilename}/beamlets/width_mm" },
                        { "height_mm_File" , $"{szFilename}/beamlets/height_mm" },
                        { "position_x_mm_File" , $"{szFilename}/beamlets/position_x_mm" },
                        { "position_y_mm_File" , $"{szFilename}/beamlets/position_y_mm" },
                        { "MLC_leaf_idx_File" , $"{szFilename}/beamlets/MLC_leaf_idx" },
                        { "grid_x_idx_File" , $"{szFilename}/beamlets/grid_x_idx" },
                        { "grid_y_idx_File" , $"{szFilename}/beamlets/grid_y_idx" }
                    }
                },
                { "jaw_position" , new Dictionary<string, float>{ { "top_left_x_mm", (float)firstCP.JawPositions.X1 }, { "top_left_y_mm", (float)firstCP.JawPositions.Y1 }, { "bottom_right_x_mm", (float)firstCP.JawPositions.X2 }, {"bottom_right_y_mm", (float)firstCP.JawPositions.Y2 } } },
                { "BEV_structure_contour_points_File" , $"{szFilename}/BEV_structure_contour_points"},
                { "MLC_name" ,  b.MLC.Name},
                { "beam_modality" , b.Technique.Id},
                { "energy_MV" ,  b.EnergyModeDisplayName},
                { "SSD_mm" , b.SSD},
                { "SAD_mm" , b.TreatmentUnit.SourceAxisDistance},
                { "influenceMatrixSparse_File", $"{szFilename}/inf_matrix_sparse" },
                { "influenceMatrixSparse_format", "CSC of (voxels x beamlets): data float32, indices int32 (voxel index), indptr int32 (num_beamlets+1), shape int32[2] = [num_voxels, num_beamlets]" },
                { "influenceMatrixSparse_tol", dInfMatrixCutoffValue },
                { "influenceMatrixFull_File", $"{szFilename}/inf_matrix_full" },
                { "closedMLCLeakage_File", $"{szFilename}/closed_mlc_leakage" },
                { "MLC_leaves_pos_y_mm_File" ,  $"{szFilename}/MLC_leaves_pos_y_mm"},
                { "machine_name" , b.TreatmentUnit.Id},
                // SFRThelper patch 3: every exported file documents its own
                // unit convention so no downstream consumer needs archaeology.
                { "dose_units", new Dictionary<string, object> {
                        { "column_meaning", "dose per 1 MU of this beamlet's beam (plan beam weights NOT applied)" },
                        { "formula", "stored_value = (raw_dose / (MetersetPerGy / PRESET_DOSE_NORMALIZATION) - closed_mlc_leakage) * DoseScalingFactor" },
                        { "closed_mlc_leakage_units", "per-MU, before DoseScalingFactor" },
                        { "MetersetPerGy", b.MetersetPerGy },
                        { "PRESET_DOSE_NORMALIZATION", 100.0 },
                        { "DoseScalingFactor", fDoseScalingFactor },
                        // SFRThelper patch 13a: scratch-plan prescription
                        // provenance. Matrix values are per-MU physics and
                        // independent of the prescription; it exists only so
                        // absolute dose presentation is defined during readout.
                        { "scratch_plan_prescription", szScratchPrescriptionNote ?? "unknown (not recorded by this build)" }
                    }
                }
            };
            CalculateInfluenceMatrix.Helpers.WriteJSONFile(dctBeamData, szOutputFile);
        }

        public static void CreateDataSet<T>(long fileId, string szDataSetName, Array arrDataSet) where T : struct
        {
            string[] arrTokens = szDataSetName.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int iLen = arrTokens.Length - 1;
            List<long> lstGroups = new List<long>();
            string szGroupName = "";
            long lGroupID = fileId;
            for (int i = 0; i < iLen; i++)
            {
                szGroupName += "/" + arrTokens[i];
                lGroupID = Hdf5.CreateOrOpenGroup(fileId, szGroupName);
                lstGroups.Add(lGroupID);
            }

            string szName = arrTokens[iLen];
            Hdf5.WriteDatasetFromArray<T>(lGroupID, szName, arrDataSet);

            for (int i = iLen - 1; i >= 0; i--)
                Hdf5.CloseGroup(lstGroups[i]);
        }
        public static void WriteInfMatrixHDF5(bool bExportFullInfMatrix, MyBeamParameters beamParams, float[,] arrFullDoseMatrix, DoseData doseData, string szPath)
        {
            long fileId;
            bool bAppend = System.IO.File.Exists(szPath);
            if (bAppend)
                fileId = Hdf5.OpenFile(szPath);
            else
                fileId = Hdf5.CreateFile(szPath);
            // SFRThelper patch 8: this method is called once per beamlet and
            // upstream never closed fileId - thousands of leaked handles per
            // run, and unflushed buffers can leave the file unloadable. The
            // finally below guarantees the handle closes on every exit path.
            try
            {
                string szTempFile = szPath + ".tmp";
                if (bExportFullInfMatrix)
                {
                    bool fullMatrixExists = HDF.PInvoke.H5L.exists(fileId, "/inf_matrix_full") > 0;
                    //Log.Information($"Full matrix dataset {(fullMatrixExists ? "exists" : "does not exist")}");

                    if (!fullMatrixExists)
                    {
                        HDFCompression_Helpers.CreateInitialDataset(fileId, "/inf_matrix_full", arrFullDoseMatrix);
                    }
                    else
                    {
                        HDFCompression_Helpers.AppendToDataset(fileId, "/inf_matrix_full", arrFullDoseMatrix);
                    }
                }

                // write sparse inf matrix - SFRThelper storage patch: CSR layout
                // (data f32 / indices i32 / indptr i32 + explicit shape) replacing
                // COO float64 triplets. Rows are beamlets (CSC of the
                // voxels-x-beamlets matrix): appending a beamlet appends one row.
                // indptr accumulates in beamParams and is written at beam
                // finalization - a file without indptr is visibly incomplete.
                // Upstream's fake zero entry at the last voxel index (shape
                // smuggled through a sentinel row) is gone: shape is explicit.
                List<DosePoint> lstDosePoints = doseData.dosePoints;
                int iPtCnt = lstDosePoints.Count;
                float[] arrData = new float[iPtCnt];
                int[] arrIndices = new int[iPtCnt];
                for (int i = 0; i < iPtCnt; i++)
                {
                    DosePoint dp = lstDosePoints[i];
                    arrIndices[i] = dp.iPtIndex;
                    // patch 4 completion: values are already scaled when
                    // harvested; upstream's *fDoseScalingFactor here would now
                    // double-scale the sparse entries.
                    arrData[i] = (float)dp.doseValue;
                }
                beamParams.m_lstCsrIndPtr.Add(
                    beamParams.m_lstCsrIndPtr[beamParams.m_lstCsrIndPtr.Count - 1] + iPtCnt);

                bool sparseMatrixExists = HDF.PInvoke.H5L.exists(fileId, "/inf_matrix_sparse") > 0;
                if (!sparseMatrixExists)
                {
                    long gid = Hdf5.CreateOrOpenGroup(fileId, "/inf_matrix_sparse");
                    Hdf5.CloseGroup(gid);
                    HDFCompression_Helpers.CreateAppendableVector<float>(fileId, "/inf_matrix_sparse/data", arrData);
                    HDFCompression_Helpers.CreateAppendableVector<int>(fileId, "/inf_matrix_sparse/indices", arrIndices);
                }
                else
                {
                    HDFCompression_Helpers.AppendToVector<float>(fileId, "/inf_matrix_sparse/data", arrData);
                    HDFCompression_Helpers.AppendToVector<int>(fileId, "/inf_matrix_sparse/indices", arrIndices);
                }
            }
            finally
            {
                Hdf5.CloseFile(fileId);
            }
        }
        public static void WriteBeamletInfoHDF5(MyBeamParameters beamParams, int iMaxPointCnt, string szPath)
        {
            long fileId;
            bool bAppend = System.IO.File.Exists(szPath);
            if (bAppend)
                fileId = Hdf5.OpenFile(szPath);
            else
                fileId = Hdf5.CreateFile(szPath);

            int iBeamletCnt = beamParams.BeamletCount;
            int[] arrId = new int[iBeamletCnt];
            float[] arrXPos = new float[iBeamletCnt];
            float[] arrYPos = new float[iBeamletCnt];
            float[] arrXSize = new float[iBeamletCnt];
            float[] arrYSize = new float[iBeamletCnt];
            double[] arrSumOfCutoffValues = new double[iBeamletCnt];
            int[] arrNumCutoffValues = new int[iBeamletCnt];
            // SFRThelper patch 9: enumeration-grid indices per beamlet.
            int[] arrGridX = new int[iBeamletCnt];
            int[] arrGridY = new int[iBeamletCnt];
            for (int i = 0; i < iBeamletCnt; i++)
            {
                Beamlet bl = beamParams.m_lstBeamlets[i];
                arrId[i] = bl.m_iIndex;
                arrXPos[i] = bl.m_fXStart + bl.m_fXSize / 2.0f;
                arrYPos[i] = bl.m_fYStart + bl.m_fYSize / 2.0f;
                arrXSize[i] = bl.m_fXSize;
                arrYSize[i] = bl.m_fYSize;
                arrSumOfCutoffValues[i] = bl.m_dSumCutoffValues;
                arrNumCutoffValues[i] = bl.m_iNumCutoffValues;
                arrGridX[i] = bl.m_iGridX;
                arrGridY[i] = bl.m_iGridY;
            }
            Helpers.CreateDataSet<int>(fileId, "/beamlets/id", arrId);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/position_x_mm", arrXPos);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/width_mm", arrXSize);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/position_y_mm", arrYPos);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/height_mm", arrYSize);
            Helpers.CreateDataSet<int>(fileId, "/beamlets/grid_x_idx", arrGridX);
            Helpers.CreateDataSet<int>(fileId, "/beamlets/grid_y_idx", arrGridY);
            // SFRThelper storage patch: CSR finalization - the bookmarks and
            // the explicit [num_voxels, num_beamlets] shape.
            Helpers.CreateDataSet<int>(fileId, "/inf_matrix_sparse/indptr", beamParams.m_lstCsrIndPtr.ToArray());
            Helpers.CreateDataSet<int>(fileId, "/inf_matrix_sparse/shape", new int[2] { iMaxPointCnt, iBeamletCnt });
            // SFRThelper patch 5: the closed-MLC leakage field as its own
            // dataset - same per-MU units as the matrix columns, stored
            // BEFORE DoseScalingFactor (subtraction happens pre-scaling).
            // With it, reconstruction is an exact identity:
            //   dose = sum_b(A_b * MU_b) / s + (sum MU) * L_b
            if (beamParams.m_arrClosedMLCDoseMatrix != null)
                Helpers.CreateDataSet<float>(fileId, "/closed_mlc_leakage", beamParams.m_arrClosedMLCDoseMatrix);
            Helpers.CreateDataSet<double>(fileId, "/beamlets/sum_cutoff_value", arrSumOfCutoffValues);
            Helpers.CreateDataSet<int>(fileId, "/beamlets/cutoff_value_cnt", arrNumCutoffValues);

            Hdf5.CloseFile(fileId);
        }
    }
}
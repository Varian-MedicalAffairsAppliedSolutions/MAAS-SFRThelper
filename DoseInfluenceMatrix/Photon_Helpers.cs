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
        // SFRThelper patch 15: the cutoff defines the matrix, it does not fork
        // it. Sub-cutoff entries are ZEROED in the matrix during this pass, so
        // the sparse harvest and the (optional) full export that follows are
        // the same thresholded matrix in two encodings - the reconstruction
        // identity sparse == full holds by construction at ANY cutoff. The
        // discarded mass stays audited per beamlet (count/sum below). Upstream
        // MSK thresholded the sparse harvest only, on RAW pre-leakage dose, at
        // tol 0.5 legacy units (~5E-5 Gy/MU, ~5% of a beamlet peak): their
        // sparse and full differed by both threshold and physics. Cutoff 0
        // remains the unthresholded, bit-exact escape hatch. The closed-MLC
        // leakage dataset is deliberately NOT thresholded: it reconstructs the
        // raw beam dose (column + leakage), a different object.
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
                    arrDoseMatrix[iPtIndex, 0] = 0f;   // patch 15: threshold the matrix itself
                }
            }
            return new DoseData(lstBeamletDose, dSumCutOffValues, iCutOffValueCnt);
        }

        // SFRThelper patch 16: saves the reference dose ("answer key") files -
        // one data file with the dose values for every point in the grid, in
        // the same point ordering the matrix files use, plus a small text
        // file describing what it is and how it was made.
        public static void WriteReferenceDoseFiles(float[,] arrDose, Dictionary<string, object> meta, string szH5File, string szJsonFile)
        {
            long fileId = Hdf5.CreateFile(szH5File);
            try
            {
                Hdf5.WriteDatasetFromArray<float>(fileId, "reference_dose", arrDose);
            }
            finally
            {
                Hdf5.CloseFile(fileId);
            }
            System.IO.File.WriteAllText(szJsonFile, string.Empty);   // start clean even if a stale file exists
            CalculateInfluenceMatrix.Helpers.WriteJSONFile(meta, szJsonFile);
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
        // SFRThelper patch 14.2: szReadoutModeNote records how presented
        // dose was converted to Gy per MU (absolute honored, or the
        // relative(%)/MetersetPerGy derivation).
        public static void WriteBeamMetaData(Beam b, MyBeamParameters beamParams, double dInfMatrixCutoffValue, float fDoseScalingFactor, string szOutputFile, string szScratchPrescriptionNote = null, string szReadoutModeNote = null)
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
                        { "position_convention" , "position_x_mm / position_y_mm are beamlet rectangle CENTRES (edges = position +/- size/2), inherited from the MSK format" },
                        { "MLC_leaf_idx_File" , $"{szFilename}/beamlets/MLC_leaf_idx" },
                        { "grid_x_idx_File" , $"{szFilename}/beamlets/grid_x_idx" },
                        { "grid_y_idx_File" , $"{szFilename}/beamlets/grid_y_idx" },
                        { "sum_clamped_value_File" , $"{szFilename}/beamlets/sum_clamped_value" },
                        { "clamped_value_cnt_File" , $"{szFilename}/beamlets/clamped_value_cnt" },
                        { "clamp_stats_meaning" , "patch 19: count and signed sum of negative (beamlet - closed_mlc_leakage) marginals zeroed by the subtraction clamp, per beamlet, post-scale per-MU units" }
                    }
                },
                { "closed_leaf_park_mode" , PhotonInfluenceMatrixCalc.PARK_MODE.ToString() },
                { "closed_leaf_park_position_mm" , (PhotonInfluenceMatrixCalc.PARK_MODE == PhotonInfluenceMatrixCalc.ParkMode.FixedInField) ? (object)PhotonInfluenceMatrixCalc.PARK_POSITION_MM : "adaptive: per-beamlet, see beamlets/park_position_mm; baseline parks at X1-1 (under the left jaw)" },
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
                // SFRThelper patch 15: the tol above defines the matrix, not
                // just the sparse encoding - sub-cutoff entries are zeroed
                // before BOTH exports, so sparse and full are the same matrix
                // and sparse == full holds at any tol. Discarded mass is
                // audited per beamlet (sub_cutoff count/sum in beamlet info).
                { "matrix_thresholding", "entries <= tol zeroed in the matrix before export; sparse and full encodings are identical; tol 0 = unthresholded; closed_mlc_leakage is never thresholded" },
                { "influenceMatrixFull_File", $"{szFilename}/inf_matrix_full" },
                { "closedMLCLeakage_File", $"{szFilename}/closed_mlc_leakage" },
                { "MLC_leaves_pos_y_mm_File" ,  $"{szFilename}/MLC_leaves_pos_y_mm"},
                { "machine_name" , b.TreatmentUnit.Id},
                // SFRThelper patch 3 (units rewritten by patch 14): every
                // exported file documents its own unit convention so no
                // downstream consumer needs archaeology.
                { "dose_units", new Dictionary<string, object> {
                        { "column_meaning", "dose in Gy per 1 MU of this beamlet's beam (plan beam weights NOT applied)" },
                        { "formula", "stored_value = (Gy_per_MU - closed_mlc_leakage) * DoseScalingFactor; Gy_per_MU = beam dose in Gy divided by that beam's own machine units (independent of plan scaling and of how many beams the working plan holds)" },
                        { "closed_mlc_leakage_units", "Gy per MU, before DoseScalingFactor" },
                        { "dose_readout", szReadoutModeNote ?? "unknown (not recorded by this build)" },
                        { "MetersetPerGy", b.MetersetPerGy },
                        { "PRESET_METERSET_MU", PhotonInfluenceMatrixCalc.PRESET_METERSET_MU },
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
            // SFRThelper patch 19: clamp visibility - per-beamlet count and
            // signed sum of negative marginals zeroed by the subtraction
            // clamp, exported beside the cutoff stats they were hiding
            // behind. Units: post-scale per-MU, same as the matrix columns.
            double[] arrSumClampedValues = new double[iBeamletCnt];
            int[] arrNumClampedValues = new int[iBeamletCnt];
            // SFRThelper patch 21: per-beamlet park position (BEV-X mm) -
            // constant in FixedInField mode, per-pose under AdaptiveNearJaw.
            float[] arrParkPos = new float[iBeamletCnt];
            // SFRThelper patch 9: enumeration-grid indices per beamlet.
            int[] arrGridX = new int[iBeamletCnt];
            int[] arrGridY = new int[iBeamletCnt];
            for (int i = 0; i < iBeamletCnt; i++)
            {
                Beamlet bl = beamParams.m_lstBeamlets[i];
                arrId[i] = bl.m_iIndex;
                // NOTE (convention, verified against MSK original): the
                // exported position_x_mm / position_y_mm are the rectangle
                // CENTRES, not corners - edges = position +/- size/2. A
                // reader that treats position as the left edge misplaces
                // every column boundary by half a beamlet (this exact
                // misreading produced a wrong boundary verdict on
                // 2026-07-24; do not repeat it).
                arrXPos[i] = bl.m_fXStart + bl.m_fXSize / 2.0f;
                arrYPos[i] = bl.m_fYStart + bl.m_fYSize / 2.0f;
                arrXSize[i] = bl.m_fXSize;
                arrYSize[i] = bl.m_fYSize;
                arrSumOfCutoffValues[i] = bl.m_dSumCutoffValues;
                arrNumCutoffValues[i] = bl.m_iNumCutoffValues;
                arrSumClampedValues[i] = bl.m_dSumClampedValues;
                arrNumClampedValues[i] = bl.m_iNumClampedValues;
                arrParkPos[i] = bl.m_fParkPos;
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
            // SFRThelper patch 19: the clamp stats, beside the cutoff stats.
            Helpers.CreateDataSet<double>(fileId, "/beamlets/sum_clamped_value", arrSumClampedValues);
            Helpers.CreateDataSet<int>(fileId, "/beamlets/clamped_value_cnt", arrNumClampedValues);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/park_position_mm", arrParkPos);

            Hdf5.CloseFile(fileId);
        }
    }
}
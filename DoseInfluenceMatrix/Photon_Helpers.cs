using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;

using HDF5CSharp;
using CalculateInfluenceMatrix;

namespace PhotonCalculateInfluenceMatrix
{
    static class Helpers
    {
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

                        arrFullDoseMatrix[iPtIndex,0] = (float)pointDose;
                    }
                }
            }
            return new DoseData(lstBeamletDose, dSumCutOffValues, iCutOffValueCnt);
        }

        public static void WriteBeamMetaData(Beam b, MyBeamParameters beamParams, double dInfMatrixCutoffValue, string szOutputFile)
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
                        { "MLC_leaf_idx_File" , $"{szFilename}/beamlets/MLC_leaf_idx" }
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
                { "influenceMatrixSparse_tol", dInfMatrixCutoffValue },
                { "influenceMatrixFull_File", $"{szFilename}/inf_matrix_full" },
                { "MLC_leaves_pos_y_mm_File" ,  $"{szFilename}/MLC_leaves_pos_y_mm"},
                { "machine_name" , b.TreatmentUnit.Id}
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
        public static void WriteInfMatrixHDF5(bool bExportFullInfMatrix, float[,] arrFullDoseMatrix, DoseData doseData, bool bAddLastEntry, int iMaxPointCnt, int iBeamletIdx, float fDoseScalingFactor, string szPath)
        {
            long fileId;
            bool bAppend = System.IO.File.Exists(szPath);
            if (bAppend)
                fileId = Hdf5.OpenFile(szPath);
            else
                fileId = Hdf5.CreateFile(szPath);
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

            // write sparse inf matrix
            List<DosePoint> lstDosePoints = doseData.dosePoints;
            int iPtCnt = lstDosePoints.Count;
            double[,] arrSparse = new double[(bAddLastEntry ? iPtCnt + 1 : iPtCnt), 3];
            for (int i = 0; i < iPtCnt; i++)
            {
                DosePoint dp = lstDosePoints[i];
                arrSparse[i, 0] = dp.iPtIndex;
                arrSparse[i, 1] = iBeamletIdx;
                arrSparse[i, 2] = dp.doseValue*fDoseScalingFactor;
            }
            if (bAddLastEntry)
            {
                arrSparse[iPtCnt, 0] = iMaxPointCnt - 1;
                arrSparse[iPtCnt, 1] = iBeamletIdx;
                arrSparse[iPtCnt, 2] = 0;
            }

            // Check if sparse matrix dataset exists
            bool sparseMatrixExists = HDF.PInvoke.H5L.exists(fileId, "/inf_matrix_sparse") > 0;
            if (!sparseMatrixExists)
            {
                HDFCompression_Helpers.CreateInitialDataset(fileId, "/inf_matrix_sparse", arrSparse);
            }
            else
            {
                HDFCompression_Helpers.AppendToDataset(fileId, "/inf_matrix_sparse", arrSparse);
            }
        }
        public static void WriteBeamletInfoHDF5(MyBeamParameters beamParams, string szPath)
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
            for ( int i=0; i<iBeamletCnt; i++)
            {
                Beamlet bl = beamParams.m_lstBeamlets[i];
                arrId[i] = bl.m_iIndex;
                arrXPos[i] = bl.m_fXStart + bl.m_fXSize/2.0f;
                arrYPos[i] = bl.m_fYStart + bl.m_fYSize / 2.0f;
                arrXSize[i] = bl.m_fXSize;
                arrYSize[i] = bl.m_fYSize;
                arrSumOfCutoffValues[i] = bl.m_dSumCutoffValues;
                arrNumCutoffValues[i] = bl.m_iNumCutoffValues;
            }
            Helpers.CreateDataSet<int>(fileId, "/beamlets/id", arrId);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/position_x_mm", arrXPos);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/width_mm", arrXSize);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/position_y_mm", arrYPos);
            Helpers.CreateDataSet<float>(fileId, "/beamlets/height_mm", arrYSize);
            Helpers.CreateDataSet<double>(fileId, "/beamlets/sum_cutoff_value", arrSumOfCutoffValues);
            Helpers.CreateDataSet<int>(fileId, "/beamlets/cutoff_value_cnt", arrNumCutoffValues);

            Hdf5.CloseFile(fileId);
        }
    }
}

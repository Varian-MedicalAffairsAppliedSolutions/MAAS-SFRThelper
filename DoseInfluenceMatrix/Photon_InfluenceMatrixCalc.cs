using System;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Text;
using HDF5CSharp;
using CalculateInfluenceMatrix;


namespace PhotonCalculateInfluenceMatrix
{
    public class Beamlet
    {
        public Beamlet(int idx, string beamId)
        {
            m_iIndex = idx;
            m_szBeamId = beamId;
        }

        public Beamlet(int idx, string beamId, float xStart, float yStart, float xSize, float ySize)
        {
            m_iIndex = idx;
            m_szBeamId = beamId;
            m_fXStart = xStart;
            m_fYStart = yStart;
            m_fXSize = xSize;
            m_fYSize = ySize;
        }

        public int m_iIndex;
        public string m_szBeamId;
        public float m_fXSize;
        public float m_fYSize;
        public float m_fXStart;
        public float m_fYStart;

        public double m_dSumCutoffValues;
        public int m_iNumCutoffValues;
    }

    public class MyBeamParameters
    {
        public MyBeamParameters(VRect<double> jaws, float[,] staticLeafs)
        {
            m_rectJaws = jaws;
            m_arrStaticLeafs = staticLeafs;
            m_lstBeamlets = new List<Beamlet>();
            m_lstBeamletMLCs = new List<float[,]>();
            m_lstBeamletBeam = new List<Beam>();
            m_arrClosedMLCDoseMatrix = null;
        }
        public int BeamletCount
        {
            get { return m_lstBeamlets.Count; }
        }
        public VRect<double> m_rectJaws;
        public float[,] m_arrStaticLeafs;

        public List<Beamlet> m_lstBeamlets; // list of beamlets for this beam
        public List<float[,]> m_lstBeamletMLCs; // list of MLC position for each beamlet
        public float[,] m_ClosedMLC; // closed MLC
        public List<Beam> m_lstBeamletBeam; // beams copied from original beam. size is the same as number of beams to be calculated at a time
        public float[,] m_arrClosedMLCDoseMatrix;
    };

    public static class PhotonInfluenceMatrixCalc
    {
        public static void Calculate(Patient hPatient, Course hCourse, ExternalPlanSetup hPlan, double dInfCutoffValue, bool bExportFullInfMatrix, int iMaxDoseCalcRetry,
            float beamletSizeX, float beamletSizeY, int iNumBeamletsToBeCalcAtATime, string szEclipseVolumeDoseCalcModel, 
            string szCalculationGridSizeInCM, float fDoseScalingFactor, string szOutputRootFolder, DisplayProgress hProgress,
            Func<bool> checkCancellation = null)
        {
            hPatient.BeginModifications();
            int iFieldCnt = hPlan.Beams.Count();

            string resultsDirPath = szOutputRootFolder + $"\\{hPatient.LastName}${hPatient.Id}";
            if (!System.IO.Directory.Exists(resultsDirPath))
            {
                System.IO.Directory.CreateDirectory(resultsDirPath);
            }

            string planResultsPath = resultsDirPath + $"\\{hPlan.Id}";
            if (Directory.Exists(planResultsPath))
            {
                Directory.Delete(planResultsPath, true);
                System.Threading.Thread.Sleep(5000);
            }
            Directory.CreateDirectory(planResultsPath);
            if (hProgress != null)
            {
                hProgress.Message($"Results will be written to: {planResultsPath}");
                hProgress.Message("Exporting structure outlines and masks...");
            }
            ExportStructureOutlinesAndMasks(hPlan, planResultsPath, hProgress, checkCancellation);

            // create beamlets course, plan
            string szCourseId = "Backup";
            Course backupCourse = hPatient.Courses.Where(o => o.Id == szCourseId).SingleOrDefault();
            if (backupCourse == null)
            {
                backupCourse = hPatient.AddCourse();
                backupCourse.Id = szCourseId;
            }

            hPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, szEclipseVolumeDoseCalcModel);
            hPlan.SetCalculationOption(hPlan.GetCalculationModel(CalculationType.PhotonVolumeDose), "CalculationGridSizeInCM", szCalculationGridSizeInCM);
            Dictionary<string, string> dictVals = hPlan.GetCalculationOptions(hPlan.GetCalculationModel(CalculationType.PhotonVolumeDose));//, "CalculationGridSizeInCM", out szVal);

            // calculate number of beamlets for all beams and initialize them
            Dictionary<string, MyBeamParameters> tblBeamParameters = new Dictionary<string, MyBeamParameters>();
            bool bHalcyon=false; //TODO: bHalcyon machines are not supported
            int iMaxBeamletCount = int.MinValue;

            ExternalPlanSetup backupPlan = CopyPlan(hPlan, backupCourse);
            List<Beam> arrRemovedBeams = hPlan.Beams.ToList();
            // make copies of orignal beams
            foreach (Beam hBeam in arrRemovedBeams)
                CopyBeam(hBeam, backupPlan);
            List<Beam> arrOrigBeams = backupPlan.Beams.ToList();

            foreach (Beam hBeam in arrRemovedBeams)
                hPlan.RemoveBeam(hBeam);

            double dSumBeamWeights = 0;
            foreach (Beam origBeam in arrOrigBeams)
                dSumBeamWeights += origBeam.WeightFactor;

            // will be used for dose calculation later
            List<KeyValuePair<string, MetersetValue>> presetValues = new List<KeyValuePair<string, MetersetValue>>();
            foreach (Beam origBeam in arrOrigBeams)
            {
                VRect<double> jaws = GetJawsFromBeam(origBeam);
                float[] arrLeafWidths = GetLeafWidths(origBeam.MLC, bHalcyon);
                int xs = GetBeamletsCountX(jaws, beamletSizeX);
                int ys = GetBeamletsCountY(jaws, beamletSizeY, arrLeafWidths, bHalcyon);

                float[,] arrStaticLeafPositions = origBeam.ControlPoints.First().LeafPositions;
                MyBeamParameters bp = new MyBeamParameters(jaws, arrStaticLeafPositions);
                bp.m_ClosedMLC = GetClosedLeafPositions(arrLeafWidths.Length, jaws);

                int iBeamletIdx = 0;
                VRect<float> beamletSize;
                for (int y = 0; y < ys; y++)
                {
                    for (int x = 0; x < xs; x++)
                    {
                        float[,] leafs;
                        if (!bHalcyon)
                        {
                            leafs = GetLeafPositions(jaws, beamletSizeX, beamletSizeY, arrLeafWidths, x, y, out beamletSize);
                        }
                        else
                        {
                            leafs = GetLeafPositionsHalcyon(origBeam, jaws, beamletSizeX, beamletSizeY, arrLeafWidths, x, y, out beamletSize);
                        }
                        if (IsBeamletInsideAperture(leafs, arrStaticLeafPositions))
                        {
                            bp.m_lstBeamletMLCs.Add(leafs);
                            bp.m_lstBeamlets.Add(new Beamlet(iBeamletIdx, origBeam.Id, beamletSize.X1, beamletSize.Y1, beamletSize.X2 - beamletSize.X1, beamletSize.Y2 - beamletSize.Y1));
                            iBeamletIdx++;
                        }
                    }
                }
                if (iBeamletIdx >= iMaxBeamletCount)
                    iMaxBeamletCount = iBeamletIdx + 1;

                // create beamlet beams
                for (int i = 0; i < iNumBeamletsToBeCalcAtATime; i++)
                {
                    Beam hCopied = CopyBeam(origBeam, hPlan);
                    bp.m_lstBeamletBeam.Add(hCopied);

                    presetValues.Add(new KeyValuePair<string, MetersetValue>(hCopied.Id, new MetersetValue(1, DosimeterUnit.MU)));
                }

                tblBeamParameters[origBeam.Id] = bp;
            }

            string szBeamPath = System.IO.Path.Combine(planResultsPath, "Beams");
            if (!Directory.Exists(szBeamPath))
                Directory.CreateDirectory(szBeamPath);

            int iMaxPointCnt = 0;

            List<string> lstCalcBeams = new List<string>();
            bool bFirstDoseCalc = true;
            float[,] arrFullDoseMatrix = null;
            // lopp thru beamlets
            int iCurrBeamlet = 0;
            bool bFirstCalc = true;
            do
            {
                lstCalcBeams.Clear();
                for (int i = 0; i < iNumBeamletsToBeCalcAtATime; i++)
                {
                    // update MLC to cover this particular beamlet
                    foreach (Beam origBeam in arrOrigBeams)
                    {
                        MyBeamParameters bp = tblBeamParameters[origBeam.Id];
                        if (iCurrBeamlet < bp.BeamletCount)
                        {
                            Beam hBeamletBeam = bp.m_lstBeamletBeam[i];

                            //update MLCs
                            BeamParameters beamParams = hBeamletBeam.GetEditableParameters();
                            if (bFirstCalc && i == 0)
                                beamParams.SetAllLeafPositions(bp.m_ClosedMLC);
                            else
                                beamParams.SetAllLeafPositions(bp.m_lstBeamletMLCs[iCurrBeamlet]);

                            hBeamletBeam.ApplyParameters(beamParams);

                            lstCalcBeams.Add(hBeamletBeam.Id);
                        }
                    }
                    if( !(bFirstCalc && i == 0) )
                        iCurrBeamlet++;
                }
                hProgress.Message($"Progress: Beamlet {iCurrBeamlet}/{iMaxBeamletCount}.");

                if (lstCalcBeams.Count > 0)
                {
                    int iRetryCnt = 0;
                    bool bSuccess = false;
                    do
                    {
                        try
                        {
                            CalculationResult calcRes = hPlan.CalculateDoseWithPresetValues(presetValues);
                            bSuccess = calcRes.Success;
                        }
                        catch (Exception)
                        {
                            bSuccess = false;
                        }
                        iRetryCnt++;

                        if (!bSuccess && iRetryCnt < iMaxDoseCalcRetry)
                            hProgress.Message($"Retry: Beamlet { iCurrBeamlet}/{ iMaxBeamletCount}.");
                    } while (!bSuccess && iRetryCnt < iMaxDoseCalcRetry);

                    if (!bSuccess)
                    {
                        //app.SaveModifications();
                        throw new ApplicationException($"Dose Calculation Failed after {iMaxDoseCalcRetry} attempts");
                    }

                    if (bFirstDoseCalc)
                    {
                        iMaxPointCnt = ExportOptimizationVoxels(hPlan, planResultsPath);
                        bFirstDoseCalc = false;
                    }

                    // extract dose for all beams
                    foreach (Beam b in arrOrigBeams)
                    {
                        MyBeamParameters bp = tblBeamParameters[b.Id];
                        for (int i = 0; i < iNumBeamletsToBeCalcAtATime; i++)
                        {
                            Beam blb = bp.m_lstBeamletBeam[i];
                            if (lstCalcBeams.Contains(blb.Id))
                            {
                                BeamDose hBeamDose = blb.Dose;
                                int iDoseMatrixSize = hBeamDose.ZSize* hBeamDose.YSize* hBeamDose.XSize;

                                if (arrFullDoseMatrix == null)
                                    arrFullDoseMatrix = new float[iDoseMatrixSize, 1];
                                Array.Clear(arrFullDoseMatrix, 0, arrFullDoseMatrix.Length);

                                double dWeight = (blb.WeightFactor / dSumBeamWeights) * blb.MetersetPerGy / 100.0;
                                string szHDF5DataFile = System.IO.Path.Combine(szBeamPath, $"Beam_{b.Id}_Data.h5");

                                DoseData doseData;
                                float[,] arrClosedMLCDoseMatrix = null;
                                if (bFirstCalc && i == 0)
                                {
                                    arrClosedMLCDoseMatrix = new float[iDoseMatrixSize, 1];
                                    doseData = Helpers.GetDosePoints(hBeamDose, dWeight, dInfCutoffValue, ref arrClosedMLCDoseMatrix);
                                    bp.m_arrClosedMLCDoseMatrix = arrClosedMLCDoseMatrix;
                                }
                                else
                                {
                                    doseData = Helpers.GetDosePoints(hBeamDose, dWeight, dInfCutoffValue, ref arrFullDoseMatrix);
                                    // subtract matrix from closedMLC matrix
                                    arrClosedMLCDoseMatrix = bp.m_arrClosedMLCDoseMatrix;
                                    for (int iDosePtIdx = 0; iDosePtIdx < iDoseMatrixSize; iDosePtIdx++)
                                    {
                                        arrFullDoseMatrix[iDosePtIdx, 0] = (arrFullDoseMatrix[iDosePtIdx, 0] - arrClosedMLCDoseMatrix[iDosePtIdx, 0])*fDoseScalingFactor;
                                        if (arrFullDoseMatrix[iDosePtIdx, 0] < 0)
                                            arrFullDoseMatrix[iDosePtIdx, 0] = 0;
                                    }

                                    int iBeamletIdx = iCurrBeamlet - iNumBeamletsToBeCalcAtATime + i;
                                    Beamlet hBeamlet = bp.m_lstBeamlets[iBeamletIdx];
                                    hBeamlet.m_dSumCutoffValues = doseData.m_dSumCutoffValues*fDoseScalingFactor;
                                    hBeamlet.m_iNumCutoffValues = doseData.m_iNumCutoffValues;

                                    bool bAddLastEntry = (iBeamletIdx == bp.BeamletCount - 1);
                                    Helpers.WriteInfMatrixHDF5(bExportFullInfMatrix, arrFullDoseMatrix, doseData, bAddLastEntry, iMaxPointCnt, iBeamletIdx, fDoseScalingFactor, szHDF5DataFile);
                                }
                            }
                        }
                    }
                    bFirstCalc = false;
                }

                if (checkCancellation != null && checkCancellation())
                {
                    hProgress?.Message("Calculation cancelled by user.");
                    return;
                }
            } while (iCurrBeamlet<iMaxBeamletCount);

            // export beam meta data
            foreach (Beam b in arrOrigBeams)
            {
                hProgress.Message($"Progress: Finalizing beam {b.Id}.");

                string szHDF5DataFile = System.IO.Path.Combine(szBeamPath, $"Beam_{b.Id}_Data.h5");
                Helpers.WriteBeamletInfoHDF5(tblBeamParameters[b.Id], szHDF5DataFile);

                string szBeamMetaDataFile = System.IO.Path.Combine(szBeamPath, $"Beam_{b.Id}_MetaData.json");
                Helpers.WriteBeamMetaData(b, tblBeamParameters[b.Id], dInfCutoffValue, szBeamMetaDataFile);
            }
            hProgress.Message("Influence matrix calculation finished.");
        }

        public static int ExportOptimizationVoxels(ExternalPlanSetup hPlanSetup, string szOutputFolder)
        {
            if (!Directory.Exists(szOutputFolder))
            {
                Directory.CreateDirectory(szOutputFolder);
            }

            PlanningItemDose hPlanDose = hPlanSetup.Dose;
            int iXSize = hPlanDose.XSize;
            int iYSize = hPlanDose.YSize;
            int iZSize = hPlanDose.ZSize;
            int iPtCnt = iXSize * iYSize * iZSize;
            VVector vOrigin = hPlanDose.Origin;
            double dXRes = hPlanDose.XRes;
            double dYRes = hPlanDose.YRes;
            double dZRes = hPlanDose.ZRes;
            float[,] npPtCoords = new float[iPtCnt, 3];
            float[] npPtWeights = new float[iPtCnt];
            float fZ, fY, fX;
            int i = 0;
            for(int z=0; z<iZSize; z++)
            {
                fZ = (float)(vOrigin.z + z*dZRes);
                for (int y = 0; y < iYSize; y++)
                {
                    fY = (float)(vOrigin.y + y*dYRes);
                    for (int x = 0; x < iXSize; x++)
                    {
                        fX = (float)(vOrigin.x + x*dXRes);

                        npPtCoords[i, 0] = fX;
                        npPtCoords[i, 1] = fY;
                        npPtCoords[i, 2] = fZ;
                        npPtWeights[i] = 1;
                        i++;
                    }
                }
            }

            string szDataFilename = "OptimizationVoxels_Data.h5";
            string szH5Path = System.IO.Path.Combine(szOutputFolder, szDataFilename);
            long hf = Hdf5.CreateFile(szH5Path);
            Helpers.CreateDataSet<float>(hf, "/voxel_coordinate_XYZ_mm", npPtCoords);
            Helpers.CreateDataSet<float>(hf, "/voxel_weight_mm3", npPtWeights);
            Hdf5.CloseFile(hf);

            // Save meta data
            Image hCT = hPlanSetup.StructureSet.Image;
            VVector vCTOrigin = hCT.Origin;

            var dctMetaData = new
            {
                ct_origin_xyz_mm = new[] { vCTOrigin.x, vCTOrigin.y, vCTOrigin.z },
                ct_voxel_resolution_xyz_mm = new[] { hCT.XRes, hCT.YRes, hCT.ZRes },
                dose_voxel_resolution_xyz_mm = new double[] { dXRes, dYRes, dZRes },
                ct_size_xyz = new[] { hCT.XSize, hCT.YSize, hCT.ZSize },
                cal_box_xyz_start = new[] { vOrigin.x, vOrigin.y, vOrigin.z },
                cal_box_xyz_end = new[] { vOrigin.x + dXRes*iXSize, vOrigin.y + dYRes * iYSize, vOrigin.z + dZRes * iZSize},
                ct_to_dose_voxel_map_File = $"{szDataFilename}/ct_to_dose_voxel_map",
                voxel_coordinate_XYZ_mm_File = $"{szDataFilename}/voxel_coordinate_XYZ_mm",
                opt_point_cnt = iPtCnt
            };

            string szMetaDataFile = System.IO.Path.Combine(szOutputFolder, "OptimizationVoxels_MetaData.json");
            CalculateInfluenceMatrix.Helpers.WriteJSONFile(dctMetaData, szMetaDataFile);
            return iPtCnt;
        }

        public static void ExportStructureOutlinesAndMasks(ExternalPlanSetup hPlanSetup, string szOutputFolder, DisplayProgress hProgress, Func<bool> checkCancellation = null)
        {
            string szStructOutlinesFolder = System.IO.Path.Combine(szOutputFolder, "Beams");

            if (!Directory.Exists(szStructOutlinesFolder))
            {
                Directory.CreateDirectory(szStructOutlinesFolder);
            }

            // Export structure outlines
            foreach (Beam b in hPlanSetup.Beams)
            {
                if (b.IsSetupField)
                {
                    continue;
                }

                string szH5OutlinesPath = System.IO.Path.Combine(szStructOutlinesFolder, $"Beam_{b.Id}_Data.h5");
                long fileId1 = Hdf5.CreateFile(szH5OutlinesPath);

                foreach (Structure s in hPlanSetup.StructureSet.Structures)
                {
                    try
                    {
                        Point[][] arrOutlines = b.GetStructureOutlines(s, true);
                        if (arrOutlines != null && arrOutlines.Length > 0)
                        {
                            for (int j = 0; j < arrOutlines.Length; j++)
                            {
                                Point[] points = arrOutlines[j];
                                string szDatasetName = $"/BEV_structure_contour_points/{s.Id}/Segment-{j}";
                                double[,] arrPoints = new double[points.Length, 2];
                                for (int i = 0; i < points.Length; i++)
                                {
                                    arrPoints[i, 0] = points[i].X;
                                    arrPoints[i, 1] = points[i].Y;
                                }
                                Helpers.CreateDataSet<double>(fileId1, szDatasetName, arrPoints);
                            }
                        }
                    }
                    catch (Exception) {}

                    if (checkCancellation != null && checkCancellation())
                    {
                        hProgress?.Message("Calculation cancelled by user.");
                        return;
                    }
                }
                Hdf5.CloseFile(fileId1);
            }

            // Export structure masks
            string szH5MaskPath = System.IO.Path.Combine(szOutputFolder, "StructureSet_Data.h5");
            long fileId = Hdf5.CreateFile(szH5MaskPath);
            List<object> lstAllStructsMetaData = new List<object>();

            Image hCT = hPlanSetup.StructureSet.Image;
            foreach (Structure s in hPlanSetup.StructureSet.Structures)
            {
                try
                {
                    if (s.HasSegment)
                    {
                        string szStructID = s.Id;
                        string szStandardStructName = szStructID; 

                        byte[,,] struct3DMask = Transpose<byte>(MakeSegmentMaskForStructure(hCT, s));
                        Helpers.CreateDataSet<byte>(fileId, "/" + szStructID, struct3DMask);

                        lstAllStructsMetaData.Add(new
                        {
                            name = szStandardStructName,
                            volume_cc = s.Volume,
                            dicom_structure_name = szStructID,
                            fraction_of_vol_in_calc_box = 1, 
                            structure_mask_3d_File = $"StructureSet_Data.h5/{szStandardStructName}"
                        });
                    }
                }
                catch (Exception) { }

                if (checkCancellation != null && checkCancellation())
                {
                    hProgress?.Message("Calculation cancelled by user.");
                    return;
                }
            }
            Hdf5.CloseFile(fileId);

            string szMetaDataFile = System.IO.Path.Combine(szOutputFolder, "StructureSet_MetaData.json");
            CalculateInfluenceMatrix.Helpers.WriteJSONFile(lstAllStructsMetaData, szMetaDataFile);
        }
        public static byte[,,] MakeSegmentMaskForStructure(Image hCT, Structure hStruct)
        {
            if (hStruct.HasSegment)
            {
                System.Collections.BitArray pre_buffer = new System.Collections.BitArray(hCT.ZSize);
                return fill_in_profiles(hCT, hStruct, pre_buffer);
            }
            else
                throw new Exception("Structure has no segment data");
        }
        public static T[,,] Transpose<T>(T[,,] arrInput) where T : struct
        {
            int iXSize = arrInput.GetLength(2);
            int iYSize = arrInput.GetLength(1);
            int iZSize = arrInput.GetLength(0);

            T[,,] transposed = new T[iXSize, iYSize, iZSize];

            for (int z = 0; z < iZSize; z++)
            {
                for (int y = 0; y < iYSize; y++)
                {
                    for (int x = 0; x < iXSize; x++)
                    {
                        transposed[x, y, z] = arrInput[z, y, x];
                    }
                }
            }

            return transposed;
        }

        private static byte[,,] fill_in_profiles(Image hCT, Structure hStruct, System.Collections.BitArray pre_buffer) // dose_or_image, profile_fxn, row_buffer, dtype, pre_buffer= None)
        {
            int iXSize = hCT.XSize;
            int iYSize = hCT.YSize;
            int iZSize = hCT.ZSize;
            byte[,,] mask_array = new byte[iXSize, iYSize, iZSize];

            VVector z_direction = ((iZSize - 1) * hCT.ZRes) * hCT.ZDirection;
            VVector y_step = hCT.YRes * hCT.YDirection;

            VVector start_x, stop;
            for (int x = 0; x < iXSize; x++)    //) :  # scan X dimensions
            {
                start_x = hCT.Origin + ((x * hCT.XRes) * hCT.XDirection);

                for (int y = 0; y < iYSize; y++)  // # scan Y dimension
                {
                    stop = start_x + z_direction;

                    hStruct.GetSegmentProfile(start_x, stop, pre_buffer);

                    for (int z = 0; z < iZSize; z++)
                        mask_array[x, y, z] = (byte)(pre_buffer[z] ? 1 : 0);

                    start_x = start_x + y_step;
                }
            }
            return mask_array;
        }
        ///////////////////////////////////////////////////////////////////
        ///
        static float[] leafWidthsMillennium120 = new float[60]
        {
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10
        };

        static float[] leafWidthsHD120 = new float[60]
        {
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F, 2.5F,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5
        };

        static float[] leafWidthsHalcyon = new float[56]
        {
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 5, 5
        };

        public static int GetLeafStartIndex(VRect<double> jaws, float beamletSizeY, float[] leafWidths, bool halcyon)
        {
            double sum = 0;
            double leafsum = 0;
            int mid = halcyon ? 27 : 29;
            for (int i = mid; i >= 0; i--) // start from midway downwards
            {
                sum += leafWidths[i];
                leafsum += leafWidths[i];
                if (leafsum >= beamletSizeY)
                {
                    leafsum = 0;
                }
                if (sum >= -jaws.Y1 + leafsum)
                {
                    return i;
                }
            }
            throw new Exception("Y jaws too wide");
        }

        // calculate stop leaf index from jaw position
        public static int GetLeafStopIndex(VRect<double> jaws, float beamletSizeY, float[] leafWidths, bool halcyon)
        {
            double sum = 0;
            double leafsum = 0;
            int mid = halcyon ? 28 : 30;
            int end = halcyon ? 56 : 60;
            for (int i = mid; i < end; i++) // start from midway upwards
            {
                sum += leafWidths[i];
                leafsum += leafWidths[i];
                if (leafsum >= beamletSizeY)
                {
                    leafsum = 0;
                }
                if (sum >= jaws.Y2 + leafsum)
                {
                    return i;
                }
            }
            throw new Exception("Y jaws too wide");
        }
        public static int GetBeamletsCountX(VRect<double> jaws, float beamletSizeX)
        {
            int xs = (int)Math.Ceiling(-jaws.X1 / beamletSizeX) + (int)Math.Ceiling(jaws.X2 / beamletSizeX);
            return xs;
        }
        public static int GetBeamletsCountY(VRect<double> jaws, float beamletSizeY, float[] leafWidths, bool halcyon)
        {
            double sum = 0;
            int counter = 0;
            int startIndex = GetLeafStartIndex(jaws, beamletSizeY, leafWidths, halcyon);
            int stopIndex = GetLeafStopIndex(jaws, beamletSizeY, leafWidths, halcyon);
            for (int i = startIndex; i <= stopIndex; i++)
            {
                double width = leafWidths[i];
                sum += width;
                if (sum >= beamletSizeY)
                {
                    counter++;
                    sum = 0;
                }
            }
            return counter;
        }
        public static float[] GetLeafWidths(MLC mlc, bool halcyon)
        {
            float[] leafWidths;
            string szMLCModel = mlc.Model;
            if (halcyon) //(mlc.IsSX())
            {
                leafWidths = leafWidthsHalcyon;
            }
            else if (szMLCModel == "Millennium 120")
            {
                leafWidths = leafWidthsMillennium120;
            }
            else if (szMLCModel == "Varian High Definition 120")
            {
                leafWidths = leafWidthsHD120;
            }
            else
            {
                throw new Exception("unsupported MLC model");
            }

            return leafWidths;
        }
        public static VRect<double> GetJawsFromBeam(Beam beam)
        {
            if (beam != null)
            {
                double x1 = beam.ControlPoints.Min(cp => cp.JawPositions.X1); //mm
                double x2 = beam.ControlPoints.Max(cp => cp.JawPositions.X2);
                double y1 = beam.ControlPoints.Min(cp => cp.JawPositions.Y1);
                double y2 = beam.ControlPoints.Max(cp => cp.JawPositions.Y2);
                VRect<double> jaws = new VRect<double>(x1, y1, x2, y2);
                return jaws;
            }
            else
            {
                VRect<double> jaws = new VRect<double>(0, 0, 0, 0);
                return jaws;
            }
        }
        public static float[,] GetClosedLeafPositions(int nLeafs, VRect<double> jaws)
        {
            float[,] positions = new float[2, nLeafs];
            float parkPos = (float)jaws.X1 - 1;

            // loop through all leafs
            for (int i = 0; i < nLeafs; i++)
            {
                positions[0, i] = parkPos;
                positions[1, i] = parkPos;
            }
            return positions;
        }

        public static float[,] GetLeafPositions(VRect<double> jaws, float beamletSizeX, float beamletSizeY, float[] leafWidths, int x, int y, out VRect<float> beamletSize)
        {
            int nLeafs = leafWidths.Length;
            float[,] positions = new float[2, nLeafs];

            // bl.start_x & end_x
            float xPosLeft = (float)(beamletSizeX * (float)Math.Floor(jaws.X1 / beamletSizeX) + beamletSizeX * x); // align grid with isocenter
            float xPosRight = xPosLeft + beamletSizeX;

            int leafStart = GetLeafStartIndex(jaws, beamletSizeY, leafWidths, false);

            // find start index of leaf that covers this beamlet
            for (int yi = 0; yi < y; yi++)
            {
                double sum = 0;
                for (int i = leafStart; i < nLeafs; i++)
                {
                    double width = leafWidths[i];
                    sum += width;
                    leafStart++;
                    if (sum >= beamletSizeY)
                    {
                        break;
                    }
                }
            }

            float gapWidth = 0, yStart = 0;
            float parkPos = (float)jaws.X1 - 1;

            float fMLCHalfWidth = 0.0f;
            for (int i = 0; i < nLeafs / 2; i++)
                fMLCHalfWidth += leafWidths[i];
            float fCurrLeafPosY = -fMLCHalfWidth;
            // loop through all leafs
            for (int i = 0; i < nLeafs; i++)
            {
                if (i < leafStart) //below beamlet
                {
                    positions[0, i] = parkPos;
                    positions[1, i] = parkPos;
                }
                else if (gapWidth < beamletSizeY) // leaves that cover beamlet
                {
                    if (yStart == 0)
                        yStart = fCurrLeafPosY; // half field in mm

                    positions[0, i] = xPosLeft;
                    positions[1, i] = xPosRight;
                    gapWidth += leafWidths[i];
                }
                else // above beamlet
                {
                    positions[0, i] = parkPos;
                    positions[1, i] = parkPos;
                }
                fCurrLeafPosY += leafWidths[i];
            }
            beamletSize = new VRect<float>(xPosLeft, yStart, xPosRight, yStart + gapWidth);

            return positions;
        }
        public static float[,] GetLeafPositionsHalcyon(Beam beam, VRect<double> jaws, float beamletSizeX, float beamletSizeY, float[] leafWidths, int x, int y, out VRect<float> beamletSize)
        {
            int nLeafs = leafWidths.Length; // Halcyon can make 56 beamlets in y-direction (one less than the number of leaf pairs)

            float xPosLeft = beamletSizeX * (float)Math.Floor(jaws.X1 / beamletSizeX) + beamletSizeX * x; // align grid with isocenter
            float xPosRight = xPosLeft + beamletSizeX;

            int leafStart = GetLeafStartIndex(jaws, beamletSizeY, leafWidths, true);

            for (int yi = 0; yi < y; yi++)
            {
                double sum = 0;
                for (int i = leafStart; i < nLeafs; i++)
                {
                    double width = leafWidths[i];
                    sum += width;
                    leafStart++;
                    if (sum >= beamletSizeY)
                    {
                        break;
                    }
                }
            }

            float gapWidth = 0;
            float yPosDown = 0, yPosTop = 0;

            float fMLCHalfWidth = 0.0f;
            for (int i = 0; i < nLeafs / 2; i++)
                fMLCHalfWidth += leafWidths[i];
            float fCurrLeafPosY = -fMLCHalfWidth;
            for (int i = 0; i < nLeafs; i++)
            {
                if (i < leafStart)
                {
                }
                else if (gapWidth < beamletSizeY)
                {
                    if (yPosDown == 0)
                    {
                        yPosDown = fCurrLeafPosY;
                    }
                    gapWidth += leafWidths[i];
                }
                else
                {
                    yPosTop = yPosDown + gapWidth;
                    break;
                }
                fCurrLeafPosY += leafWidths[i];
            }
            beamletSize = new VRect<float>(xPosLeft, yPosDown, xPosRight, yPosDown + gapWidth);
            //TODO
            throw new NotImplementedException();

            float[,] positions = null; // NAperture.MLC.Halcyon.CreateMLCApertureFromCollJawPositions(beam, xPosLeft * 0.1f, xPosRight * 0.1f, yPosDown * 0.1f, yPosTop * 0.1f);
            return positions;
        }
        public static bool IsBeamletInsideAperture(float[,] leafs, float[,] staticAperture)
        {
            int nLeafs = leafs.GetLength(1); // halcyon ? 57 : 60; // here we're dealing with all real leaf pairs.
            float fTol = 0.01f;
            for (int i = 0; i < nLeafs; i++)
            {
                //NMLC.LeafPositionPair pair = leafs.GetLeafPair(i);
                //if (!pair.IsClosed())
                if(Math.Abs(leafs[0,i]-leafs[1, i])>fTol)
                {
                    //NMLC.LeafPositionPair staticPair = staticAperture.GetLeafPair(i);
                    //if (!staticPair.IsClosed())
                    if (Math.Abs(staticAperture[0, i] - staticAperture[1, i]) > fTol)
                    {
                        if (leafs[0, i] < staticAperture[1, i] && leafs[1, i] > staticAperture[0, i])
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        public static ExternalPlanSetup CopyPlan(ExternalPlanSetup plansetup, Course course)
        {
            StructureSet structureset = plansetup.StructureSet;
            ExternalPlanSetup copyPlan = course.AddExternalPlanSetup(structureset);
            int? fractions = plansetup.NumberOfFractions;
            double prescribedPercentage = plansetup.TreatmentPercentage;
            DoseValue fractiondose = plansetup.DosePerFraction;
            if (fractions != null && !Double.IsNaN(fractiondose.Dose))
            {
                copyPlan.SetPrescription(fractions.Value, fractiondose, prescribedPercentage);
            }

            return copyPlan;
        }

        public static Beam CopyBeam(Beam beam, ExternalPlanSetup plansetup)
        {
            if (!beam.IsSetupField)
            {
                string energyModeDisp = beam.EnergyModeDisplayName;
                Char[] sep = { '-' };
                string energyMode = energyModeDisp.Split(sep).First();
                string pfm = energyModeDisp.Split(sep).Count() > 1 ? energyModeDisp.Split(sep).Last() : null;
                ExternalBeamMachineParameters extParams = new ExternalBeamMachineParameters(beam.TreatmentUnit.Id, energyMode, beam.DoseRate, beam.Technique.Id, pfm);
                Beam copyBeam;
                //if there is no MLC, or this is for the second loop and there is an optimal fluence
                if (beam.MLC == null || beam.GetOptimalFluence() != null)
                    copyBeam = plansetup.AddStaticBeam(extParams, GetJawsFromBeam(beam), beam.ControlPoints[0].CollimatorAngle, beam.ControlPoints[0].GantryAngle, beam.ControlPoints[0].PatientSupportAngle, beam.IsocenterPosition);
                else
                    copyBeam = plansetup.AddMLCBeam(extParams, beam.ControlPoints[0].LeafPositions, GetJawsFromBeam(beam), beam.ControlPoints[0].CollimatorAngle, beam.ControlPoints[0].GantryAngle, beam.ControlPoints[0].PatientSupportAngle, beam.IsocenterPosition);

                copyBeam.Id = GetValidObjectId(copyBeam, beam.Id);

                return copyBeam;
            }
            else
                return null;
        }
        public static string GetValidObjectId(VMS.TPS.Common.Model.API.ApiDataObject hObj, string szID, int iMaxLength = 13)
        {
            string szOrigID = szID;
            int iStartIdx = 0;
            int iPos = szOrigID.LastIndexOf('_');
            if (iPos != -1)
            {
                if (int.TryParse(szOrigID.Substring(iPos + 1), out iStartIdx))
                    szOrigID = szOrigID.Substring(0, iPos);
            }
            string szNewID = szOrigID;
            while (!VMS.TPS.Common.Model.API.TypeBasedIdValidator.IsValidId(szNewID, hObj, new StringBuilder()))
            {
                iStartIdx++;

                string szSuffix = "_" + iStartIdx.ToString();
                int iLen = iMaxLength - szSuffix.Length;
                if (iLen > szOrigID.Length)
                    iLen = szOrigID.Length;

                szNewID = szOrigID.Substring(0, iLen) + szSuffix;
            }
            return szNewID;
        }
    }
}

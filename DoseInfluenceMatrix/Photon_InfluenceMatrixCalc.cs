using System;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Text;
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

        public Beamlet(int idx, string beamId, float xStart, float yStart, float xSize, float ySize, int gridX, int gridY)
        {
            m_iIndex = idx;
            m_szBeamId = beamId;
            m_fXStart = xStart;
            m_fYStart = yStart;
            m_fXSize = xSize;
            m_fYSize = ySize;
            m_iGridX = gridX;
            m_iGridY = gridY;
        }

        public int m_iIndex;
        public string m_szBeamId;
        public float m_fXSize;
        public float m_fYSize;
        public float m_fXStart;
        public float m_fYStart;

        // SFRThelper patch 9: integer indices on the enumeration grid,
        // recorded at creation time. The SetOptimalFluence round trip maps
        // matrix columns to fluence-image pixels through these; deriving
        // them later from mm positions invites boundary rounding errors.
        public int m_iGridX;
        public int m_iGridY;

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
            m_lstCsrIndPtr = new List<int> { 0 };
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
        // SFRThelper storage patch: cumulative nonzero count per beamlet
        // (the CSR indptr), written to the H5 at beam finalization.
        public List<int> m_lstCsrIndPtr;
    };

    public static class PhotonInfluenceMatrixCalc
    {
        // SFRThelper patch 14 (supersedes patch 3's normalization pin): the
        // scratch plan now reads out in absolute Gy, so the only conversion
        // left is dividing by the meterset each beamlet was calculated with.
        // This constant is that meterset, and it is also the value preset on
        // every slot beam - one constant, so the two can never drift.
        // PRESET_DOSE_NORMALIZATION (100.0) and the /(MetersetPerGy/100)
        // formula are retired: they de-normalized the relative(%)-presentation
        // readout, measured at first light (2026-07-23 run) to produce
        // exactly 1e4 x Gy/MU. Measurement #1 in the ledger.
        public const double PRESET_METERSET_MU = 1.0;

        // The name tag we put on temporary working-copy plans. Shared with
        // the eligibility check so the app can refuse to run on a leftover
        // working copy (a "copy of a copy" run, seen 2026-07-23).
        public const string SCRATCH_PLAN_PREFIX = "zD_";

        // SFRThelper: single source of truth for the run-folder convention
        // (<root>\<LastName>$<PatientId>\<PlanId>). The app's Inspect Output
        // calls this too, so the writer and the inspector can never drift.
        public static string GetPlanResultsPath(string szOutputRootFolder, Patient hPatient, ExternalPlanSetup hPlan)
        {
            return System.IO.Path.Combine(szOutputRootFolder, hPatient.LastName + "$" + hPatient.Id, hPlan.Id);
        }

        // SFRThelper: every run writes into its own timestamped subfolder.
        // Runs can no longer clobber previous outputs (a measurement run on
        // 2026-07-23 silently destroyed the keeper matrices by reusing the
        // plan folder). The app captures this path at launch and passes it
        // in, so writer and inspector agree on the exact folder.
        public static string CreateRunFolderPath(string szOutputRootFolder, Patient hPatient, ExternalPlanSetup hPlan)
        {
            return System.IO.Path.Combine(GetPlanResultsPath(szOutputRootFolder, hPatient, hPlan),
                "run_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        public static void Calculate(Patient hPatient, Course hCourse, ExternalPlanSetup hPlan, double dInfCutoffValue, bool bExportFullInfMatrix, int iMaxDoseCalcRetry,
            float beamletSizeX, float beamletSizeY, string szTargetStructureId, float fTargetMarginMM, int iNumBeamletsToBeCalcAtATime, string szEclipseVolumeDoseCalcModel,
            string szCalculationGridSizeInCM, float fDoseScalingFactor, string szOutputRootFolder, DisplayProgress hProgress,
            Func<bool> checkCancellation = null, string szOverrideMachine = null, string szOverrideEnergy = null, string szRunFolder = null,
            bool bExportReferenceDose = false)
        {
            int iFieldCnt = hPlan.Beams.Count();

            // SFRThelper: unique timestamped run folder (see
            // CreateRunFolderPath). The old delete-and-recreate of the plan
            // folder is gone - it destroyed previous runs' outputs.
            string planResultsPath = szRunFolder ?? CreateRunFolderPath(szOutputRootFolder, hPatient, hPlan);
            Directory.CreateDirectory(planResultsPath);
            if (hProgress != null)
            {
                hProgress.Message($"Results will be written to: {planResultsPath}");
                hProgress.Message("Exporting structure outlines and masks...");
            }
            ExportStructureOutlinesAndMasks(hPlan, planResultsPath, hProgress, checkCancellation);
            // SFRThelper patch 12: the export above returns early on
            // cancellation; without this check the run would proceed to
            // create the scratch plan and start dose calculation anyway.
            if (checkCancellation != null && checkCancellation())
                return;

            // SFRThelper patch 2: beamlet enumeration is pruned against the
            // target structure's BEV envelope (plus margin), not against the
            // current MLC pose - the current lattice must not cage the
            // placement search. A null/empty target id means no pruning:
            // every beamlet inside the jaws is extracted.
            Structure hTargetStruct = null;
            if (!string.IsNullOrEmpty(szTargetStructureId))
            {
                hTargetStruct = hPlan.StructureSet.Structures.Where(s => s.Id == szTargetStructureId).SingleOrDefault();
                if (hTargetStruct == null)
                    throw new ApplicationException(
                        $"Target structure '{szTargetStructureId}' not found in structure set '{hPlan.StructureSet.Id}'.");
            }

            // SFRThelper patch 2 housekeeping: write access opens only when
            // writing is about to begin; everything above is read-only.
            hPatient.BeginModifications();

            // SFRThelper patch 1b (scratch-plan pattern): the clinical plan is
            // read-only from here on. All beamlet slots and dose calculations
            // live in a scratch plan inside a dedicated course; the upstream
            // "Backup" machinery is gone because nothing clinical is modified,
            // so nothing needs backing up.
            string szCourseId = "zDIJ";
            Course dijCourse = hPatient.Courses.Where(o => o.Id == szCourseId).SingleOrDefault();
            if (dijCourse == null)
            {
                dijCourse = hPatient.AddCourse();
                dijCourse.Id = szCourseId;
            }

            // One scratch plan per source plan (Eclipse Id limit: 13 chars).
            // A leftover scratch plan from a previous run is replaced;
            // checkpoint-based resume arrives with patch 10.
            string szScratchPlanId = SCRATCH_PLAN_PREFIX + hPlan.Id;
            if (szScratchPlanId.Length > 13)
                szScratchPlanId = szScratchPlanId.Substring(0, 13);
            ExternalPlanSetup existingScratch = dijCourse.ExternalPlanSetups.Where(p => p.Id == szScratchPlanId).SingleOrDefault();
            if (existingScratch != null)
            {
                hProgress?.Message($"Removing scratch plan '{szScratchPlanId}' left by a previous run.");
                dijCourse.RemovePlanSetup(existingScratch);
            }

            ExternalPlanSetup scratchPlan = CopyPlan(hPlan, dijCourse);
            scratchPlan.Id = GetValidObjectId(scratchPlan, szScratchPlanId);
            hProgress?.Message($"Scratch plan '{scratchPlan.Id}' created in course '{szCourseId}'; the clinical plan will not be modified.");

            // SFRThelper patch 13a (provenance): record which prescription the
            // scratch plan carries. The matrix is per-MU physics and does not
            // depend on the prescription magnitude; the prescription's only
            // job is to make Eclipse's absolute-dose presentation defined so
            // VoxelToDoseValue returns numbers instead of NaN. A source plan
            // without a prescription (e.g. a bare test/phantom plan) gets a
            // nominal one on the scratch plan inside CopyPlan.
            bool bSourceHasPrescription = (hPlan.NumberOfFractions != null && !Double.IsNaN(hPlan.DosePerFraction.Dose));
            string szPrescriptionNote;
            if (bSourceHasPrescription)
            {
                szPrescriptionNote = $"copied from source plan: {hPlan.NumberOfFractions} x " +
                    $"{hPlan.DosePerFraction.Dose} {hPlan.DosePerFraction.UnitAsString}";
            }
            else
            {
                szPrescriptionNote = "nominal 1 fx x 1.0 Gy (source plan has no prescription; " +
                    "applied to scratch plan only, matrix values unaffected)";
                hProgress?.Message("Source plan has NO prescription. A nominal 1 fx x 1 Gy prescription was applied " +
                    "to the scratch plan so absolute dose readout is defined (without one, VoxelToDoseValue returns NaN " +
                    "and every matrix entry silently becomes zero). Matrix values are dose-per-MU physics and are " +
                    "unaffected by the prescription magnitude. The scratch plan also uses no plan normalization, " +
                    "so readouts are unnormalized absolute dose.");
            }

            // SFRThelper patch 14: force absolute dose presentation on the
            // scratch plan so every readout is in physical units (Gy).
            // Relative presentation is a per-beam shape normalized to the
            // reference-point dose; the retired %-formula existed to undo
            // that. Each readout still verifies the unit it actually got
            // (see the patch 14 guard in the readout loop).
            scratchPlan.DoseValuePresentation = DoseValuePresentation.Absolute;
            if (!string.IsNullOrWhiteSpace(szOverrideMachine) || !string.IsNullOrWhiteSpace(szOverrideEnergy))
                hProgress?.Message($"MACHINE OVERRIDE: scratch beams use machine='" +
                    (string.IsNullOrWhiteSpace(szOverrideMachine) ? "(source)" : szOverrideMachine) +
                    "', energy='" + (string.IsNullOrWhiteSpace(szOverrideEnergy) ? "(source)" : szOverrideEnergy) +
                    "'. The influence matrix will reflect THIS machine's beam model, not the source plan's.");

            // SFRThelper patch 12: from here to the end of the run, every exit
            // (completion, cancellation, exception) passes through the finally
            // below, which reports the true outcome. No catch: exceptions
            // propagate to the caller. The scratch plan is deliberately kept
            // on abnormal exits (resume support).
            bool bCompleted = false;
            try
            {

                scratchPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, szEclipseVolumeDoseCalcModel);
                scratchPlan.SetCalculationOption(scratchPlan.GetCalculationModel(CalculationType.PhotonVolumeDose), "CalculationGridSizeInCM", szCalculationGridSizeInCM);
                Dictionary<string, string> dictVals = scratchPlan.GetCalculationOptions(scratchPlan.GetCalculationModel(CalculationType.PhotonVolumeDose));//, "CalculationGridSizeInCM", out szVal);

                // SFRThelper patch 16 (reference dose export): the "answer
                // key" for checking the matrix. If the checkbox is on, we
                // briefly copy each real treatment field into the scratch
                // plan exactly as it is, ask Eclipse to compute its dose in
                // one shot, save that dose next to the matrix files, and
                // then delete the copies again. Everything after this point
                // runs exactly as if this step never happened. Later, a
                // checking step will compare "add up all the small pieces"
                // against this answer key to see how close they are.
                if (bExportReferenceDose)
                {
                    ExportReferenceDoses(hPlan, scratchPlan, planResultsPath,
                        szOverrideMachine, szOverrideEnergy, hProgress);
                }

                // calculate number of beamlets for all beams and initialize them
                Dictionary<string, MyBeamParameters> tblBeamParameters = new Dictionary<string, MyBeamParameters>();
                bool bHalcyon = false; //TODO: bHalcyon machines are not supported
                int iMaxBeamletCount = int.MinValue;

                // The clinical treatment fields themselves are the read-only
                // geometry source (setup fields excluded - upstream excluded them
                // implicitly because CopyBeam refuses setup fields).
                List<Beam> arrOrigBeams = hPlan.Beams.Where(b => !b.IsSetupField).ToList();


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

                    // SFRThelper patch 2: target envelope for this beam's BEV,
                    // computed once; null means keep everything in the jaws.
                    List<VRect<float>> lstEnvelopeBoxes = null;
                    if (hTargetStruct != null)
                        lstEnvelopeBoxes = GetTargetEnvelopeBoxes(origBeam, hTargetStruct, fTargetMarginMM);

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
                            // SFRThelper patch 2: keep-test against the target
                            // envelope; the MLC pose no longer steers enumeration.
                            if (IsBeamletInAnyEnvelopeBox(beamletSize, lstEnvelopeBoxes))
                            {
                                bp.m_lstBeamletMLCs.Add(leafs);
                                bp.m_lstBeamlets.Add(new Beamlet(iBeamletIdx, origBeam.Id, beamletSize.X1, beamletSize.Y1, beamletSize.X2 - beamletSize.X1, beamletSize.Y2 - beamletSize.Y1, x, y));
                                iBeamletIdx++;
                            }
                        }
                    }
                    if (iBeamletIdx >= iMaxBeamletCount)
                        iMaxBeamletCount = iBeamletIdx + 1;

                    hProgress?.Message($"Field {origBeam.Id}: {bp.BeamletCount} beamlets kept" +
                        (hTargetStruct != null ? " inside target envelope" : " (no target - whole field)") +
                        $"; creating {iNumBeamletsToBeCalcAtATime} slot beams...");

                    // create beamlet beams
                    for (int i = 0; i < iNumBeamletsToBeCalcAtATime; i++)
                    {
                        Beam hCopied = CopyBeam(origBeam, scratchPlan, szOverrideMachine, szOverrideEnergy);
                        bp.m_lstBeamletBeam.Add(hCopied);

                        // SFRThelper patch 14: preset meterset comes from the
                        // same constant the per-MU division uses.
                        presetValues.Add(new KeyValuePair<string, MetersetValue>(hCopied.Id, new MetersetValue(PRESET_METERSET_MU, DosimeterUnit.MU)));
                    }

                    tblBeamParameters[origBeam.Id] = bp;
                }

                string szBeamPath = System.IO.Path.Combine(planResultsPath, "Beams");
                if (!Directory.Exists(szBeamPath))
                    Directory.CreateDirectory(szBeamPath);

                int iMaxPointCnt = 0;

                List<string> lstCalcBeams = new List<string>();
                bool bFirstDoseCalc = true;
                // SFRThelper patch 14.1: one-shot flag for the presentation
                // readback diagnostic logged on the first post-calc re-assert.
                bool bPresentationReadbackLogged = false;
                // SFRThelper patch 14.2: readout mode determined at the first
                // unit probe, logged once, and stamped into beam metadata.
                string szReadoutModeNote = null;
                bool bRelativeReadoutMode = false;
                float[,] arrFullDoseMatrix = null;
                // lopp thru beamlets
                int iCurrBeamlet = 0;
                bool bFirstCalc = true;
                hProgress?.Message($"Starting dose calculations: up to {iMaxBeamletCount} beamlet indices per field, " +
                    $"batch size {iNumBeamletsToBeCalcAtATime}. The first batch is the closed-MLC leakage baseline. " +
                    "Each dose calculation runs for minutes with no output - the window may freeze during computation; " +
                    "Cancel takes effect at the next batch boundary.");
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
                                float[,] arrLeavesToApply = (bFirstCalc && i == 0)
                                    ? bp.m_ClosedMLC
                                    : bp.m_lstBeamletMLCs[iCurrBeamlet];
                                beamParams.SetAllLeafPositions(arrLeavesToApply);
                                try
                                {
                                    hBeamletBeam.ApplyParameters(beamParams);
                                }
                                catch (Exception exLeaf)
                                {
                                    // SFRThelper diagnostic: name the offending
                                    // aperture. Reports open-leaf-pair extents and
                                    // the min/max gap so an HD120 leaf-rule
                                    // violation (park position, interdigitation,
                                    // min gap) is identifiable.
                                    int nL = arrLeavesToApply.GetLength(1);
                                    int openCnt = 0; float minPair = float.MaxValue, maxPair = float.MinValue;
                                    float firstOpen = -9999, lastOpen = -9999;
                                    for (int li = 0; li < nL; li++)
                                    {
                                        float gap = arrLeavesToApply[1, li] - arrLeavesToApply[0, li];
                                        if (Math.Abs(gap) > 0.01f)
                                        {
                                            openCnt++;
                                            if (firstOpen < -9998) firstOpen = li;
                                            lastOpen = li;
                                            if (arrLeavesToApply[0, li] < minPair) minPair = arrLeavesToApply[0, li];
                                            if (arrLeavesToApply[1, li] > maxPair) maxPair = arrLeavesToApply[1, li];
                                        }
                                    }
                                    string szKind = (bFirstCalc && i == 0) ? "CLOSED-MLC baseline" : $"beamlet {iCurrBeamlet}";
                                    throw new ApplicationException(
                                        $"ApplyParameters (leaf positions) failed for {szKind} on beam '{hBeamletBeam.Id}': {exLeaf.Message} | " +
                                        $"leafPairs={nL}, openPairs={openCnt}, openRange=[{firstOpen}..{lastOpen}], " +
                                        $"openBankA_min={minPair:F1}, openBankB_max={maxPair:F1}", exLeaf);
                                }

                                lstCalcBeams.Add(hBeamletBeam.Id);
                            }
                        }
                        if (!(bFirstCalc && i == 0))
                            iCurrBeamlet++;
                    }
                    hProgress.Message($"Progress: Beamlet {iCurrBeamlet}/{iMaxBeamletCount}.");

                    if (lstCalcBeams.Count > 0)
                    {
                        int iRetryCnt = 0;
                        bool bSuccess = false;
                        string szLastCalcMsg = "(no message)";
                        do
                        {
                            try
                            {
                                CalculationResult calcRes = scratchPlan.CalculateDoseWithPresetValues(presetValues);
                                bSuccess = calcRes.Success;
                                // SFRThelper diagnostic: surface the calc result
                                // message instead of swallowing it. A sub-second
                                // failure is a rejection, not a computation - the
                                // message names the cause (model not valid for the
                                // machine, blocked field, MU out of range, ...).
                                if (!bSuccess)
                                {
                                    szLastCalcMsg = string.IsNullOrEmpty(calcRes.ToString()) ? "(empty)" : calcRes.ToString();
                                    hProgress?.Message($"  Dose calc reported failure: {szLastCalcMsg}");
                                }
                            }
                            catch (Exception exCalc)
                            {
                                bSuccess = false;
                                szLastCalcMsg = exCalc.GetType().Name + ": " + exCalc.Message;
                                hProgress?.Message($"  Dose calc threw: {szLastCalcMsg}");
                            }
                            iRetryCnt++;

                            if (!bSuccess && iRetryCnt < iMaxDoseCalcRetry)
                                hProgress.Message($"Retry: Beamlet {iCurrBeamlet}/{iMaxBeamletCount}.");
                        } while (!bSuccess && iRetryCnt < iMaxDoseCalcRetry);

                        if (!bSuccess)
                        {
                            //app.SaveModifications();
                            throw new ApplicationException($"Dose Calculation Failed after {iMaxDoseCalcRetry} attempts. Last message: {szLastCalcMsg}");
                        }

                        // SFRThelper patch 14.1: re-assert absolute presentation
                        // AFTER every dose calculation, immediately before
                        // readout. Setting it once at scratch-plan creation was
                        // not enough - first light of patch 14 showed the
                        // readout still in '%': either the calculation resets
                        // the presentation, or assignment before any dose
                        // exists never takes hold. Asserting here, when the
                        // dose objects it governs actually exist, is the
                        // cheapest placement that is correct under both
                        // explanations. The patch 13b/14 unit guard remains
                        // the enforcement; this line is the cure.
                        scratchPlan.DoseValuePresentation = DoseValuePresentation.Absolute;
                        if (!bPresentationReadbackLogged)
                        {
                            bPresentationReadbackLogged = true;
                            hProgress?.Message($"Presentation readback after re-assert: plan reports " +
                                $"'{scratchPlan.DoseValuePresentation}'. The readout dispatches on the unit it " +
                                "actually receives (patch 14.2); see the 'Dose readout mode' line below.");
                        }

                        if (bFirstDoseCalc)
                        {
                            hProgress.Message("Exporting optimization voxel grid and CT-to-dose voxel map...");
                            iMaxPointCnt = ExportOptimizationVoxels(scratchPlan, planResultsPath);
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
                                    int iDoseMatrixSize = hBeamDose.ZSize * hBeamDose.YSize * hBeamDose.XSize;

                                    if (arrFullDoseMatrix == null)
                                        arrFullDoseMatrix = new float[iDoseMatrixSize, 1];
                                    Array.Clear(arrFullDoseMatrix, 0, arrFullDoseMatrix.Length);

                                    // SFRThelper patch 14 (replaces the patch 3 formula):
                                    // dWeight means "divide presented dose down to Gy per
                                    // MU". Its value depends on the unit Eclipse actually
                                    // presents, worked out by the shared helper below
                                    // (patch 14.2/16) - the same helper the reference-dose
                                    // export uses, so the two can never disagree.
                                    bool bRelativeReadout;
                                    double dReadScale, dReadIntercept, dCrossCheck;
                                    string szReadUnit;
                                    double dWeight = ResolvePerMuDivisor(hBeamDose, blb, scratchPlan, b.Id,
                                        out bRelativeReadout, out dReadScale, out dReadIntercept, out szReadUnit,
                                        out dCrossCheck);
                                    if (szReadoutModeNote == null)
                                    {
                                        bRelativeReadoutMode = bRelativeReadout;
                                        szReadoutModeNote = bRelativeReadout
                                            ? "percent readout; dose in Gy per MU = percent / 100 x prescribed dose, " +
                                              "divided by the beam's own machine units (does not depend on how many " +
                                              "beams are in the plan)"
                                            : $"direct readout in {szReadUnit}; dose in Gy per MU = value divided by " +
                                              "the beam's own machine units";
                                        hProgress?.Message("Dose readout mode: " + szReadoutModeNote);

                                        // Two independent ways of working out the same
                                        // conversion. They must agree; if they do not,
                                        // something about the plan is not what we think.
                                        if (!Double.IsNaN(dCrossCheck) && dCrossCheck > 0)
                                        {
                                            double dRatio = dCrossCheck / dWeight;
                                            hProgress?.Message($"Conversion cross-check: machine-units route gives " +
                                                $"{dWeight:F1}, share-of-plan route gives {dCrossCheck:F1} " +
                                                $"(ratio {dRatio:F4}, 1.0000 = agree).");
                                            if (Math.Abs(dRatio - 1.0) > 0.02)
                                                hProgress?.Message("WARNING: the two conversion routes disagree by more " +
                                                    "than 2%. Treat this run's dose values with caution.");
                                        }
                                    }
                                    string szHDF5DataFile = System.IO.Path.Combine(szBeamPath, $"Beam_{b.Id}_Data.h5");

                                    float[,] arrClosedMLCDoseMatrix = null;
                                    if (bFirstCalc && i == 0)
                                    {
                                        arrClosedMLCDoseMatrix = new float[iDoseMatrixSize, 1];
                                        Helpers.FillDoseMatrix(hBeamDose, dWeight, ref arrClosedMLCDoseMatrix);
                                        bp.m_arrClosedMLCDoseMatrix = arrClosedMLCDoseMatrix;

                                        // SFRThelper patch 13c: first-light diagnostics for
                                        // the leakage baseline. Separates the failure
                                        // families: maxRawInt = 0 -> quantisation floor /
                                        // empty dose; NaN scalars -> units/prescription
                                        // (patch 13b throws first); healthy raw but zero
                                        // converted -> dWeight error.
                                        float fMaxBaseline = GetMaxMatrixValue(arrClosedMLCDoseMatrix);
                                        hProgress?.Message(FormatReadoutDiagnostics("closed-MLC baseline", b.Id,
                                                hBeamDose, dReadScale, dReadIntercept, blb.MetersetPerGy, dWeight) +
                                            $"; maxConverted={fMaxBaseline:E3} Gy/MU");
                                        ReportPresentationMeasurement(scratchPlan, blb, hBeamDose,
                                            bRelativeReadout, dReadScale / dWeight, fMaxBaseline, hProgress);
                                    }
                                    else
                                    {
                                        // SFRThelper patch 13c: index hoisted above the fill
                                        // so beamlet-0 diagnostics can key off it (was
                                        // declared after ExtractSparsePoints; pure
                                        // arithmetic, value unchanged).
                                        int iBeamletIdx = iCurrBeamlet - iNumBeamletsToBeCalcAtATime + i;
                                        bool bDiagBeamlet = (iBeamletIdx == 0);

                                        // SFRThelper patch 4: fill raw, correct (subtract
                                        // leakage, scale, clamp), THEN harvest sparse from
                                        // the corrected matrix - sparse and full are the
                                        // same matrix in two encodings from here on.
                                        Helpers.FillDoseMatrix(hBeamDose, dWeight, ref arrFullDoseMatrix);

                                        // SFRThelper patch 13c: first open beamlet of each
                                        // field gets the full diagnostic trace. dScale here
                                        // is also the quantisation floor of this beamlet's
                                        // dose in physical units - record it for the paper.
                                        if (bDiagBeamlet)
                                        {
                                            float fMaxBeamlet0 = GetMaxMatrixValue(arrFullDoseMatrix);
                                            hProgress?.Message(FormatReadoutDiagnostics("beamlet 0", b.Id,
                                                    hBeamDose, dReadScale, dReadIntercept, blb.MetersetPerGy, dWeight) +
                                                $"; maxConverted (pre-subtraction)={fMaxBeamlet0:E3} Gy/MU");
                                            ReportPresentationMeasurement(scratchPlan, blb, hBeamDose,
                                                bRelativeReadout, dReadScale / dWeight, fMaxBeamlet0, hProgress);
                                        }

                                        // subtract matrix from closedMLC matrix
                                        arrClosedMLCDoseMatrix = bp.m_arrClosedMLCDoseMatrix;
                                        for (int iDosePtIdx = 0; iDosePtIdx < iDoseMatrixSize; iDosePtIdx++)
                                        {
                                            arrFullDoseMatrix[iDosePtIdx, 0] = (arrFullDoseMatrix[iDosePtIdx, 0] - arrClosedMLCDoseMatrix[iDosePtIdx, 0]) * fDoseScalingFactor;
                                            if (arrFullDoseMatrix[iDosePtIdx, 0] < 0)
                                                arrFullDoseMatrix[iDosePtIdx, 0] = 0;
                                        }

                                        DoseData doseData = Helpers.ExtractSparsePoints(arrFullDoseMatrix, dInfCutoffValue);

                                        if (bDiagBeamlet)
                                            hProgress?.Message($"DIAG [{b.Id} / beamlet 0]: maxAfterSubtraction=" +
                                                $"{GetMaxMatrixValue(arrFullDoseMatrix):E3} Gy/MU; nnz={doseData.dosePoints.Count:N0}; " +
                                                $"subCutoff count={doseData.m_iNumCutoffValues:N0}, sum={doseData.m_dSumCutoffValues:E3}");

                                        Beamlet hBeamlet = bp.m_lstBeamlets[iBeamletIdx];
                                        // patch 4: values are already scaled by the time
                                        // extraction sees them - no second scaling here.
                                        hBeamlet.m_dSumCutoffValues = doseData.m_dSumCutoffValues;
                                        hBeamlet.m_iNumCutoffValues = doseData.m_iNumCutoffValues;

                                        Helpers.WriteInfMatrixHDF5(bExportFullInfMatrix, bp, arrFullDoseMatrix, doseData, szHDF5DataFile);
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
                } while (iCurrBeamlet < iMaxBeamletCount);

                // SFRThelper: FINAL-STATE CHECK (measurement #1, completed-run
                // form). After the loop the slot beams retain the LAST batch's
                // dose, and the scratch plan is kept after the run - so a
                // finished run supports the Eclipse ground-truth comparison
                // with no cancel gymnastics. Three UI observables, each
                // pinning one leg of the unit chain:
                //   (a) relative field-dose max  -> raw readout <-> display;
                //   (b) absolute field-dose max  -> the percent <-> Gy anchor
                //       (100% = total prescription, observed 2026-07-23:
                //       400.4% <-> 4.004 Gy at 1 Gy nominal Rx);
                //   (c) Fields-panel MU          -> MetersetPerGy against
                //       Eclipse's own renormalization arithmetic (observed:
                //       MU column = equal split of Rx across slot beams;
                //       displayed dose and displayed MU use DIFFERENT
                //       normalizations, so dose/MU is NOT comparable).
                try
                {
                    if (bRelativeReadoutMode)
                    {
                        double dRxGy = GetPlanTotalDoseGy(scratchPlan);

                        foreach (Beam b in arrOrigBeams)
                        {
                            Beam hSlot0 = tblBeamParameters[b.Id].m_lstBeamletBeam[0];
                            BeamDose hSlotDose = hSlot0.Dose;
                            if (hSlotDose == null)
                                continue;
                            double dInt0 = hSlotDose.VoxelToDoseValue(0).Dose;
                            double dScl0 = hSlotDose.VoxelToDoseValue(1).Dose - dInt0;
                            double dPmax = GetMaxRawVoxel(hSlotDose) * dScl0 + dInt0;   // percent
                            double dSlotMu = hSlot0.Meterset.Value;
                            double dAbsGy = dPmax / 100.0 * dRxGy;
                            double dGyPerMU = dAbsGy / dSlotMu;
                            hProgress?.Message(
                                $"FINAL-STATE CHECK [beam '{hSlot0.Id}', last aperture used]: dose per machine unit = " +
                                $"{dGyPerMU:E3} Gy/MU. To confirm this against Eclipse on the kept working plan: " +
                                $"(a) show this beam's dose with the display in %, the highest value should read ~{dPmax:F1}%; " +
                                $"(b) switch the display to Gy, it should read ~{dAbsGy:F4} Gy (100% = {dRxGy:F2} Gy prescribed); " +
                                $"(c) the Fields panel should list ~{dSlotMu:F0} machine units for this beam. " +
                                $"Then (b) divided by (c) is the number above - that is the whole conversion, checked " +
                                "against Eclipse's own screen.");
                        }
                    }
                }
                catch (Exception exFinalState)
                {
                    hProgress?.Message("FINAL-STATE CHECK unavailable: " + exFinalState.Message);
                }

                // export beam meta data
                foreach (Beam b in arrOrigBeams)
                {
                    hProgress.Message($"Progress: Finalizing beam {b.Id}.");

                    string szHDF5DataFile = System.IO.Path.Combine(szBeamPath, $"Beam_{b.Id}_Data.h5");
                    Helpers.WriteBeamletInfoHDF5(tblBeamParameters[b.Id], iMaxPointCnt, szHDF5DataFile);

                    string szBeamMetaDataFile = System.IO.Path.Combine(szBeamPath, $"Beam_{b.Id}_MetaData.json");
                    Helpers.WriteBeamMetaData(b, tblBeamParameters[b.Id], dInfCutoffValue, fDoseScalingFactor, szBeamMetaDataFile, szPrescriptionNote, szReadoutModeNote);
                }
                bCompleted = true;
            }
            finally
            {
                if (bCompleted)
                    hProgress?.Message("Influence matrix calculation finished.");
                else
                    hProgress?.Message($"Run ended without completing (cancelled or failed). " +
                        $"Scratch plan '{scratchPlan.Id}' retained in course '{szCourseId}'; " +
                        $"partial output at '{planResultsPath}' should not be used.");
            }
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
            for (int z = 0; z < iZSize; z++)
            {
                fZ = (float)(vOrigin.z + z * dZRes);
                for (int y = 0; y < iYSize; y++)
                {
                    fY = (float)(vOrigin.y + y * dYRes);
                    for (int x = 0; x < iXSize; x++)
                    {
                        fX = (float)(vOrigin.x + x * dXRes);

                        npPtCoords[i, 0] = fX;
                        npPtCoords[i, 1] = fY;
                        npPtCoords[i, 2] = fZ;
                        npPtWeights[i] = 1;
                        i++;
                    }
                }
            }

            // SFRThelper patch 6: the metadata below has always advertised
            // ct_to_dose_voxel_map, but upstream never wrote it. Built here:
            // one entry per CT voxel, in the same z-outer / y / x-inner linear
            // order used for dose voxels above; value = linear index of the
            // NEAREST dose voxel (per-axis Math.Round - floor would shift
            // every structure mask by up to a full dose voxel toward the
            // origin corner), or -1 when the CT voxel lies outside the dose
            // grid. Axis-aligned grids with scalar origins are assumed, the
            // same assumption this method already makes for coordinates.
            Image hCTForMap = hPlanSetup.StructureSet.Image;
            int iCTX = hCTForMap.XSize, iCTY = hCTForMap.YSize, iCTZ = hCTForMap.ZSize;
            VVector vCTOriginForMap = hCTForMap.Origin;
            int[] arrCtToDoseMap = new int[iCTX * iCTY * iCTZ];
            int iMapIdx = 0;
            for (int z = 0; z < iCTZ; z++)
            {
                double dCTz = vCTOriginForMap.z + z * hCTForMap.ZRes;
                int iDz = (int)Math.Round((dCTz - vOrigin.z) / dZRes);
                for (int y = 0; y < iCTY; y++)
                {
                    double dCTy = vCTOriginForMap.y + y * hCTForMap.YRes;
                    int iDy = (int)Math.Round((dCTy - vOrigin.y) / dYRes);
                    for (int x = 0; x < iCTX; x++)
                    {
                        double dCTx = vCTOriginForMap.x + x * hCTForMap.XRes;
                        int iDx = (int)Math.Round((dCTx - vOrigin.x) / dXRes);

                        if (iDx >= 0 && iDx < iXSize && iDy >= 0 && iDy < iYSize && iDz >= 0 && iDz < iZSize)
                            arrCtToDoseMap[iMapIdx] = iDz * iYSize * iXSize + iDy * iXSize + iDx;
                        else
                            arrCtToDoseMap[iMapIdx] = -1;
                        iMapIdx++;
                    }
                }
            }

            string szDataFilename = "OptimizationVoxels_Data.h5";
            string szH5Path = System.IO.Path.Combine(szOutputFolder, szDataFilename);
            long hf = Hdf5.CreateFile(szH5Path);
            Helpers.CreateDataSet<float>(hf, "/voxel_coordinate_XYZ_mm", npPtCoords);
            Helpers.CreateDataSet<float>(hf, "/voxel_weight_mm3", npPtWeights);
            Helpers.CreateDataSet<int>(hf, "/ct_to_dose_voxel_map", arrCtToDoseMap);
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
                cal_box_xyz_end = new[] { vOrigin.x + dXRes * iXSize, vOrigin.y + dYRes * iYSize, vOrigin.z + dZRes * iZSize },
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
                hProgress?.Message($"Exporting BEV structure outlines for beam {b.Id}...");

                string szH5OutlinesPath = System.IO.Path.Combine(szStructOutlinesFolder, $"Beam_{b.Id}_Data.h5");
                long fileId1 = Hdf5.CreateFile(szH5OutlinesPath);
                // SFRThelper patch 12: the cancellation return inside this loop
                // used to skip CloseFile(fileId1); finally guarantees it.
                try
                {
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
                        catch (Exception) { }

                        if (checkCancellation != null && checkCancellation())
                        {
                            hProgress?.Message("Calculation cancelled by user.");
                            return;
                        }
                    }
                }
                finally
                {
                    Hdf5.CloseFile(fileId1);
                }
            }

            // Export structure masks
            string szH5MaskPath = System.IO.Path.Combine(szOutputFolder, "StructureSet_Data.h5");
            long fileId = Hdf5.CreateFile(szH5MaskPath);
            List<object> lstAllStructsMetaData = new List<object>();
            // SFRThelper patch 12: as above - the cancellation return used to
            // skip CloseFile(fileId).
            try
            {

                Image hCT = hPlanSetup.StructureSet.Image;
                int iMaskIdx = 0;
                int iMaskCnt = hPlanSetup.StructureSet.Structures.Count();
                foreach (Structure s in hPlanSetup.StructureSet.Structures)
                {
                    iMaskIdx++;
                    hProgress?.Message($"Structure mask {iMaskIdx}/{iMaskCnt}: {s.Id}");
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
            }
            finally
            {
                Hdf5.CloseFile(fileId);
            }

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

            // SFRThelper patch 7: probe only the (x, y) columns inside the
            // structure's own bounding box (one-voxel margin, clamped).
            // Everything outside the box is zero by definition, and the mask
            // array is zero-initialized - identical output, without walking
            // the whole CT per structure. Null mesh (degenerate structure)
            // falls back to the full grid, so behavior is never worse than
            // upstream. Same axis-aligned assumption as the loop itself.
            int iXStart = 0, iXEnd = iXSize, iYStart = 0, iYEnd = iYSize;
            var hMesh = hStruct.MeshGeometry;
            if (hMesh != null)
            {
                var bounds = hMesh.Bounds;
                iXStart = (int)Math.Floor((bounds.X - hCT.Origin.x) / hCT.XRes) - 1;
                iXEnd = (int)Math.Ceiling((bounds.X + bounds.SizeX - hCT.Origin.x) / hCT.XRes) + 2;
                iYStart = (int)Math.Floor((bounds.Y - hCT.Origin.y) / hCT.YRes) - 1;
                iYEnd = (int)Math.Ceiling((bounds.Y + bounds.SizeY - hCT.Origin.y) / hCT.YRes) + 2;
                if (iXStart < 0) iXStart = 0;
                if (iYStart < 0) iYStart = 0;
                if (iXEnd > iXSize) iXEnd = iXSize;
                if (iYEnd > iYSize) iYEnd = iYSize;
            }

            VVector z_direction = ((iZSize - 1) * hCT.ZRes) * hCT.ZDirection;
            VVector y_step = hCT.YRes * hCT.YDirection;

            VVector start_x, stop;
            for (int x = iXStart; x < iXEnd; x++)    //) :  # scan X dimensions
            {
                start_x = hCT.Origin + ((x * hCT.XRes) * hCT.XDirection) + (iYStart * hCT.YRes) * hCT.YDirection;

                for (int y = iYStart; y < iYEnd; y++)  // # scan Y dimension
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
            // SFRThelper interdigitation fix (option 2): close the baseline at
            // the field centre-line (X = 0), the same closed-leaf tip position
            // used for non-covering leaves in GetLeafPositions. MSK parked at
            // (jaws.X1 - 1); centre-line closing is interdigitation-safe on
            // HD120 and keeps the leakage baseline geometrically consistent
            // with every beamlet aperture, so per-voxel subtraction is exact.
            float parkPos = 0.0f;

            // loop through all leafs
            for (int i = 0; i < nLeafs; i++)
            {
                positions[0, i] = parkPos;
                positions[1, i] = parkPos;
            }
            return positions;
        }

        /// <summary>
        /// SFRThelper patch 1 (scratch-plan pattern): leaf pose fully open to
        /// the jaw edges. Used as the initial static aperture for scratch
        /// copies of non-static (fluence/DMLC) source beams, which have no
        /// single honest aperture of their own. Mirror image of
        /// GetClosedLeafPositions. Interim rule until the PTV-envelope
        /// aperture fit (patch 2) replaces aperture-based beamlet pruning.
        /// </summary>
        public static float[,] GetOpenLeafPositions(int nLeafs, VRect<double> jaws)
        {
            float[,] positions = new float[2, nLeafs];

            // loop through all leafs
            for (int i = 0; i < nLeafs; i++)
            {
                positions[0, i] = (float)jaws.X1;
                positions[1, i] = (float)jaws.X2;
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
            // SFRThelper patch 11: 0 is a legal yStart (a beamlet row whose
            // lower edge sits exactly on the midline), so "unset" needs its
            // own flag - the upstream (yStart == 0) test overwrote the value
            // whenever a multi-leaf beamlet started at y = 0, shifting that
            // row's recorded rectangle by one leaf width.
            bool bYStartSet = false;

            // SFRThelper interdigitation fix (option 2): park non-covering
            // leaf pairs CLOSED at the field centre-line (X = 0), consistently
            // with GetClosedLeafPositions. MSK's original parked every closed
            // leaf at (jaws.X1 - 1); on Millennium 120 (where MSK validated)
            // that is legal because all closed leaves retract equally to the
            // same edge. On HD120 a single open beamlet far from that edge
            // strands the parked leaves ~a field width from their neighbours,
            // violating interdigitation / adjacent-leaf-travel limits. Parking
            // closed pairs at X = 0 keeps every leaf near its neighbours AND
            // matches the closed-MLC baseline geometry exactly, so leakage
            // subtraction stays exact (the baseline and every beamlet share
            // the same closed-leaf tip position).
            float parkPos = 0.0f;

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
                    if (!bYStartSet)
                    {
                        yStart = fCurrLeafPosY; // half field in mm
                        bYStartSet = true;
                    }

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
        /// <summary>
        /// SFRThelper patch 2: bounding boxes of the target structure's BEV
        /// outline segments at the isocenter plane, grown by a margin, in the
        /// MLC (collimator) frame. Beamlet enumeration keeps any beamlet whose
        /// rectangle intersects one of these boxes. Boxes (not exact polygons)
        /// are deliberately over-inclusive: an extra beamlet costs dose-calc
        /// time; a missing one silently re-cages the placement search.
        /// NOTE: outline points are rotated by the collimator angle to move
        /// them from the fixed BEV frame into the MLC frame; this convention
        /// is pinned by the collimator-0 differential runs and must be
        /// re-verified before trusting rotated-collimator plans.
        /// </summary>
        public static List<VRect<float>> GetTargetEnvelopeBoxes(Beam beam, Structure target, float marginMM)
        {
            Point[][] arrOutlines = beam.GetStructureOutlines(target, true);
            if (arrOutlines == null || arrOutlines.Length == 0)
                throw new ApplicationException(
                    $"Target structure '{target.Id}' has no BEV outline for beam '{beam.Id}'.");

            double dCollDeg = beam.ControlPoints[0].CollimatorAngle;
            double dCollRad = dCollDeg * Math.PI / 180.0;
            double dCos = Math.Cos(dCollRad);
            double dSin = Math.Sin(dCollRad);

            List<VRect<float>> lstBoxes = new List<VRect<float>>();
            foreach (Point[] segment in arrOutlines)
            {
                if (segment == null || segment.Length == 0)
                    continue;
                double dMinX = double.MaxValue, dMaxX = double.MinValue;
                double dMinY = double.MaxValue, dMaxY = double.MinValue;
                foreach (Point pt in segment)
                {
                    // fixed BEV frame -> MLC frame (rotate by collimator angle)
                    double x = pt.X * dCos + pt.Y * dSin;
                    double y = -pt.X * dSin + pt.Y * dCos;
                    if (x < dMinX) dMinX = x;
                    if (x > dMaxX) dMaxX = x;
                    if (y < dMinY) dMinY = y;
                    if (y > dMaxY) dMaxY = y;
                }
                lstBoxes.Add(new VRect<float>(
                    (float)(dMinX - marginMM), (float)(dMinY - marginMM),
                    (float)(dMaxX + marginMM), (float)(dMaxY + marginMM)));
            }
            if (lstBoxes.Count == 0)
                throw new ApplicationException(
                    $"Target structure '{target.Id}' produced no usable BEV outline segments for beam '{beam.Id}'.");
            return lstBoxes;
        }

        /// <summary>
        /// SFRThelper patch 2: axis-aligned rectangle intersection between a
        /// beamlet and any envelope box. A null box list means no target was
        /// specified: keep every beamlet inside the jaws.
        /// </summary>
        public static bool IsBeamletInAnyEnvelopeBox(VRect<float> beamletRect, List<VRect<float>> lstBoxes)
        {
            if (lstBoxes == null)
                return true;
            foreach (VRect<float> box in lstBoxes)
            {
                if (beamletRect.X1 < box.X2 && beamletRect.X2 > box.X1 &&
                    beamletRect.Y1 < box.Y2 && beamletRect.Y2 > box.Y1)
                    return true;
            }
            return false;
        }

        public static bool IsBeamletInsideAperture(float[,] leafs, float[,] staticAperture)
        {
            int nLeafs = leafs.GetLength(1); // halcyon ? 57 : 60; // here we're dealing with all real leaf pairs.
            float fTol = 0.01f;
            for (int i = 0; i < nLeafs; i++)
            {
                //NMLC.LeafPositionPair pair = leafs.GetLeafPair(i);
                //if (!pair.IsClosed())
                if (Math.Abs(leafs[0, i] - leafs[1, i]) > fTol)
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
            // SFRThelper patch 13a: upstream's condition above is a silent
            // skip - a source plan without a prescription (bare test/phantom
            // plan) left the scratch plan without one, and ESAPI then has no
            // defined absolute-dose presentation: VoxelToDoseValue returns
            // NaN, and NaN fails every downstream comparison, producing a
            // structurally valid but entirely empty matrix. MSK never hit
            // this because clinical plans always carry a prescription; the
            // precondition was implicit in their workflow. The influence
            // matrix itself is per-MU physics and does not depend on the
            // prescription magnitude, so a nominal 1 fx x 1 Gy at 100% is a
            // pure enabling constant. Applied to the scratch plan only.
            else
            {
                copyPlan.SetPrescription(1, new DoseValue(1.0, DoseValue.DoseUnit.Gy), 1.0);
            }

            return copyPlan;
        }

        // SFRThelper patch 13c: first-light readout diagnostics. One line per
        // diagnosed dose readout: the decode scalars (with the DoseValue unit
        // string, so a cGy or Percent presentation is visible immediately),
        // the per-MU conversion factors, and the maximum raw integer over the
        // whole volume. Reading of the outcomes:
        //   maxRawInt = 0                 -> quantisation floor or empty dose
        //   NaN scalars                   -> units/prescription (13b throws)
        //   raw healthy, converted zero   -> dWeight/units error
        //   converted healthy, zero later -> leakage subtraction cancelling
        private static string FormatReadoutDiagnostics(string szWhat, string szFieldId, BeamDose hDose,
            double dScale, double dIntercept, double dMetersetPerGy, double dWeight)
        {
            DoseValue dvOne = hDose.VoxelToDoseValue(1);
            return $"DIAG [{szFieldId} / {szWhat}]: dScale={dScale:E6} ({dvOne.UnitAsString}/int), " +
                $"dIntercept={dIntercept:E6}, MetersetPerGy={dMetersetPerGy:F4}, dWeight={dWeight:F6}, " +
                $"maxRawInt={GetMaxRawVoxel(hDose):N0}";
        }

        // SFRThelper patch 13c: maximum raw integer voxel over the volume.
        // One extra pass over the dose grid per diagnosed beamlet only -
        // negligible next to the dose calculation itself.
        private static int GetMaxRawVoxel(BeamDose hDose)
        {
            int iX = hDose.XSize, iY = hDose.YSize, iZ = hDose.ZSize;
            int[,] arrBuffer = new int[iX, iY];
            int iMax = int.MinValue;
            for (int z = 0; z < iZ; z++)
            {
                hDose.GetVoxels(z, arrBuffer);
                for (int j = 0; j < iY; j++)
                {
                    for (int i = 0; i < iX; i++)
                    {
                        if (arrBuffer[i, j] > iMax)
                            iMax = arrBuffer[i, j];
                    }
                }
            }
            return iMax;
        }

        // SFRThelper patch 13c: maximum value of a (N,1) matrix buffer.
        private static float GetMaxMatrixValue(float[,] arrMatrix)
        {
            float fMax = float.MinValue;
            int iCnt = arrMatrix.GetLength(0);
            for (int i = 0; i < iCnt; i++)
            {
                if (arrMatrix[i, 0] > fMax)
                    fMax = arrMatrix[i, 0];
            }
            return fMax;
        }

        // SFRThelper patch 14.2/16: works out how to turn the numbers Eclipse
        // hands back into "dose in Gy for one machine unit (MU)". Eclipse can
        // answer in different units depending on the system:
        //   Gy   -> use as-is, divide by the machine units we asked for
        //   cGy  -> hundredths of a Gy, so also divide by 100
        //   %    -> a percentage of a reference level; dividing by
        //           (100 x MetersetPerGy) converts it to Gy per MU - the
        //           reference level cancels out of the math, which is why
        //           this works no matter how many machine units were used
        // Anything else means we don't understand the numbers, and we stop
        // the run rather than save wrong values. This one method is used by
        // BOTH the matrix readout and the reference-dose export, so the two
        // can never quietly use different conversions.
        // SFRThelper patch 18 (corrects patch 3 and 14.2): turns the numbers
        // Eclipse hands back into "dose in Gy for one machine unit (MU)".
        //
        // What went wrong before: we divided using MetersetPerGy alone. That
        // number depends on how many beams happen to be sitting in the plan
        // at the moment of reading - Eclipse shares the prescription out
        // among them - so the same beam gave different answers depending on
        // its company. The answer key (2 beams in the plan) came out 2x too
        // small and the matrix pieces (10 beams) 10x too small, which made
        // the two impossible to compare. Upstream MSK actually had this
        // right, via a "share of the plan" term; patch 3 removed that term
        // believing it was plan-specific weighting. It was not.
        //
        // The fix: use the beam's OWN machine units, exactly the number
        // Eclipse shows in its Fields panel. Dose and machine units are
        // always scaled together, so dose-divided-by-machine-units is the
        // same no matter how the plan is scaled or how many beams it holds.
        // In percent mode the percentage is first turned into real dose
        // using the plan's prescription (100% = the prescribed dose, checked
        // against Eclipse's own display on 2026-07-23: 400.4% = 4.004 Gy).
        //
        // A second, independent way of working out the same number (using
        // the beam's share of the plan, upstream's route) is computed
        // alongside and handed back so the run can cross-check the two and
        // complain if they ever disagree.
        private static double ResolvePerMuDivisor(BeamDose hBeamDose, Beam hBeam, ExternalPlanSetup hPlanOfBeam,
            string szSourceFieldId, out bool bRelativeReadout, out double dReadScale, out double dReadIntercept,
            out string szReadUnit, out double dCrossCheckDivisor)
        {
            DoseValue dvProbe = hBeamDose.VoxelToDoseValue(1);
            dReadIntercept = hBeamDose.VoxelToDoseValue(0).Dose;
            dReadScale = dvProbe.Dose - dReadIntercept;
            szReadUnit = dvProbe.UnitAsString;
            bRelativeReadout = false;
            dCrossCheckDivisor = double.NaN;

            if (Double.IsNaN(dReadScale) || Double.IsNaN(dReadIntercept))
            {
                throw new ApplicationException(
                    $"Dose readout values are not numbers (NaN) for beam '{hBeam.Id}' " +
                    $"(source field '{szSourceFieldId}'). Usual cause: the plan has no " +
                    "prescription. Stopping rather than saving an all-zero result.");
            }

            // The beam's machine units, as Eclipse currently has them.
            double dBeamMu = double.NaN;
            try { dBeamMu = hBeam.Meterset.Value; }
            catch { /* handled just below */ }
            if (Double.IsNaN(dBeamMu) || dBeamMu <= 0)
                throw new ApplicationException(
                    $"Beam '{hBeam.Id}' (source field '{szSourceFieldId}') does not report valid " +
                    $"machine units (got {dBeamMu}). Every dose value depends on this number, so " +
                    "the run stops rather than saving wrongly scaled results.");

            double dWeight;
            if (dvProbe.Unit == DoseValue.DoseUnit.Gy)
                dWeight = dBeamMu;
            else if (dvProbe.Unit == DoseValue.DoseUnit.cGy)
                dWeight = 100.0 * dBeamMu;
            else if (dvProbe.Unit == DoseValue.DoseUnit.Percent)
            {
                bRelativeReadout = true;
                double dRxGy = GetPlanTotalDoseGy(hPlanOfBeam);
                if (Double.IsNaN(dRxGy) || dRxGy <= 0)
                    throw new ApplicationException(
                        $"Percent readout for beam '{hBeam.Id}' needs the plan's prescribed dose " +
                        "to convert into Gy, but the plan does not report one. Stopping.");
                dWeight = 100.0 * dBeamMu / dRxGy;

                // Second route, independent of the machine-units reading:
                // the beam's share of the plan combined with MetersetPerGy
                // (this is upstream MSK's route, once its units are fixed).
                try
                {
                    double dSumWeights = 0;
                    foreach (Beam bAny in hPlanOfBeam.Beams)
                        if (!bAny.IsSetupField) dSumWeights += bAny.WeightFactor;
                    if (dSumWeights > 0 && hBeam.WeightFactor > 0 &&
                        !Double.IsNaN(hBeam.MetersetPerGy) && hBeam.MetersetPerGy > 0)
                        dCrossCheckDivisor = 100.0 * hBeam.MetersetPerGy * hBeam.WeightFactor / dSumWeights;
                }
                catch { /* cross-check is optional; the main route stands */ }
            }
            else
            {
                throw new ApplicationException(
                    $"Dose readout unit '{dvProbe.UnitAsString}' for beam '{hBeam.Id}' " +
                    $"(source field '{szSourceFieldId}') is not Gy, cGy, or %. " +
                    "Stopping rather than saving numbers in unknown units.");
            }
            return dWeight;
        }

        // The plan's prescribed dose in Gy (100% on the percent scale).
        private static double GetPlanTotalDoseGy(ExternalPlanSetup hPlan)
        {
            try
            {
                DoseValue dv = hPlan.TotalDose;
                if (Double.IsNaN(dv.Dose)) return double.NaN;
                return (dv.Unit == DoseValue.DoseUnit.cGy) ? dv.Dose * 0.01 : dv.Dose;
            }
            catch { return double.NaN; }
        }


        // SFRThelper patch 16: the reference-dose export - the "answer key".
        // Copies each real treatment field into the scratch plan exactly as
        // it is (same leaf shapes, same angles), has Eclipse compute its
        // dose in one shot, saves that dose in the run folder using the same
        // Gy-per-MU conversion the matrix uses, and then deletes the copies
        // so the rest of the run proceeds as if this never happened. A later
        // checking step compares "sum of all the small pieces" against these
        // files to measure how close the matrix comes to the real thing.
        private static void ExportReferenceDoses(ExternalPlanSetup hSourcePlan, ExternalPlanSetup scratchPlan,
            string szRunFolder, string szOverrideMachine, string szOverrideEnergy, DisplayProgress hProgress)
        {
            hProgress?.Message("Reference dose: copying the real treatment fields for a one-shot dose calculation...");

            List<Beam> lstRefBeams = new List<Beam>();
            List<string> lstSourceIds = new List<string>();
            List<KeyValuePair<string, MetersetValue>> refPresets = new List<KeyValuePair<string, MetersetValue>>();
            foreach (Beam bSource in hSourcePlan.Beams)
            {
                if (bSource.IsSetupField)
                    continue;
                Beam hRefCopy = CopyBeam(bSource, scratchPlan, szOverrideMachine, szOverrideEnergy);

                // Open the copy's leaves all the way to the jaw opening.
                // The matrix pieces always tile the whole jaw opening, so
                // the answer key must be that same whole opening - no matter
                // where the source plan's leaves happen to be sitting
                // (2026-07-23: a test plan's leaves were parked nearly shut,
                // and the answer key faithfully measured a shut field).
                VRect<double> refJaws = GetJawsFromBeam(bSource);
                float[,] arrOpenLeaves = GetOpenLeafPositions(
                    bSource.ControlPoints[0].LeafPositions.GetLength(1), refJaws);
                BeamParameters refParams = hRefCopy.GetEditableParameters();
                refParams.SetAllLeafPositions(arrOpenLeaves);
                hRefCopy.ApplyParameters(refParams);

                lstRefBeams.Add(hRefCopy);
                lstSourceIds.Add(bSource.Id);
                refPresets.Add(new KeyValuePair<string, MetersetValue>(hRefCopy.Id, new MetersetValue(PRESET_METERSET_MU, DosimeterUnit.MU)));
            }

            CalculationResult calcRes = scratchPlan.CalculateDoseWithPresetValues(refPresets);
            if (!calcRes.Success)
                throw new ApplicationException("Reference dose calculation did not succeed; stopping the run. " +
                    "The matrix cannot be checked without the answer key, so fix this before re-running.");

            // Keep the display in real units, same as the main run does.
            scratchPlan.DoseValuePresentation = DoseValuePresentation.Absolute;

            float[,] arrRefDose = null;
            for (int i = 0; i < lstRefBeams.Count; i++)
            {
                Beam hRef = lstRefBeams[i];
                string szSourceId = lstSourceIds[i];
                BeamDose hDose = hRef.Dose;
                if (hDose == null)
                    throw new ApplicationException($"Reference beam '{hRef.Id}' has no dose after calculation; stopping.");

                bool bRel; double dScale, dIntercept, dCrossCheck; string szUnit;
                double dWeight = ResolvePerMuDivisor(hDose, hRef, scratchPlan, szSourceId,
                    out bRel, out dScale, out dIntercept, out szUnit, out dCrossCheck);

                int iSize = hDose.XSize * hDose.YSize * hDose.ZSize;
                if (arrRefDose == null || arrRefDose.GetLength(0) != iSize)
                    arrRefDose = new float[iSize, 1];
                Helpers.FillDoseMatrix(hDose, dWeight, ref arrRefDose);

                string szH5 = System.IO.Path.Combine(szRunFolder, $"Reference_{szSourceId}_Data.h5");
                string szJson = System.IO.Path.Combine(szRunFolder, $"Reference_{szSourceId}_MetaData.json");
                Dictionary<string, object> meta = new Dictionary<string, object>
                {
                    { "source_field", szSourceId },
                    { "machine_used", hRef.TreatmentUnit.Id },
                    { "energy", hRef.EnergyModeDisplayName },
                    { "MetersetPerGy", hRef.MetersetPerGy },
                    { "machine_units_of_this_beam", hRef.Meterset.Value },
                    { "units", "Gy per MU (same conversion as the matrix files)" },
                    { "readout", bRel ? "percent, converted via 100 * MetersetPerGy" : $"direct ({szUnit})" },
                    { "aperture", "jaws and angles copied from the source field; leaves opened to the jaw opening (the same area the matrix pieces cover)" },
                    { "purpose", "answer key: Eclipse's one-shot dose of the whole field, for checking the summed matrix against" }
                };
                Helpers.WriteReferenceDoseFiles(arrRefDose, meta, szH5, szJson);
                hProgress?.Message($"Reference dose saved for field '{szSourceId}' " +
                    $"(max {GetMaxMatrixValue(arrRefDose):E3} Gy/MU) -> {System.IO.Path.GetFileName(szH5)}");
            }

            // Remove the copies so the rest of the run is untouched by them.
            foreach (Beam hRef in lstRefBeams)
                scratchPlan.RemoveBeam(hRef);
            hProgress?.Message("Reference dose export done; temporary field copies removed. Continuing with the normal run.");
        }

        // SFRThelper patch 14 (measurement #1), reworked by 14.2. Two modes:
        //
        // Absolute readout (Gy/cGy honored): flip to Relative, read the decode
        // scalar, reproduce the retired %-formula per raw integer, and print
        // the ratio against the pipeline's Gy-per-int - predicted 1.0E+004.
        // A genuine cross-presentation measurement.
        //
        // Relative(%) readout: flipping presentations proves nothing (both
        // sides would read the same numbers - the ratio becomes an algebraic
        // identity, not a measurement). Instead print the one-time MANUAL
        // verification: open the scratch plan in Eclipse, display the named
        // slot beam's dose in absolute mode, and compare its max against the
        // predicted value. Agreement pins the %-derivation to ground truth.
        //
        // Runs only for diagnosed beamlets; any failure is reported and
        // skipped - the measurement is optional, run integrity is not.
        private static void ReportPresentationMeasurement(ExternalPlanSetup hScratchPlan, Beam blb,
            BeamDose hBeamDose, bool bRelativeReadout, double dPipelineGyPerInt, float fMaxConvertedGyPerMU,
            DisplayProgress hProgress)
        {
            try
            {
                if (bRelativeReadout)
                {
                    hProgress?.Message($"MEASUREMENT #1 [{blb.Id}]: relative(%) readout; pipeline scale = " +
                        $"{dPipelineGyPerInt:E6} Gy/int. Note: slot-beam dose is OVERWRITTEN by later batches, " +
                        "so after a completed run do not compare Eclipse against this batch - use the " +
                        "FINAL-STATE CHECK lines printed at completion (or cancel at the first batch boundary " +
                        "to preserve this batch's dose).");
                    return;
                }

                double dRelScalePerInt;
                string szRelUnit;
                hScratchPlan.DoseValuePresentation = DoseValuePresentation.Relative;
                try
                {
                    DoseValue dvRel = hBeamDose.VoxelToDoseValue(1);
                    dRelScalePerInt = dvRel.Dose - hBeamDose.VoxelToDoseValue(0).Dose;
                    szRelUnit = dvRel.UnitAsString;
                }
                finally
                {
                    hScratchPlan.DoseValuePresentation = DoseValuePresentation.Absolute;
                }
                double dLegacyPerInt = dRelScalePerInt * 100.0 / blb.MetersetPerGy;
                double dRatio = dLegacyPerInt / dPipelineGyPerInt;
                hProgress?.Message($"MEASUREMENT #1 [{blb.Id}]: relative dScale={dRelScalePerInt:E6} {szRelUnit}/int; " +
                    $"legacy-formula value/int={dLegacyPerInt:E6}; absolute value/int={dPipelineGyPerInt:E6} Gy; " +
                    $"ratio legacy/absolute={dRatio:E4} (predicted 1.0000E+004)");
            }
            catch (Exception ex)
            {
                hProgress?.Message($"MEASUREMENT #1 [{blb.Id}]: comparison unavailable " +
                    $"({ex.Message}); skipping (pipeline output unaffected).");
                try { hScratchPlan.DoseValuePresentation = DoseValuePresentation.Absolute; } catch { }
            }
        }

        public static Beam CopyBeam(Beam beam, ExternalPlanSetup plansetup,
            string szOverrideMachine = null, string szOverrideEnergy = null)
        {
            if (!beam.IsSetupField)
            {
                // SFRThelper machine override: on a research box the source
                // plan's machine (e.g. an imported TrueBeam) may not be
                // commissioned locally, so new-beam validation rejects it.
                // The override re-hosts scratch slots on a locally available
                // machine/energy. Geometry (jaws, angles, isocenter, leaves)
                // is untouched; only the machine identity changes. WARNING:
                // the resulting matrix reflects the OVERRIDE machine's beam
                // model, not the source plan's - correct for pipeline
                // development, not for optimizing a real plan on the source
                // machine. The machine actually used is stamped in metadata.
                string szMachine = string.IsNullOrWhiteSpace(szOverrideMachine)
                    ? beam.TreatmentUnit.Id : szOverrideMachine.Trim();
                string energyModeDisp = string.IsNullOrWhiteSpace(szOverrideEnergy)
                    ? beam.EnergyModeDisplayName : szOverrideEnergy.Trim();
                Char[] sep = { '-' };
                string energyMode = energyModeDisp.Split(sep).First();
                string pfm = energyModeDisp.Split(sep).Count() > 1 ? energyModeDisp.Split(sep).Last() : null;
                ExternalBeamMachineParameters extParams = new ExternalBeamMachineParameters(szMachine, energyMode, beam.DoseRate, beam.Technique.Id, pfm);

                // SFRThelper patch 1: always create MLC-carrying copies.
                // Upstream fell back to AddStaticBeam (jaw-only, no MLC) when the
                // source had no MLC or carried an optimal fluence - but slot
                // beams receive per-beamlet apertures via SetAllLeafPositions,
                // which requires an MLC; MLC-ness is fixed at creation time.
                // Aperture rule: a static source passes its CP0 pose through
                // unchanged (identical to upstream behavior for those sources);
                // a non-static source gets leaves opened to the jaws, since a
                // dynamic sequence has no single honest aperture and its first
                // control point is a near-closed sliver that would cage
                // beamlet enumeration.
                if (beam.MLC == null)
                    throw new InvalidOperationException(
                        $"Cannot create an influence-matrix copy of beam '{beam.Id}': " +
                        "it has no MLC, and beamlet apertures are formed from MLC leaves.");

                VRect<double> jaws = GetJawsFromBeam(beam);
                float[,] arrInitialLeaves;
                if (beam.MLCPlanType == MLCPlanType.Static)
                    arrInitialLeaves = beam.ControlPoints[0].LeafPositions;
                else
                    arrInitialLeaves = GetOpenLeafPositions(
                        beam.ControlPoints[0].LeafPositions.GetLength(1), jaws);

                Beam copyBeam;
                try
                {
                    copyBeam = plansetup.AddMLCBeam(extParams, arrInitialLeaves, jaws,
                        beam.ControlPoints[0].CollimatorAngle, beam.ControlPoints[0].GantryAngle,
                        beam.ControlPoints[0].PatientSupportAngle, beam.IsocenterPosition);
                }
                catch (Exception ex)
                {
                    // Diagnostic wrap: ESAPI's beam-creation errors are terse;
                    // echo every parameter that went into the call so a failed
                    // run diagnoses itself. Additionally run a bisection probe:
                    // try AddStaticBeam with the IDENTICAL machine parameters.
                    // If the probe succeeds, the machine/energy/doseRate/
                    // technique tuple is valid and the failure is specific to
                    // MLC beam creation; if it also fails, the tuple itself is
                    // being rejected. Any probe beam is removed immediately.
                    string szProbe;
                    try
                    {
                        Beam probeBeam = plansetup.AddStaticBeam(extParams, jaws,
                            beam.ControlPoints[0].CollimatorAngle, beam.ControlPoints[0].GantryAngle,
                            beam.ControlPoints[0].PatientSupportAngle, beam.IsocenterPosition);
                        plansetup.RemoveBeam(probeBeam);
                        szProbe = "AddStaticBeam with identical parameters SUCCEEDED - " +
                            "tuple is valid; failure is specific to MLC beam creation.";
                    }
                    catch (Exception exProbe)
                    {
                        szProbe = "AddStaticBeam with identical parameters ALSO FAILED (" +
                            exProbe.Message + ") - the machine parameter tuple itself is rejected.";
                    }
                    throw new ApplicationException(
                        $"AddMLCBeam failed for source beam '{beam.Id}': {ex.Message} | " +
                        $"machine(used)='{szMachine}', energy(used)='{energyMode}', source_machine='{beam.TreatmentUnit.Id}', pfm='{pfm ?? "null"}', " +
                        $"doseRate={beam.DoseRate}, technique='{beam.Technique.Id}', " +
                        $"MLCPlanType={beam.MLCPlanType}, leafPairs={arrInitialLeaves.GetLength(1)}, " +
                        $"jaws=[{jaws.X1:F1},{jaws.Y1:F1},{jaws.X2:F1},{jaws.Y2:F1}] | PROBE: {szProbe}", ex);
                }

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
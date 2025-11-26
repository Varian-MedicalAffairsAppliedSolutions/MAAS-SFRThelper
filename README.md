# MAAS-SFRThelper

A shared-source toolkit providing extensive support for clinical SFRT (Spatially Fractionated Radiation Therapy) workflows and research. MAAS-SFRThelper enables the creation of structure patterns (spheres) or irregular structures (straight and angled rods) with comprehensive quantitative dose evaluation capabilities. The toolkit is designed for treatment plans that intentionally create heterogeneous dose distributions with large peak-to-valley ratios, fundamentally different from traditional homogeneous PTV coverage approaches.

## Features 

### SphereLattice

The SphereLattice tab is used to create spherical high-dose boost regions within a selected PTV. These spheres are arranged in a structured 3D pattern designed to create dose heterogeneity.

Options available:

- Target: Dropdown to select the target structure where the sphere lattice will be generated.
- Pattern:
  - Hexagonal Closest Packed (HCP)
  - Simple Cubic (SCP)
  - Alternating Cubic (AC)
  - Centroidal Voronoi Tessellation in 3D (CVT3D)
- Radius (mm): Defines the radius of each sphere.
- Spacing (mm): Defines center-to-center spacing between spheres.
- Lattice X / Y Shift (mm): Allows you to laterally shift the lattice in the transverse plane.
- Lateral Spacing Factor: A multiplier for spacing in the X/Y direction.
- Full Spheres Only (slider): By default, only full spheres are included. Moving the slider left allows cropped spheres to be included near the edges.
- Create spheres as single structure only: Reduces structure count by merging all spheres into one ROI.
- Create void structures: Generates complementary voids (valleys) to guide dose distribution and improve plan optimization.
- Option to export sphere/void locations and specifications in csv file.

  <img width="717" height="465" alt="image" src="https://github.com/user-attachments/assets/0ea82290-cf05-4d70-8a26-d4b2673d3b28" />


### SCART

The SCART (Stereotactic Central Ablative Radiation Therapy) tab enables generation of a central high-dose boost volume (SCART Treatment Volume, or STV) within a selected gross tumor volume (GTV). This is based on the SCART technique which focuses ablative doses to the hypoxic tumor core while delivering lower peripheral doses. Users can define an inner STV within a selected GTV structure which can then be used in treatment planning systems to prescribe a higher dose compared to surrounding tissue.

Options available:

- Select GTV: Choose a structure representing the gross tumor volume.
- Define STV Parameters: Specify percentage reduction or fixed margin from GTV to generate the STV.
- Generate STV Structure: Creates a structure in the Eclipse workspace that can be used for dose painting or boost prescriptions.
- Optimization: Optimize and develop appropriate treatment plans. 

Note: The SCART tab reflects implementation aligned with emerging clinical research but is not validated for clinical use. For more background, refer to peer-reviewed studies describing SCART dosimetry and outcomes.

<img width="721" height="528" alt="image" src="https://github.com/user-attachments/assets/d6b5cf05-e46b-4bec-84c2-2fd00116ea33" />


### RapidRods

The RapidRods tab supports creation of cylindrical rod-shaped dose structures. These are intended for experimental or directional SFRT planning techniques.

- Generates straight or angled rods inside selected PTVs.
- Rod axis definition and advanced geometry options are under development.
- Additional parameters like rod length, diameter, and spacing will be supported in future updates.

<img width="720" height="663" alt="image" src="https://github.com/user-attachments/assets/f16420be-a5bc-4182-a7d4-d70b95b821bc" />

### Optimization
The SFRT Optimization module provides an integrated workflow for optimizing Spatially Fractionated Radiation Therapy (SFRT) plans directly within Eclipse. This module streamlines the process of setting up and running VMAT optimization with Peak-Valley dose objectives specific to lattice-based SFRT treatments.

Workflow

1. Select Structures
Choose the Lattice (Peak) structure from the dropdown
Choose the Valley structure, or select "[Auto-create Valley]" to generate one

2. Create Valley Structure (if needed)
Select the PTV structure
Click "Create Valley" to generate Valley = PTV - Lattice

3. Populate Objectives
Click "Populate Objectives" to fill the table with default values
Peak objectives: Lower dose bounds to ensure hot spots in lattice
Valley objectives: Upper dose bounds to spare tissue between peaks
OAR objectives: Auto-matched based on structure names

4. Edit Objectives
Modify dose, volume, and priority values as needed
Use checkboxes to include/exclude objectives
Add or remove objectives using the buttons below the table

5. Create Objectives in Eclipse
Click "Create Objectives" to apply the table to the Eclipse plan
Existing objectives will be cleared and replaced

6. Run Optimization
Select VMAT beams to optimize
Choose MLC and intermediate dose options
Click "Run Optimization" (typically completes in 5-15 minutes)

7. Calculate Dose
After optimization completes, click "Calculate Dose"
Note: Dose calculation for SFRT plans can take 1+ hour due to complex MLC patterns
DVH summary displays automatically upon completion

<img width="732" height="845" alt="image" src="https://github.com/user-attachments/assets/7c3755ed-1b3d-4160-a877-9bce4e676ff0" />

This module is currently under heavy development. This module is experimental. Please reach out with comments and suggestions on how to improve. 

### Evaluation

The Evaluation tab provides comprehensive dose analysis with automated peak-valley evaluation specifically designed for SFRT workflows. The module implements 3D clustering using percentile-based thresholding (80th percentile for peaks, 20th percentile for valleys) to robustly identify dose heterogeneity patterns across varying prescription doses and delivery techniques.

#### Analysis Modes

The evaluation module provides four comprehensive analysis modes:

1. **Dose Metrics Evaluation**: Quantifies dose heterogeneity and calculates comprehensive metrics.

2. **1D Central Axis Profiling**: Provides central-axis dose profiling with automated tumor boundary detection for rapid dose assessment along the beam axis.

3. **2D Multi-Planar Visualization**: Displays beam's-eye-view dose distributions with interactive depth navigation through all relevant slices, enabling detailed examination of dose patterns at any depth.

4. **3D Volumetric P/V Analysis**: Performs volumetric peak-valley clustering that identifies distinct peak and valley clusters in clinical SFRT plans, with full 3D visualization of dose heterogeneity.

#### Novel Onion-Layer Analysis

The toolkit features innovative onion-layer analysis that divides target structures into five concentric shells from core to surface, enabling characterization of radial dose heterogeneity patterns. This analysis reveals how dose varies from the tumor center to periphery, providing critical insights for SFRT plan evaluation.

#### Data Export

All metrics are automatically exported to CSV format including:
- Comprehensive biological metrics
- Individual cluster properties with spatial coordinates
- Dose statistics suitable for statistical analysis and outcome correlation studies
- Quality assurance data for multi-institutional outcome studies

<img width="734" height="364" alt="image" src="https://github.com/user-attachments/assets/60f719f2-fdce-43b7-8d8d-d2448cec39c3" />

Note: This module is still experimental and in validation.

---

## Upcoming Features

1. Improved PVDR analysis.
2. Improved optimization with auto planning feature.
   
---
## Installation

1. Clone or download this repository.
2. Build the solution (`MAAS-SFRThelper.sln`) in Visual Studio using an ESAPI-compatible profile.
3. Place the compiled plugin DLL into the Eclipse plugins folder.
4. Launch Eclipse and load the plugin from the scripts menu.

---

## System Requirements

- Varian Eclipse version 15.6 or later
- Valid ESAPI scripting license
- Windows 10 or later

---

## License

This project is licensed under the Varian Limited Use Software License Agreement (LUSLA). See the license.txt file for details.

---

## Special Recognition for Contribution

- Pierre Lansonneur – Original research and prototyping of CVT3D algorithms
- Ilias Sachpazidis – C# CVT3D implementation adapted from 2D open-source generator (https://github.com/isachpaz/CVTGenerator)

## Publications
- https://aapm.confex.com/aapm/2025scm/meetingapp.cgi/Paper/14084

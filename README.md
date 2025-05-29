
# MAAS-SFRThelper

Tools to aid in the creation of structure patterns (spheres) or irregular stuctures (straight and angled rods) which can be evaluated or aid in the creation of treatment plans with the intention of not covering traditional PTVs homogeneously.  Spacially Fractionated Radiation Therapy aims to create hetrogenious dose distrubutions with intentionally large peak to valley ratios while underdosing a significant portion of target volume compared with evaluating a traditional radiotherapy PTV.

---

## Screenshot

![image](https://github.com/user-attachments/assets/5bc58395-8f92-40f1-ba06-cee08dc40013)


---

## Features 

### SphereLattice Tab

The SphereLattice tab is used to create spherical high-dose boost regions within a selected PTV. These spheres are arranged in a structured 3D pattern designed to create dose heterogeneity.

Options available:

- Target: Dropdown to select the target structure where the sphere lattice will be generated.
- Pattern:
  - Hexagonal Closest Packed (HCP)
  - Simple Cubic (SCP)
  - Centroidal Voronoi Tessellation in 3D (CVT3D)
- Radius (mm): Defines the radius of each sphere.
- Spacing (mm): Defines center-to-center spacing between spheres.
- Lattice X / Y Shift (mm): Allows you to laterally shift the lattice in the transverse plane.
- Lateral Spacing Factor: A multiplier for spacing in the X/Y direction.
- Full Spheres Only (slider): By default, only full spheres are included. Moving the slider left allows cropped spheres to be included near the edges.
- Create spheres as single structure only: Reduces structure count by merging all spheres into one ROI.
- Create void structures: Generates complementary voids (valleys) to guide dose distribution.

### SCART Tab

The SCART (Stereotactic Central Ablative Radiation Therapy) tab enables generation of a central high-dose boost volume (SCART Treatment Volume, or STV) within a selected gross tumor volume (GTV). This is based on the SCART technique which focuses ablative doses to the hypoxic tumor core while delivering lower peripheral doses. Users can define an inner STV within a selected GTV structure which can then be used in treatment planning systems to prescribe a higher dose compared to surrounding tissue.

Options available:

- Select GTV: Choose a structure representing the gross tumor volume.
- Define STV Parameters: Specify percentage reduction or fixed margin from GTV to generate the STV.
- Generate STV Structure: Creates a structure in the Eclipse workspace that can be used for dose painting or boost prescriptions.
- Compatibility: Can be used with VMAT or CyberKnife-based planning.

Note: The SCART tab reflects implementation aligned with emerging clinical research but is not validated for clinical use. For more background, refer to peer-reviewed studies describing SCART dosimetry and outcomes.

### RapidRods Tab

The RapidRods tab supports creation of cylindrical rod-shaped dose structures. These are intended for experimental or directional SFRT planning techniques.

- Generates straight or angled rods inside selected PTVs.
- Rod axis definition and advanced geometry options are under development.
- Additional parameters like rod length, diameter, and spacing will be supported in future updates.

Note: This module is still experimental and in validation.

### Evaluation Tab

The Evaluation tab is currently under development. It will provide analysis tools and additional functionality for evaluating or optimizing created structures.

## Planned functionality includes:

- Peak-to-Valley Ratio (PVR) analysis:
  - Quantify dose inhomogeneity across high-dose peaks and low-dose valleys.
  - Evaluate inhomogeneity index (e.g., max-D95%, min-D5%).
- Automatic Dose Optimization:
  - Load optimization objectives for generated peak/valley structures.
  - Support manual normal tissue objective integration.

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

## Special Recognition for contribution

- Pierre Lansonneur – Original research and prototyping of CVT3D algorithms.
- Ilias Sachpazidis – C# CVT3D implementation adapted from 2D open-source generator (https://github.com/isachpaz/CVTGenerator)

## Publications
- https://aapm.confex.com/aapm/2025scm/meetingapp.cgi/Paper/14084

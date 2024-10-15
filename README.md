# MAAS-SFRThelper
Tools to aid in the creation of structure patterns (spheres) or irregular stuctures (straight and angled rods) which can be evaluated or aid in the creation of treatment plans with the intention of not covering traditional PTVs homogeneously.  Spacially Fractionated Radiation Therapy aims to create hetrogenious dose distrubutions with intentionally large peak to valley ratios while underdosing a significant portion of target volume compared with evaluating a traditional radiotherapy PTV.

![image](https://github.com/user-attachments/assets/f6108613-068e-4d02-90f9-401ce18968e2)
## SphereLattice
Features
* Target structure to place sphere lattice within
  * Only PTVs selectable
* 3 pattern options
  * Hexagonal Close Packed (HCP)
  * Simple Cubic Packed (SCP)
    * square/rectangular pattern
  * Centroidal Voronoi Tessellation in 3D (CVT3D)
* Selectable sphere radius
* Selectable sphere spacing
  * spacing only strictly enforced for HCP and SCP
  * CVT3D uses spacing only for initial number of spheres (from HCP), final distance based on target shape and number of spheres to fit
* Selectable sphere lattice position shift in X and Y
  * shift only strictly enforced for HCP and SCP
  * CVT3D shifts only help determine initial number of spheres (from HCP), final position based on target shape and number of spheres to fit
* Slider for determining how close spheres can be to the edges of the selected Target
  * default is full spheres only (slider to the right)
  * Full spheres only creates an intermargin to ensure no sphere is partial (cropped against edge of PTV)
  * 99% or less (left slider positions) will allow more spheres to be placed but outer spheres my be cropped
* Lateral spacing factor
  * a multiplier for lateral spacing (x y direction)
  * 1 = off, 1.1 and up accepted
  * example: 2 = 2x the spacing between spheres laterally, vs spacing in the head to foot direction
  * larger lateral spacing could increase peak to valley ratio in coplanar VMAT delivery
  * not currently supported with CVT3D

Features planned
* display volume of selected target in lower console output
* display total sphere volume and % (divided by target volume) in Message Box (and console) with OK/cancel option 
* nulls+voids structure creation support
  * 3 levels
    * nulls+voids_contigious (ring / shell with a margin around the inter portion of the lattice)
    * nulls+voids (legnth similar to sphere radius)
    * nulls+voids_core (small, inner portion only)
* Peak to Valley Ratio (PVR) evaluation tab
  * quantify PVR reproducibly
  * inhomogeneity index between spheres (max-D95%) and nulls+voids (min-D5%)
* Automatic Dose Optimization tab
  * Preloads and starts optimizer with objectives on spheres, nulls+voids structures and manual NTO

Special Recognition for contributions<br>
[Pierre Lansonneur](https://www.linkedin.com/in/pierre-lansonneur-87141111b/) [ for original Centroidal Voronoi Tessellation in 3D research and prototyping<br>
[Ilias Sachpazidis](https://www.sachpazidis.com/) [isachpaz](https://github.com/isachpaz) for CVT3D C# implementation adapted from [previous 2D implementation](https://www.sachpazidis.com/cvt-space-partitioning/) and open source example [CVTGenerator](https://github.com/isachpaz/CVTGenerator)  

## RapidRods
* further testing needed

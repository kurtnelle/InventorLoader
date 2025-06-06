# InventorLoader
Loads/Imports: Autodesk (R) Inventor (R) files into FreeCAD (v0.18 or greater).
Until now only Parts (IPT) but not assemblies (IAM) or drawings (IDW) or presentations (IPN) can be displayed.

As Fusion360 files contains a complete ACIS model representation these files can also be opened in FreeCAD.
## Status:
**Released 1.5**

## Screenshots
[Demo-Status](https://github.com/jmplonka/InventorLoader/tree/master/Demo-Status/) subdirectory shows examples of this Addon.

## Limitations
Export will not be supported - neither IPT nor SAT/SAB or DXF.
Only files from INVENTOR V2010 or newer are supported.

### Feature Based Import

### ACIS (`sat`) Native Import
- Blending surfaces are not yet supported.
- Helix surfaces are not yet supported for lines.
- Interpolated curves and surfaces defined by laws are omitted if they don't have spline data.

### STEP Conversion Import
STEP converts the ACIS data from SAT or IPT files. Therefore any limitation is inherited.

Autodesk Inventor files have OLE2 files.
This allows embedding Excel workbooks e.g.:
* The addon is able to read Inventor files from 2010 or newer.
* Read the iProperties (Note: only a few can be applied in FreeCAD).
* Display embedded workbooks as a new spreadsheet when importing as features.

### DXF import
DXF files contains sometimes 3D-Solids. These are represented as SAT/SAB content.
The solids can be imported either using native of STEP conversion.

## History
**1.3**    (2021-03-09): Added support for Fusion360 files.
**1.2**    (2021-02-28): Added support for Inventor 2021 files.
**1.1**    (2020-05-04): Added importing of 3D-Solids from DXF files.
**1.0.1**  (2020-04-10): Fixed finding of SAB import.
**1.0.0**  (2019-08-26): Reorganized section readers (1.0).

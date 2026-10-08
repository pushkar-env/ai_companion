# Editable Alita wardrobe sources

AlitaWardrobe.blend was created in the connected interactive Blender 5.2.2 process.
Original owner-supplied Alita and Cosmos sources are preserved in fitting-reference
collections; garments are in Wardrobe garments. It contains a fitted tee and denim
shorts, plus a shell and midi skirt derived from the original dress. Source exports
in this directory retain named weights, UVs and smooth normals, before Unity handedness
conversion. Dress.json is the source-coordinate calibration reference.

After edits, use the live Blender workflow to run tools/export-wardrobe.py. Then run
Companion.Editor.WardrobeImport.Run in the stopped Unity Editor. It updates derived
Resources/Wardrobe meshes/materials in place, preserving their GUIDs. Do not import
against a Play-mode posed skeleton. Validate all four combinations with WardrobeReview
and live UI with WardrobeUiChecks. Only this exact Alita body is supported.

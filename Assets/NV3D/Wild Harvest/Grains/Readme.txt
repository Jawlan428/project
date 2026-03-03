// Wild Harvest: Grains // Unity 6 //


-- DEMO INSTRUCTIONS --

Click crops to trigger growth + harvest.

Move cube into crops to trigger growth + harvest.

A Timer will also trigger growth + harvest after 10s.

Click soil to toggle wet/dry.


-- NOTES --

- Package setup for built-in render pipeline by default. From Unity 6 onwards URP will be supported by default with BiRP and HDRP packages included.

- If using URP or HDRP make sure to swap to the correct shaders by importing the corrosponding package in 'Shared'.

Note: For HDRP projects some setup on the material will be required, such as tweaking colour values, setting double sided and alpha clip to an appropriate level. 

- Crop growth and spawn controlled by either script graphs or C# scripts.  

- In demo level simply play scene and click on a bush to trigger the growth stages. You can also click the soil to toggle between dry and wet.

- Soil uses the red channel on the vertex colours to define wetness area when toggled on.

- Grain crops have modified vertex normals as such it is better to have "mirror vertex normals" set to "off" in the materials. You can revert to face normals by forcing the mesh to recalculate normals in the import settings then enabling "mirror vertex normals" back on the material.


Foliage Masks Breakdown:

Mask texture
R = ID mask for Stem (~0.25), flower (~0.5) and fruit (~0.75) tints.
G = Translucency (simple boost to emission)
B = AO 
A = Smoothness

Vertex Color
R = AO
G = Leaf variation (Height based with some noise)
B = Empty
A = Empty

Shaders made with Amplify Shader Editor. 




Thank you for purchasing Wild Harvest: Root Vegetables, please consider leaving a review and checking out my other packs!

https://assetstore.unity.com/publishers/314

For questions and queries please contact me here -->  matt.nv3d@gmail.com  <--
// For questions and queries please contact me here: matt.nv3d@gmail.com //
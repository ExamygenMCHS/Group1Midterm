
Unity URP and HDRP projects require shaders built for that specific render pipeline otherwise materials will render in pink.

The EasyRoads3D Pro package includes separate material packages for URP and HDRP projects. These packages are located in:

/Assets/EasyRoads3D/SRP Support packages/ 

After importing the EasyRoads3D package the required URP or HDRP package should auto import from the above mentioned folder.

Should this not happen than there will not be any shader present in the folder /Assets/EasyRoads3D/Shaders/. In that case the required URP or HDRP material package should auto import after opening a new scene. 

If all fails than the respective package can be imported manually from /Assets/EasyRoads3D/SRP Support Packages/. The required package is the one that matches the URP resp HDRP version in the project or the nearest version released before the version in the project because new packages will be added when required, which is after critical changes in new URP or HDRP versions.  

Please contact us should you still have material issues after that.

Forum: https://discussions.unity.com/t/easyroads3d-v3-the-upcoming-new-road-system-part-2/
Website: https://www.easyroads3d.com
Support: info@easyroads3d.com

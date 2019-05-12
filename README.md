# XBC2ModelDecomp
A reformatted fork of a [project created by daemon1](https://forum.xentax.com/viewtopic.php?f=16&t=18087) on the XeNTaX forums. All credit to them and to [PredatorCZ/Lukas Cone](https://lukascone.wordpress.com/2018/05/06/xenoblade-chronicles-import-tool/) for publicizing tools to reverse the game's various model formats. Special thanks to Turk645 as well for working on the map formats and for bouncing ideas back and forth with.
## Features
* Dump model meshes, bones, and flexes to XNALara ascii or glTF (but with no bones)
* Dump textures from .wismt
* Dump animations
* Dumps raw files from map files
## Running
Simply run the executable, and pick an input file. The only supported filetypes (at the time of writing) are \*.wimdo for models and \*.wismda for maps. An output folder will automatically be selected for the file(s) you choose, but you can override this by picking a output folder manually. Configure your output settings at the bottom and hit Extract. If you are using Blender, it does not natively support the XNALara format; I recommend [johnzero7's plugin.](https://github.com/johnzero7/XNALaraMesh) Many other plugins exist for other modeling tools.
## Compiling
Every NuGet package should be included in the solution, but if for whatever reason they are missing, download the latest [zlib](https://www.nuget.org/packages/zlib.net/1.0.4) (model decompression), [GitInfo](https://github.com/kzu/GitInfo) (commit hash in title), [SharpGLTF](https://github.com/vpenades/SharpGLTF) (glTF export), and [WindowsAPICodePack-Shell](https://github.com/contre/Windows-API-Code-Pack-1.1) (non-infuriating folder selection.)

I've personally used Visual Studio 2017 to write the majority of this, but VS2019 works as well.
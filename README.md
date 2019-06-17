# XBC2ModelDecomp
A reformatted fork of a [project created by daemon1](https://forum.xentax.com/viewtopic.php?f=16&t=18087) on the XeNTaX forums. All credit to them and to [PredatorCZ/Lukas Cone](https://lukascone.wordpress.com/2018/05/06/xenoblade-chronicles-import-tool/) for publicizing tools to reverse the game's various model formats. Special thanks to Turk645 as well for working on the map formats and for bouncing ideas back and forth with.

## Features
* Dump models (including bones and flexes) to XNALara ascii or glTF
* Dump maps (including props) to XNALara ascii or glTF
* Save specified LOD values for both props and maps
* Dump all textures from files (including mesh textures/main chunk textures)
* Dumps raw files and animations for research

## Running
Simply run the executable, and pick an input file. An output folder will be created in the path you choose your file(s) in, but you can override this by picking a output folder manually. Each file will have its own folder in the output folder. Then, configure your output settings at the bottom and hit Extract. The file should export to the output path in the format you chose.

If you are using Blender, it does not natively support the XNALara format; I recommend [johnzero7's plugin,](https://github.com/johnzero7/XNALaraMesh) however many other plugins exist for other modeling programs. The glTF format is also [open source](https://github.com/KhronosGroup/glTF) and used by many modeling tools and game engines. However, the current version of Blender does not support bones or facial flexes, so I have not yet implemented them into the tool.

## Compiling
Every NuGet package should be included in the solution, but if for whatever reason they are missing, download the latest [zlib](https://www.nuget.org/packages/zlib.net/1.0.4) (model decompression), [GitInfo](https://github.com/kzu/GitInfo) (commit hash in title), [SharpGLTF](https://github.com/vpenades/SharpGLTF) (glTF export), and [WindowsAPICodePack-Shell](https://github.com/contre/Windows-API-Code-Pack-1.1) (non-infuriating folder selection.)

I've personally used Visual Studio 2017 to write the majority of this, but Visual Studio 2019 works as well.
# XBC2ModelDecomp
A reformatted fork of a [project created by daemon1](https://forum.xentax.com/viewtopic.php?f=16&t=18087) on the XeNTaX forums. All credit to them for reversing the format.
## Compiling
Other than the zlib library, everything should be stock System packages.
## Running
It's a bit janky at the moment, but the general format is `[filename] <flex distance>`. Flex distance is the offset of every flex mesh in the final file. I would reccomend using a distance of 0, otherwise you would have to go and manually reposition every flex mesh. (It's not fun.)
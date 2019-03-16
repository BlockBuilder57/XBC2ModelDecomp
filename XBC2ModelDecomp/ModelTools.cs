using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using zlib;

namespace XBC2ModelDecomp
{
    class ModelTools
    {
        FormatTools ft = new FormatTools();

        public ModelTools(string[] args)
        {
            //wismt
            FileStream fsWISMT = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + ".wismt", FileMode.Open, FileAccess.Read);
            BinaryReader brWISMT = new BinaryReader(fsWISMT);

            fsWISMT.Seek(12L, SeekOrigin.Current);
            int offsetMain = brWISMT.ReadInt32(); //0xC 0x10, 16
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            int num3 = brWISMT.ReadInt32(); //0x18 0x10, 16
            int num4 = brWISMT.ReadInt32(); //0x1C 0x4C, 76
            int fileCount = brWISMT.ReadInt32(); //0x20 0xE, 14
            int num6 = brWISMT.ReadInt32(); //0x24 0x18C, 396
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            brWISMT.ReadInt32();
            int textureIdCount = brWISMT.ReadInt32(); //0x44
            int offsetTextureIds = brWISMT.ReadInt32(); //0x48 0x234, 564
            int offsetTextureCount = brWISMT.ReadInt32(); //0x4C 0x24E, 590
            fsWISMT.Seek(offsetMain + offsetTextureIds, SeekOrigin.Begin); //0x244, 580

            MemoryStream msCurFile = new MemoryStream();

            if (textureIdCount > 0 && offsetTextureCount > 0)
            {
                int[] textureIds = new int[textureIdCount];
                for (int curTextureId = 0; curTextureId < textureIdCount; curTextureId++)
                {
                    textureIds[curTextureId] = (int)brWISMT.ReadInt16();
                }

                fsWISMT.Seek(offsetMain + offsetTextureCount, SeekOrigin.Begin); //0x25E, 606
                int textureCount = brWISMT.ReadInt32(); //0x25E
                brWISMT.ReadInt32();
                brWISMT.ReadInt32();
                brWISMT.ReadInt32();

                int[] textureNameOffsets = new int[textureCount];
                for (int curTextureNameOffset = 0; curTextureNameOffset < textureCount; curTextureNameOffset++)
                {
                    brWISMT.ReadInt32();
                    brWISMT.ReadInt32();
                    brWISMT.ReadInt32();
                    textureNameOffsets[curTextureNameOffset] = brWISMT.ReadInt32();
                }

                string[] textureNames = new string[textureCount];
                for (int curTextureName = 0; curTextureName < textureCount; curTextureName++)
                {
                    fsWISMT.Seek(offsetMain + offsetTextureCount + textureNameOffsets[curTextureName], SeekOrigin.Begin);
                    textureNames[curTextureName] = FormatTools.ReadNullTerminatedString(brWISMT);
                }

                int[] someVarietyOfPointer = new int[num3]; //16 elements
                fsWISMT.Seek((long)(offsetMain + num4), SeekOrigin.Begin); //0x5C, 92
                for (int i = 0; i < num3; i++)
                {
                    someVarietyOfPointer[i] = brWISMT.ReadInt32() + brWISMT.ReadInt32();
                    brWISMT.ReadInt32();
                    brWISMT.ReadInt32();
                    brWISMT.ReadInt32();
                }

                int[] fileOffsets = new int[fileCount];
                int[] fileSizes = new int[fileCount];
                fsWISMT.Seek((long)(offsetMain + num6), SeekOrigin.Begin);
                for (int curFileOffset = 0; curFileOffset < fileCount; curFileOffset++)
                {
                    fileSizes[curFileOffset] = brWISMT.ReadInt32();
                    brWISMT.ReadInt32();
                    fileOffsets[curFileOffset] = brWISMT.ReadInt32();
                }

                string texturesFolderPath = Path.GetFileNameWithoutExtension(args[0]) + "_textures";
                if (!Directory.Exists(texturesFolderPath))
                    Directory.CreateDirectory(texturesFolderPath);
                ft.ReadTextures(fsWISMT, brWISMT, texturesFolderPath, fileOffsets, textureIdCount, someVarietyOfPointer, textureNames, textureIds);

                msCurFile = ft.XBC1(fsWISMT, brWISMT, fileOffsets[0]/*, "meshfile.bin", Path.GetFileNameWithoutExtension(args[0]) + "_files"*/);
            }



            //start mesh file
            
            if (!(textureIdCount > 0 && offsetTextureCount > 0))
                msCurFile = ft.XBC1(fsWISMT, brWISMT, offsetMain + offsetTextureIds/*, "meshfile.bin", Path.GetFileNameWithoutExtension(args[0]) + "_files"*/);
            if (msCurFile != null)
            {
                BinaryReader brCurFile = new BinaryReader(msCurFile); //start new file
                ft.ModelToASCII(msCurFile, brCurFile, args);
            } else
            {
                Console.WriteLine("mesh pointer wrong!");
            }
            
        }
    }
}

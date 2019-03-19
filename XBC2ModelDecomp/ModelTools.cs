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

            int DRSMMagic = brWISMT.ReadInt32();
            if (DRSMMagic != 0x4D535244)
            {
                Console.WriteLine("wismt is corrupt!");
                return;
            }

            Structs.DRSM DRSM = new Structs.DRSM
            {
                Version = brWISMT.ReadInt32(),
                HeaderSize = brWISMT.ReadInt32(),
                MainOffset = brWISMT.ReadInt32(), //0xC 0x10, 16

                Tag = brWISMT.ReadInt32(),
                Revision = brWISMT.ReadInt32(),

                DataItemsCount = brWISMT.ReadInt32(), //0x18 0x10, 16
                DataItemsOffset = brWISMT.ReadInt32(), //0x1C 0x4C, 76
                FileCount = brWISMT.ReadInt32(), //0x20 0xE, 14
                TOCOffset = brWISMT.ReadInt32(), //0x24 0x18C, 396

                Unknown1 = brWISMT.ReadBytes(28),

                TextureIdsCount = brWISMT.ReadInt32(), //0x44
                TextureIdsOffset = brWISMT.ReadInt32(), //0x48 0x234, 564
                TextureCountOffset = brWISMT.ReadInt32() //0x4C 0x24E, 590
            };

            DRSM.DataItems = new Structs.DRSMDataItem[DRSM.DataItemsCount];
            fsWISMT.Seek(DRSM.MainOffset + DRSM.DataItemsOffset, SeekOrigin.Begin); //0x5C, 92
            for (int i = 0; i < DRSM.DataItemsCount; i++)
            {
                DRSM.DataItems[i] = new Structs.DRSMDataItem
                {
                    Offset = brWISMT.ReadInt32(),
                    Size = brWISMT.ReadInt32(),
                    id1 = brWISMT.ReadInt16(),
                    Type = (Structs.DRSMDataItemTypes)brWISMT.ReadInt16()
                };
                fsWISMT.Seek(0x8, SeekOrigin.Current);
            }

            MemoryStream msCurFile = new MemoryStream();

            if (DRSM.TextureIdsCount > 0 && DRSM.TextureCountOffset > 0)
            {
                fsWISMT.Seek(DRSM.MainOffset + DRSM.TextureIdsOffset, SeekOrigin.Begin); //0x244, 580
                DRSM.TextureIds = new short[DRSM.TextureIdsCount];
                for (int curTextureId = 0; curTextureId < DRSM.TextureIdsCount; curTextureId++)
                {
                    DRSM.TextureIds[curTextureId] = brWISMT.ReadInt16();
                }

                fsWISMT.Seek(DRSM.MainOffset + DRSM.TextureCountOffset, SeekOrigin.Begin); //0x25E, 606
                DRSM.TextureCount = brWISMT.ReadInt32(); //0x25E
                DRSM.TextureChunkSize = brWISMT.ReadInt32();
                DRSM.Unknown2 = brWISMT.ReadInt32();
                DRSM.TextureStringBufferOffset = brWISMT.ReadInt32();

                DRSM.TextureInfo = new Structs.DRSMTextureInfo[DRSM.TextureCount];
                for (int curTextureNameOffset = 0; curTextureNameOffset < DRSM.TextureCount; curTextureNameOffset++)
                {
                    DRSM.TextureInfo[curTextureNameOffset].Unknown1 = brWISMT.ReadInt32();
                    DRSM.TextureInfo[curTextureNameOffset].Size = brWISMT.ReadInt32();
                    DRSM.TextureInfo[curTextureNameOffset].Offset = brWISMT.ReadInt32();
                    DRSM.TextureInfo[curTextureNameOffset].StringOffset = brWISMT.ReadInt32();
                }

                DRSM.TextureNames = new string[DRSM.TextureCount];
                for (int curTextureName = 0; curTextureName < DRSM.TextureCount; curTextureName++)
                {
                    fsWISMT.Seek(DRSM.MainOffset + DRSM.TextureCountOffset + DRSM.TextureInfo[curTextureName].StringOffset, SeekOrigin.Begin);
                    DRSM.TextureNames[curTextureName] = FormatTools.ReadNullTerminatedString(brWISMT);
                }

                DRSM.TOC = new Structs.TOC[DRSM.FileCount];
                fsWISMT.Seek(DRSM.MainOffset + DRSM.TOCOffset, SeekOrigin.Begin);
                for (int curFileOffset = 0; curFileOffset < DRSM.FileCount; curFileOffset++)
                {
                    DRSM.TOC[curFileOffset].CompSize = brWISMT.ReadInt32();
                    DRSM.TOC[curFileOffset].FileSize = brWISMT.ReadInt32();
                    DRSM.TOC[curFileOffset].Offset = brWISMT.ReadInt32();
                }

                string texturesFolderPath = Path.GetFileNameWithoutExtension(args[0]) + "_textures";
                if (!Directory.Exists(texturesFolderPath))
                    Directory.CreateDirectory(texturesFolderPath);
                ft.ReadTextures(fsWISMT, brWISMT, DRSM, texturesFolderPath);

                msCurFile = ft.XBC1(fsWISMT, brWISMT, DRSM.TOC[0].Offset/*, "meshfile.bin", Path.GetFileNameWithoutExtension(args[0]) + "_files"*/);
            }

            //start mesh file
            if (msCurFile != null)
            {
                BinaryReader brCurFile = new BinaryReader(msCurFile); //start new file
                ft.ModelToASCII(msCurFile, brCurFile, args);
            }
            else
                Console.WriteLine("mesh pointer wrong!");
        }
    }
}

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

        public ModelTools(string path)
        {
            App.PushLog($"Reading {Path.GetFileName(path)}...");

            //wismt
            FileStream fsWISMT = new FileStream($@"{new FileInfo(path).DirectoryName}\{Path.GetFileNameWithoutExtension(path) + ".wismt"}", FileMode.Open, FileAccess.Read);
            BinaryReader brWISMT = new BinaryReader(fsWISMT);

            Structs.MSRD MSRD = ft.ReadMSRD(fsWISMT, brWISMT);

            string texturesFolderPath = App.OutputPath + @"\Textures";
            if (!Directory.Exists(texturesFolderPath))
                Directory.CreateDirectory(texturesFolderPath);
            ft.ReadTextures(fsWISMT, brWISMT, MSRD, texturesFolderPath);

            //start mesh file
            if (MSRD.TOC.Length > 0)
            {
                BinaryReader brCurFile = new BinaryReader(MSRD.TOC[0].MemoryStream); //start new file
                ft.ModelToASCII(MSRD.TOC[0].MemoryStream, brCurFile, path);
            }
        }

        
    }
}

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

        public ModelTools()
        {
            App.PushLog($"Reading {Path.GetFileName(App.CurFilePath)}...");

            //wismt
            FileStream fsWISMT = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".wismt", FileMode.Open, FileAccess.Read);
            BinaryReader brWISMT = new BinaryReader(fsWISMT);

            Structs.MSRD MSRD = ft.ReadMSRD(fsWISMT, brWISMT);

            if (App.ExportAnims && File.Exists(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".mot"))
            {
                FileStream fsMOT = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".mot", FileMode.Open, FileAccess.Read);
                BinaryReader brMOT = new BinaryReader(fsMOT);

                Structs.SAR1 SAR1 = ft.ReadSAR1(fsMOT, brMOT, @"\Animations\", App.ExportAnims);

                brMOT.Dispose();
                fsMOT.Dispose();
            }

            //start mesh file
            if (MSRD.TOC.Length > 0)
            {
                if (!Directory.Exists(App.CurOutputPath))
                    Directory.CreateDirectory(App.CurOutputPath);

                if (MSRD.TOC.Length > 1 && App.ExportTextures)
                {
                    string texturesFolderPath = App.CurOutputPath + @"\Textures";
                    if (!Directory.Exists(texturesFolderPath))
                        Directory.CreateDirectory(texturesFolderPath);
                    ft.ReadTextures(fsWISMT, brWISMT, MSRD, texturesFolderPath);
                }

                BinaryReader brCurFile = new BinaryReader(MSRD.TOC[0].Data); //start new file
                ft.ModelToASCII(MSRD.TOC[0].Data, brCurFile, App.CurFilePath);

                App.PushLog($"Finished {Path.GetFileName(App.CurFilePath)}!\n");
            }
            else
            {
                App.PushLog($"No files found in {Path.GetFileName(App.CurFilePath)}?\n");
            }

            brWISMT.Dispose();
            fsWISMT.Dispose();
        }
    }
}

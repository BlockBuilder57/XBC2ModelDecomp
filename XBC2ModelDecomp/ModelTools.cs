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
                    ft.ReadTextures(MSRD, texturesFolderPath);
                }

                BinaryReader brCurFile = new BinaryReader(MSRD.TOC[0].Data); //start new file

                Structs.Mesh Mesh = ft.ReadMesh(MSRD.TOC[0].Data, brCurFile);

                if (!File.Exists(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".wimdo"))
                    return;
                if (!File.Exists(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".arc"))
                    return;

                #region WIMDOReading
                FileStream fsWIMDO = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".wimdo", FileMode.Open, FileAccess.Read);
                BinaryReader brWIMDO = new BinaryReader(fsWIMDO);

                Structs.MXMD MXMD = ft.ReadMXMD(fsWIMDO, brWIMDO);
                #endregion WIMDOReading

                #region ARCReading
                FileStream fsARC = new FileStream(App.CurFilePath.Remove(App.CurFilePath.LastIndexOf('.')) + ".arc", FileMode.Open, FileAccess.Read);
                BinaryReader brARC = new BinaryReader(fsARC);

                Structs.SAR1 SAR1 = ft.ReadSAR1(fsARC, brARC, @"\RawFiles\", App.SaveRawFiles);
                BinaryReader brSKEL = new BinaryReader(SAR1.ItemBySearch(".skl").Data);
                Structs.SKEL SKEL = ft.ReadSKEL(brSKEL.BaseStream, brSKEL);
                #endregion ARCReading

                if (App.ExportFormat == Structs.ExportFormat.XNALara)
                    ft.ModelToASCII(Mesh, MXMD, SKEL);
                else
                    ft.ModelToGLTF(Mesh, MXMD, SKEL);

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

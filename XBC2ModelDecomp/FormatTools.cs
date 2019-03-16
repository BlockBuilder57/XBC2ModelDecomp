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
    public class FormatTools
    {
        public static string ReadNullTerminatedString(BinaryReader br)
        {
            string text = "";
            byte b;
            while ((b = br.ReadByte()) > 0)
            {
                text += (char)b;
            }
            return text;
        }

        public MemoryStream XBC1(FileStream fileStream, BinaryReader binaryReader, int offset, string saveToFileName = "", string savetoFilePath = "")
        {
            fileStream.Seek(offset, SeekOrigin.Begin);
            int XBC1Magic = binaryReader.ReadInt32(); //nice meme
            if (XBC1Magic != 0x31636278)
                return null;
            binaryReader.ReadInt32();
            int outputFileSize = binaryReader.ReadInt32();
            int compressedLength = binaryReader.ReadInt32();
            binaryReader.ReadInt32();

            //string fileInfo = ReadNullTerminatedString(binaryReader);

            fileStream.Seek(offset + 0x30, SeekOrigin.Begin);
            byte[] fileBuffer = new byte[outputFileSize >= compressedLength ? outputFileSize : compressedLength];

            MemoryStream msFile = new MemoryStream();
            fileStream.Read(fileBuffer, 0, compressedLength);

            ZOutputStream ZOutFile = new ZOutputStream(msFile);
            ZOutFile.Write(fileBuffer, 0, compressedLength);
            ZOutFile.Flush();

            if (!string.IsNullOrWhiteSpace(saveToFileName))
            {
                if (!string.IsNullOrWhiteSpace(savetoFilePath) && !Directory.Exists(savetoFilePath))
                    Directory.CreateDirectory(savetoFilePath);
                FileStream outputter = new FileStream($@"{savetoFilePath}\{saveToFileName}", FileMode.OpenOrCreate);
                msFile.WriteTo(outputter);
                outputter.Flush();
                outputter.Close();
            }

            msFile.Seek(0L, SeekOrigin.Begin);
            return msFile;
        }

        public void ModelToASCII(MemoryStream memoryStream, BinaryReader binaryReader, string[] args)
        {
            float exportFlexesDistance = 0f;
            bool exportFlexes = false;
            if (args.Length > 1)
            {
                exportFlexesDistance = Convert.ToSingle(args[1]);
                exportFlexes = true;
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            
            int i = 0;
            int meshPointersPointer = binaryReader.ReadInt32(); //0x0
            int meshCount = binaryReader.ReadInt32(); //0x4
            int meshDataPointer = binaryReader.ReadInt32(); //0x8
            int meshCountWithFlexes = binaryReader.ReadInt32(); //0xC

            memoryStream.Seek(0x18, SeekOrigin.Current);
            int num16 = binaryReader.ReadInt32(); //0x28
            binaryReader.ReadInt32(); //0x2C
            int meshDataStart = binaryReader.ReadInt32(); //0x30 4096
            binaryReader.ReadInt32(); //0x34
            int meshExtraDataPointer = binaryReader.ReadInt32(); //0x38 496

            //no clue
            int num19 = 0;
            int num20 = 0;
            int[] array6 = null;
            int[] array7 = null;
            int[] array8 = null;
            if (num16 > 0) //flex related?
            {
                memoryStream.Seek((long)num16, SeekOrigin.Begin);
                num19 = binaryReader.ReadInt32();
                int num21 = binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                num20 = binaryReader.ReadInt32();
                memoryStream.Seek((long)num21, SeekOrigin.Begin);
                array6 = new int[num19];
                array7 = new int[num19];
                array8 = new int[num19];
                for (i = 0; i < num19; i++)
                {
                    array6[i] = binaryReader.ReadInt32();
                    array7[i] = binaryReader.ReadInt32();
                    array8[i] = binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                    binaryReader.ReadInt32();
                }
            }

            int[] meshesPointers = new int[meshCount];
            int[] array10 = new int[meshCount];
            int[] meshesDataCount = new int[meshCount];
            int[] array12 = new int[meshCount];
            int[] array13 = new int[meshCount];
            int[] array14 = new int[meshCount];
            memoryStream.Seek(meshPointersPointer, SeekOrigin.Begin); //0x50
            for (i = 0; i < meshCount; i++)
            {
                meshesPointers[i] = meshDataStart + binaryReader.ReadInt32();
                meshesDataCount[i] = binaryReader.ReadInt32(); //608???
                array12[i] = binaryReader.ReadInt32(); //36
                array13[i] = binaryReader.ReadInt32(); //388
                array14[i] = binaryReader.ReadInt32(); //6
                memoryStream.Seek(12L, SeekOrigin.Current);
            }

            int[] meshesDataStart = new int[meshCountWithFlexes];
            int[] meshesVertexCount = new int[meshCountWithFlexes]; //5
            memoryStream.Seek(meshDataPointer, SeekOrigin.Begin); //0xF0 80
            for (i = 0; i < meshCountWithFlexes; i++)
            {
                meshesDataStart[i] = meshDataStart + binaryReader.ReadInt32();
                meshesVertexCount[i] = binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                binaryReader.ReadInt32();
            }

            memoryStream.Seek(meshExtraDataPointer + 0x8, SeekOrigin.Begin);
            int meshWeightBoneCount = (int)binaryReader.ReadInt16(); //4
            int[,] meshWeightIds = new int[meshesDataCount[meshWeightBoneCount], 4];
            float[,] meshWeightValues = new float[meshesDataCount[meshWeightBoneCount], 4];
            Vector3[][] meshVertices = new Vector3[meshCount][];
            Vector3[][] meshNormals = new Vector3[meshCount][];
            float[][,] meshFlexesX = new float[meshCount][,];
            float[][,] meshFlexesY = new float[meshCount][,];
            int[][] meshWeights = new int[meshCount][];
            int[] meshUVLayers = new int[meshCount];

            bool WIMDOExists = false;
            bool ARCExists = false;
            if (File.Exists(Path.GetFileNameWithoutExtension(args[0]) + ".wimdo"))
                WIMDOExists = true;
            if (File.Exists(Path.GetFileNameWithoutExtension(args[0]) + ".arc"))
                ARCExists = true;

            //might be material data (and flexes?)
            string[] meshFlexNames = new string[0];
            int num30 = 0;
            Dictionary<int, string> dictionary = new Dictionary<int, string>();
            int[] array27 = new int[num30];
            int[] array28 = new int[num30];
            int[] array29 = new int[num30];
            int[] meshVertexCount = new int[num30];

            if (WIMDOExists)
            {
                FileStream fsWIMDO = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + ".wimdo", FileMode.Open, FileAccess.Read);
                BinaryReader brWIMDO = new BinaryReader(fsWIMDO);
                brWIMDO.ReadInt32(); //0x0
                brWIMDO.ReadInt32(); //0x4
                int num23 = brWIMDO.ReadInt32(); //0x8
                fsWIMDO.Seek(num23 + 0x1C, SeekOrigin.Begin);
                int num24 = brWIMDO.ReadInt32(); //0x6C
                brWIMDO.ReadInt32(); //0x70
                brWIMDO.ReadInt32(); //0x74
                int num25 = brWIMDO.ReadInt32(); //0x78
                fsWIMDO.Seek(0x54, SeekOrigin.Current);
                int num26 = brWIMDO.ReadInt32(); //0xCC
                meshFlexNames = null;
                if (num26 > 0)
                {
                    fsWIMDO.Seek((long)(num23 + num26), SeekOrigin.Begin);
                    int num27 = brWIMDO.ReadInt32();
                    int num28 = brWIMDO.ReadInt32();
                    int[] array26 = new int[num28];
                    meshFlexNames = new string[num28];
                    for (i = 0; i < num28; i++)
                    {
                        fsWIMDO.Seek((long)(num23 + num26 + num27 + i * 28), SeekOrigin.Begin);
                        array26[i] = brWIMDO.ReadInt32();
                    }
                    for (i = 0; i < num28; i++)
                    {
                        fsWIMDO.Seek((long)(num23 + num26 + array26[i]), SeekOrigin.Begin);
                        meshFlexNames[i] = FormatTools.ReadNullTerminatedString(brWIMDO);
                    }
                }
                fsWIMDO.Seek((long)(num23 + num24), SeekOrigin.Begin);
                int num29 = brWIMDO.ReadInt32();
                num30 = brWIMDO.ReadInt32();
                fsWIMDO.Seek((long)(num23 + num29), SeekOrigin.Begin);
                array27 = new int[num30];
                array28 = new int[num30];
                array29 = new int[num30];
                meshVertexCount = new int[num30];
                for (i = 0; i < num30; i++)
                {
                    brWIMDO.ReadByte();
                    brWIMDO.ReadByte();
                    brWIMDO.ReadByte();
                    array29[i] = (int)brWIMDO.ReadByte();
                    brWIMDO.ReadInt32();
                    array28[i] = (int)brWIMDO.ReadInt16();
                    array27[i] = (int)brWIMDO.ReadInt16();
                    brWIMDO.ReadInt16();
                    brWIMDO.ReadInt32();
                    brWIMDO.ReadInt32();
                    brWIMDO.ReadInt32();
                    brWIMDO.ReadInt32();
                    meshVertexCount[i] = (int)brWIMDO.ReadInt16();
                    brWIMDO.ReadInt32();
                    brWIMDO.ReadInt32();
                    brWIMDO.ReadInt32();
                    brWIMDO.ReadInt32();
                }

                fsWIMDO.Seek((long)(num23 + num25), SeekOrigin.Begin);
                int num31 = brWIMDO.ReadInt32();
                brWIMDO.ReadInt32();
                int num32 = brWIMDO.ReadInt32();
                fsWIMDO.Seek((long)(num23 + num25 + num32), SeekOrigin.Begin);
                int[] array31 = new int[num31];
                for (i = 0; i < num31; i++)
                {
                    array31[i] = num23 + num25 + brWIMDO.ReadInt32();
                    fsWIMDO.Seek(20L, SeekOrigin.Current);
                }
                dictionary = new Dictionary<int, string>();
                for (i = 0; i < num31; i++)
                {
                    fsWIMDO.Seek((long)array31[i], SeekOrigin.Begin);
                    string value = FormatTools.ReadNullTerminatedString(brWIMDO);
                    dictionary.Add(i, value);
                }
            }

            int boneCount = 0;
            Vector3[] bonePos = new Vector3[boneCount];
            Quaternion[] boneRot = new Quaternion[boneCount];
            int[] bone_parents = new int[boneCount];
            Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
            string[] bone_names = new string[boneCount];

            if (ARCExists)
            {
                FileStream fsARC = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + ".arc", FileMode.Open, FileAccess.Read);
                BinaryReader brARC = new BinaryReader(fsARC);
                boneCount = 0;
                int num34 = 0;
                int num35 = 0;
                int num36 = 0;
                int num37 = 0;
                fsARC.Seek(12L, SeekOrigin.Begin);
                int num38 = brARC.ReadInt32();
                fsARC.Seek((long)brARC.ReadInt32(), SeekOrigin.Begin);
                int[] array32 = new int[num38];
                for (i = 0; i < num38; i++)
                {
                    array32[i] = brARC.ReadInt32();
                    fsARC.Seek(60L, SeekOrigin.Current);
                }
                for (i = 0; i < num38; i++)
                {
                    fsARC.Seek((long)(array32[i] + 36), SeekOrigin.Begin);
                    int SKELMagic = brARC.ReadInt32();
                    if (SKELMagic == 0x4C454B53) //SKEL
                    {
                        num37 = array32[i];
                        fsARC.Seek((long)(array32[i] + 80), SeekOrigin.Begin);
                        num35 = array32[i] + brARC.ReadInt32();
                        brARC.ReadInt32();
                        boneCount = brARC.ReadInt32();
                        fsARC.Seek((long)(array32[i] + 96), SeekOrigin.Begin);
                        num36 = array32[i] + brARC.ReadInt32();
                        fsARC.Seek((long)(array32[i] + 112), SeekOrigin.Begin);
                        num34 = array32[i] + brARC.ReadInt32();
                    }
                }
                Vector3[] bonePosFile = new Vector3[boneCount];
                bonePos = new Vector3[boneCount];
                Quaternion[] boneRotFile = new Quaternion[boneCount];
                boneRot = new Quaternion[boneCount];
                bone_parents = new int[boneCount];
                fsARC.Seek((long)num36, SeekOrigin.Begin);
                int[] array38 = new int[boneCount];
                for (i = 0; i < boneCount; i++)
                {
                    array38[i] = num37 + brARC.ReadInt32();
                    brARC.ReadInt32();
                    brARC.ReadInt32();
                    brARC.ReadInt32();
                }
                dictionary2 = new Dictionary<string, int>();
                bone_names = new string[boneCount];
                for (i = 0; i < boneCount; i++)
                {
                    fsARC.Seek((long)array38[i], SeekOrigin.Begin);
                    string text = FormatTools.ReadNullTerminatedString(brARC);
                    dictionary2.Add(text, i);
                    bone_names[i] = text;
                }
                fsARC.Seek((long)num34, SeekOrigin.Begin);
                for (i = 0; i < boneCount; i++)
                {
                    float ARCReadX = brARC.ReadSingle();
                    float ARCReadY = brARC.ReadSingle();
                    float ARCReadZ = brARC.ReadSingle();
                    float ARCReadR = brARC.ReadSingle();
                    bonePosFile[i] = new Vector3(ARCReadX, ARCReadY, ARCReadZ);
                    ARCReadX = brARC.ReadSingle();
                    ARCReadY = brARC.ReadSingle();
                    ARCReadZ = brARC.ReadSingle();
                    ARCReadR = brARC.ReadSingle();
                    boneRotFile[i] = new Quaternion(ARCReadX, ARCReadY, ARCReadZ, ARCReadR);
                    bonePos[i] = new Vector3(ARCReadX, ARCReadY, ARCReadZ);
                    brARC.ReadSingle();
                    brARC.ReadSingle();
                    brARC.ReadSingle();
                    brARC.ReadSingle();
                }
                fsARC.Seek((long)num35, SeekOrigin.Begin);
                for (int j = 0; j < boneCount; j++)
                {
                    bone_parents[j] = (int)brARC.ReadInt16();
                }
                for (i = 0; i < boneCount; i++)
                {
                    if (bone_parents[i] < 0) //is root
                    {
                        bonePos[i] = bonePosFile[i];
                        boneRot[i] = boneRotFile[i];
                    }
                    else
                    {
                        int curParentIndex = bone_parents[i];
                        boneRot[i] = boneRot[curParentIndex] * boneRotFile[i];
                        Quaternion right = new Quaternion(bonePosFile[i], 0f);
                        Quaternion left = boneRot[curParentIndex] * right;
                        Quaternion Quaternion = left * new Quaternion(-boneRot[curParentIndex].X, -boneRot[curParentIndex].Y, -boneRot[curParentIndex].Z, boneRot[curParentIndex].W);
                        bonePos[i] = new Vector3(Quaternion.X, Quaternion.Y, Quaternion.Z);
                        Vector3[] array40;
                        IntPtr intPtr;
                        (array40 = bonePos)[(int)(intPtr = (IntPtr)i)] = array40[(int)intPtr] + bonePos[curParentIndex];
                    }
                }
            }

            //begin ascii
            //bone time
            StreamWriter asciiWriter = new StreamWriter(Path.GetDirectoryName(args[0]) + "\\" + Path.GetFileNameWithoutExtension(args[0]) + ".ascii");
            asciiWriter.WriteLine(boneCount);
            for (int j = 0; j < boneCount; j++)
            {
                asciiWriter.WriteLine(bone_names[j]);
                asciiWriter.WriteLine(bone_parents[j]);
                asciiWriter.Write(bonePos[j].X.ToString("0.######"));
                asciiWriter.Write(" " + bonePos[j].Y.ToString("0.######"));
                asciiWriter.Write(" " + bonePos[j].Z.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].X.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].Y.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].Z.ToString("0.######"));
                asciiWriter.Write(" " + boneRot[j].W.ToString("0.######"));
                //bone name
                //bone parent index
                //x y z i j w real
                asciiWriter.WriteLine();
            }

            //begin meshes
            for (int currentMesh = 0; currentMesh < meshCount; currentMesh++)
            {
                meshVertices[currentMesh] = new Vector3[meshesDataCount[currentMesh]];
                meshNormals[currentMesh] = new Vector3[meshesDataCount[currentMesh]];
                meshFlexesX[currentMesh] = new float[meshesDataCount[currentMesh], 4];
                meshFlexesY[currentMesh] = new float[meshesDataCount[currentMesh], 4];
                meshWeights[currentMesh] = new int[meshesDataCount[currentMesh]];
                int num44 = -1;
                int num45 = -1;
                int num46 = -1;
                int num47 = -1;
                int num48 = -1;
                int num49 = 0;
                int[] meshUVSomething = new int[4];
                memoryStream.Seek((long)array13[currentMesh], SeekOrigin.Begin);
                for (int j = 0; j < array14[currentMesh]; j++)
                {
                    int num50 = (int)binaryReader.ReadInt16();
                    int num51 = (int)binaryReader.ReadInt16();
                    if (num50 == 0)
                    {
                        num44 = num49;
                    }
                    else if (num50 == 3)
                    {
                        num46 = num49;
                    }
                    else if (num50 == 5)
                    {
                        meshUVSomething[meshUVLayers[currentMesh]] = num49;
                        meshUVLayers[currentMesh]++;
                    }
                    else if (num50 == 6)
                    {
                        meshUVSomething[meshUVLayers[currentMesh]] = num49;
                        meshUVLayers[currentMesh]++;
                    }
                    else if (num50 == 7)
                    {
                        meshUVSomething[meshUVLayers[currentMesh]] = num49;
                        meshUVLayers[currentMesh]++;
                    }
                    else if (num50 == 28)
                    {
                        num45 = num49;
                    }
                    else if (num50 == 41)
                    {
                        num47 = num49;
                    }
                    else if (num50 == 42)
                    {
                        num48 = num49;
                    }
                    num49 += num51;
                }
                for (int j = 0; j < meshesDataCount[currentMesh]; j++)
                {
                    if (num44 >= 0)
                    {
                        //if (j == 0)
                        //    Console.WriteLine($"POINTER: {meshesPointers[currentMesh]}\nJ: {j}\nARRAY12: {array12[currentMesh]}\nNUM44: {num44}");
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num44), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's vertices! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshVertices[currentMesh][j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                        meshNormals[currentMesh][j] = new Vector3(0f, 0f, 0f);
                    }
                    if (num46 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num46), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's weights! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshWeights[currentMesh][j] = binaryReader.ReadInt32();
                    }

                    for (int k = 0; k < meshUVLayers[currentMesh]; k++)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + meshUVSomething[k]), SeekOrigin.Begin);
                        //if (currentMesh == 0 && k == 0)
                        //    Console.WriteLine($"Setting mesh's flexes! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshFlexesX[currentMesh][j, k] = binaryReader.ReadSingle();
                        meshFlexesY[currentMesh][j, k] = binaryReader.ReadSingle();
                    }

                    if (num45 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num45), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's normals! At offset 0x{memoryStream.Position.ToString("X")}");
                        float num40 = (float)binaryReader.ReadSByte() / 128f;
                        float num41 = (float)binaryReader.ReadSByte() / 128f;
                        float num42 = (float)binaryReader.ReadSByte() / 128f;
                        meshNormals[currentMesh][j] = new Vector3(num40, num41, num42);
                    }
                    if (num48 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num48), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's weight ids! At offset 0x{memoryStream.Position.ToString("X")}");
                        try
                        {
                            int key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 0] = dictionary2[dictionary[key]];
                            key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 1] = dictionary2[dictionary[key]];
                            key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 2] = dictionary2[dictionary[key]];
                            key = (int)binaryReader.ReadByte();
                            meshWeightIds[j, 3] = dictionary2[dictionary[key]];
                        }
                        catch
                        {
                            meshWeightIds[j, 0] = 0;
                            meshWeightIds[j, 1] = 0;
                            meshWeightIds[j, 2] = 0;
                            meshWeightIds[j, 3] = 0;
                        }
                    }
                    if (num47 >= 0)
                    {
                        memoryStream.Seek((long)(meshesPointers[currentMesh] + j * array12[currentMesh] + num47), SeekOrigin.Begin);
                        //if (currentMesh == 0)
                        //    Console.WriteLine($"Setting mesh's weight values! At offset 0x{memoryStream.Position.ToString("X")}");
                        meshWeightValues[j, 0] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 1] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 2] = (float)binaryReader.ReadUInt16() / 65535f;
                        meshWeightValues[j, 3] = (float)binaryReader.ReadUInt16() / 65535f;
                    }
                }
            }

            Vector3[][] array42 = new Vector3[num19][];
            if (num16 > 0)
            {
                for (i = 0; i < num19; i++)
                {
                    array10[array6[i]] = i + 1;
                    memoryStream.Seek((long)(num20 + array7[i] * 16), SeekOrigin.Begin);
                    int num52 = meshDataStart + binaryReader.ReadInt32();
                    int num53 = binaryReader.ReadInt32();
                    if (num53 != meshesDataCount[array6[i]])
                    {
                        Console.WriteLine("Flex vertices count is incorrect!");
                    }
                    array42[i] = new Vector3[num53];
                    for (int j = 0; j < num53; j++)
                    {
                        memoryStream.Seek((long)(num52 + j * 32), SeekOrigin.Begin);
                        meshVertices[array6[i]][j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                        meshNormals[array6[i]][j] = new Vector3(0f, 0f, 0f);
                    }
                }
            }

            //total mesh count
            int flexAndMeshCount = 0;
            for (i = 0; i < num30; i++)
            {
                if (meshVertexCount[i] < 2)
                {
                    flexAndMeshCount++;
                    if (array29[i] != 0 && exportFlexes)
                    {
                        flexAndMeshCount += array8[0];
                    }
                }
            }
            asciiWriter.WriteLine(flexAndMeshCount);
            int num55 = 1;
            if (num16 > 0 && exportFlexes)
            {
                num55 += array8[0];
            }

            //write it
            for (int flexIndex = 0; flexIndex < num55; flexIndex++)
            {
                if (flexIndex > 0)
                {
                    for (i = 0; i < num19; i++)
                    {
                        for (int j = 0; j < array42[i].Length; j++)
                        {
                            array42[i][j] = new Vector3();
                        }
                        memoryStream.Seek((long)(num20 + (array7[i] + 1 + flexIndex) * 16), SeekOrigin.Begin);
                        int num56 = meshDataStart + binaryReader.ReadInt32();
                        int num57 = binaryReader.ReadInt32();
                        for (int j = 0; j < num57; j++)
                        {
                            memoryStream.Seek((long)(num56 + j * 32), SeekOrigin.Begin);
                            Vector3 Vector3 = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                            memoryStream.Seek(16L, SeekOrigin.Current);
                            int num58 = binaryReader.ReadInt32();
                            array42[i][num58] = Vector3;
                        }
                    }
                }
                for (i = 0; i < num30; i++)
                {
                    if (meshVertexCount[i] <= 1)
                    {
                        int curMesh = array27[i];
                        int curMeshIndex = array28[i];
                        if (array29[i] != 0 || flexIndex <= 0)
                        {
                            memoryStream.Seek((long)meshesDataStart[curMesh], SeekOrigin.Begin);
                            int[] array43 = new int[meshesVertexCount[curMesh]];
                            int curMeshVertCount = 0;
                            int prvMeshVertCount = meshesDataCount[curMeshIndex];
                            for (int j = 0; j < meshesVertexCount[curMesh]; j++)
                            {
                                int num63 = (int)binaryReader.ReadInt16();
                                array43[j] = num63;
                                if (num63 > curMeshVertCount)
                                {
                                    curMeshVertCount = num63;
                                }
                                if (num63 < prvMeshVertCount)
                                {
                                    prvMeshVertCount = num63;
                                }
                            }
                            if (flexIndex > 0)
                            {
                                asciiWriter.WriteLine($"sm_{i}_{meshFlexNames[flexIndex - 1]}"); //mesh name (+ flex name)
                            }
                            else
                            {
                                asciiWriter.WriteLine("sm_" + i); //mesh name
                            }
                            asciiWriter.WriteLine(meshUVLayers[curMeshIndex]);
                            asciiWriter.WriteLine(0); //texture count, always 0 for us, though maybe I should change that?
                            asciiWriter.WriteLine(curMeshVertCount - prvMeshVertCount + 1); //vertex count
                            for (int vrtIndex = prvMeshVertCount; vrtIndex <= curMeshVertCount; vrtIndex++)
                            {
                                if (flexIndex > 0)
                                {
                                    //vertex position (+ flexOffset)
                                    Vector3 Vector3 = meshVertices[curMeshIndex][vrtIndex] + array42[array10[curMeshIndex] - 1][vrtIndex] + new Vector3((float)flexIndex * exportFlexesDistance, 0f, 0f);
                                    asciiWriter.Write(Vector3.X.ToString("0.######"));
                                    asciiWriter.Write(" " + Vector3.Y.ToString("0.######"));
                                    asciiWriter.Write(" " + Vector3.Z.ToString("0.######"));
                                    asciiWriter.WriteLine();
                                }
                                else
                                {
                                    //vertex position
                                    asciiWriter.Write(meshVertices[curMeshIndex][vrtIndex].X.ToString("0.######"));
                                    asciiWriter.Write(" " + meshVertices[curMeshIndex][vrtIndex].Y.ToString("0.######"));
                                    asciiWriter.Write(" " + meshVertices[curMeshIndex][vrtIndex].Z.ToString("0.######"));
                                    asciiWriter.WriteLine();
                                }

                                //vertex normal
                                asciiWriter.Write(meshNormals[curMeshIndex][vrtIndex].X.ToString("0.######"));
                                asciiWriter.Write(" " + meshNormals[curMeshIndex][vrtIndex].Y.ToString("0.######"));
                                asciiWriter.Write(" " + meshNormals[curMeshIndex][vrtIndex].Z.ToString("0.######"));
                                asciiWriter.WriteLine();
                                asciiWriter.WriteLine("0 0 0 0"); // vertex color (why)

                                //uv coords
                                for (int curUVLayer = 0; curUVLayer < meshUVLayers[curMeshIndex]; curUVLayer++)
                                    asciiWriter.WriteLine(meshFlexesX[curMeshIndex][vrtIndex, curUVLayer].ToString("0.######") + " " + meshFlexesY[curMeshIndex][vrtIndex, curUVLayer].ToString("0.######"));

                                //weight ids
                                asciiWriter.Write(meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 0]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 1]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 2]);
                                asciiWriter.Write(" " + meshWeightIds[meshWeights[curMeshIndex][vrtIndex], 3]);
                                asciiWriter.WriteLine();

                                //weight values
                                asciiWriter.Write(meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 0].ToString("0.######"));
                                asciiWriter.Write(" " + meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 1].ToString("0.######"));
                                asciiWriter.Write(" " + meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 2].ToString("0.######"));
                                asciiWriter.Write(" " + meshWeightValues[meshWeights[curMeshIndex][vrtIndex], 3].ToString("0.######"));
                                asciiWriter.WriteLine();
                            }

                            //face count
                            asciiWriter.WriteLine(meshesVertexCount[curMesh] / 3);
                            for (int j = 0; j < meshesVertexCount[curMesh]; j += 3)
                            {
                                int faceVertexZ = array43[j] - prvMeshVertCount;
                                int faceVertexY = array43[j + 1] - prvMeshVertCount;
                                int faceVertexX = array43[j + 2] - prvMeshVertCount;
                                //face vertex ids
                                asciiWriter.WriteLine($"{faceVertexX} {faceVertexY} {faceVertexZ}");
                            }
                        }
                    }
                }
            }

            asciiWriter.Close();
        }

        public void ReadTextures(FileStream fsWISMT, BinaryReader brWISMT, string texturesFolderPath, int[] fileOffsets, int textureIdCount, int[] someVarietyOfPointer, string[] textureNames, int[] textureIds)
        {
            MemoryStream msCurFile = XBC1(fsWISMT, brWISMT, fileOffsets[1]);
            BinaryReader brCurFile = new BinaryReader(msCurFile);
            int[] array44 = new int[textureIdCount];
            int[] array45 = new int[textureIdCount];
            int[] array46 = new int[textureIdCount];
            for (int j = 0; j < textureIdCount; j++)
            {
                msCurFile.Seek((long)(someVarietyOfPointer[j + 3] - 32), SeekOrigin.Begin);
                array44[j] = brCurFile.ReadInt32();
                array45[j] = brCurFile.ReadInt32();
                brCurFile.ReadInt32();
                brCurFile.ReadInt32();
                array46[j] = brCurFile.ReadInt32();
            }

            

            int i = 0;
            while (i < textureIdCount-1)
            {
                msCurFile = XBC1(fsWISMT, brWISMT, fileOffsets[i + 2]);
                brCurFile = new BinaryReader(msCurFile);
                int DDSDepth = 1;
                int num67;
                if (array46[i] == 37)
                {
                    num67 = 28;
                    goto IL_19CA;
                }
                if (array46[i] == 66)
                {
                    num67 = 71;
                    goto IL_19CA;
                }
                if (array46[i] == 68)
                {
                    num67 = 77;
                    goto IL_19CA;
                }
                if (array46[i] == 73)
                {
                    num67 = 80;
                    goto IL_19CA;
                }
                if (array46[i] == 75)
                {
                    num67 = 83;
                    goto IL_19CA;
                }
                Console.WriteLine("unknown texture type " + array46[i]);
                IL_1D71:
                i++;
                continue;
                IL_19CA:
                int DDSHeight = array44[i] * 2;
                int DDSFlags = array45[i] * 2;
                int num70 = 808540228; //DX10
                int num71 = bpp[num67] * 2;
                int num72 = 4;
                if (num67 == 71)
                {
                    num70 = 0x31545844; //DXT1
                }
                if (num67 == 74)
                {
                    num70 = 861165636; //DXT3
                }
                if (num67 == 77)
                {
                    num70 = 894720068; //DXT5
                }
                if (num67 == 80)
                {
                    num70 = 826889281; //ATI1
                }
                if (num67 == 83)
                {
                    num70 = 843666497; //ATI2
                }
                FileStream fileStream4;
                if (array46[i] == 37)
                {
                    fileStream4 = new FileStream(string.Concat(new string[]
                    {
                        texturesFolderPath,
                        "\\",
                        i.ToString("d2"),
                        "_",
                        textureNames[textureIds[i]],
                        ".tga"
                    }), FileMode.Create);
                    BinaryWriter binaryWriter = new BinaryWriter(fileStream4);
                    binaryWriter.Write(131072);
                    binaryWriter.Write(0);
                    binaryWriter.Write(0);
                    binaryWriter.Write((short)DDSHeight);
                    binaryWriter.Write((short)DDSFlags);
                    binaryWriter.Write(2080);
                    binaryWriter.Seek(0x12, SeekOrigin.Begin);
                    num71 = bpp[num67] / 8;
                    num72 = 1;
                }
                else
                {
                    fileStream4 = new FileStream(string.Concat(new string[]
                    {
                        texturesFolderPath,
                        "\\",
                        i.ToString("d2"),
                        "_",
                        textureNames[textureIds[i]],
                        ".dds"
                    }), FileMode.Create);
                    BinaryWriter binaryWriter = new BinaryWriter(fileStream4);
                    binaryWriter.Write(0x7C20534444); //DDS | (backwards)
                    binaryWriter.Write(0x1007); //some shit
                    binaryWriter.Write(DDSFlags);
                    binaryWriter.Write(DDSHeight);
                    binaryWriter.Write(msCurFile.Length);
                    //binaryWriter.Write(0);
                    binaryWriter.Write(DDSDepth);
                    fileStream4.Seek(44L, SeekOrigin.Current);
                    binaryWriter.Write(32);
                    binaryWriter.Write(4);
                    binaryWriter.Write(num70);
                    fileStream4.Seek(40L, SeekOrigin.Current);
                    if (num70 == 808540228)
                    {
                        binaryWriter.Write(num67);
                        binaryWriter.Write(3);
                        binaryWriter.Write(0);
                        binaryWriter.Write(1);
                        binaryWriter.Write(0);
                    }
                }
                byte[] array47 = new byte[16];
                byte[] array48 = new byte[msCurFile.Length];
                int num73 = DDSFlags / num72;
                int num74 = DDSHeight / num72;
                int num75 = num73 / 8;
                if (num75 > 16)
                {
                    num75 = 16;
                }
                int num76 = 1;
                if (num71 == 16)
                {
                    num76 = 1;
                }
                if (num71 == 8)
                {
                    num76 = 2;
                }
                if (num71 == 4)
                {
                    num76 = 4;
                }
                for (int n = 0; n < num73 / 8 / num75; n++)
                {
                    for (int num77 = 0; num77 < num74 / 4 / num76; num77++)
                    {
                        for (int num78 = 0; num78 < num75; num78++)
                        {
                            for (int num79 = 0; num79 < 32; num79++)
                            {
                                for (int num80 = 0; num80 < num76; num80++)
                                {
                                    int num81 = swi[num79];
                                    int num82 = num81 / 4;
                                    int num83 = num81 % 4;
                                    int num84 = (n * num75 + num78) * 8 + num82;
                                    int num85 = (num77 * 4 + num83) * num76 + num80;
                                    if (num72 == 1)
                                    {
                                        array47[2] = (byte)msCurFile.ReadByte();
                                        array47[1] = (byte)msCurFile.ReadByte();
                                        array47[0] = (byte)msCurFile.ReadByte();
                                        array47[3] = (byte)msCurFile.ReadByte();
                                        num84 = DDSFlags - num84 - 1;
                                    }
                                    else
                                    {
                                        msCurFile.Read(array47, 0, num71);
                                    }
                                    int destinationIndex = num71 * (num84 * num74 + num85);
                                    Array.Copy(array47, 0, array48, destinationIndex, num71);
                                }
                            }
                        }
                    }
                }
                fileStream4.Write(array48, 0, (int)msCurFile.Length);
                fileStream4.Close();
                goto IL_1D71;
            }
        }

        public static int[] bpp = new int[] {
             0, 128, 128, 128, 128,  96,  96,  96,  96,  64,  64,  64,  64,  64,  64,  64,
            64,  64,  64,  64,  64,  64,  64,  32,  32,  32,  32,  32,  32,  32,  32,  32,
            32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,
            16,  16,  16,  16,  16,  16,  16,  16,  16,  16,  16,  16,   8,   8,   8,   8,
             8,   8,   1,  32,  32,  32,   4,   4,   4,   8,   8,   8,   8,   8,   8,   4,
             4,   4,   8,   8,   8,  16,  16,  32,  32,  32,  32,  32,  32,  32,   8,   8,
             8,   8,   8,   8,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,
             0,   0,   0,  16
        };

        public static int[] swi = new int[]
        {
             0,  4,  1,  5,  8, 12,  9, 13,
            16, 20, 17, 21, 24, 28, 25, 29,
             2,  6,  3,  7, 10, 14, 11, 15,
            18, 22, 19, 23, 26, 30, 27, 31
        };
    }
}

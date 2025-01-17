﻿using Cocona;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using Tiled2Dmap.CLI.Attributes;
using Tiled2Dmap.CLI.Dmap;
using Tiled2Dmap.CLI.Extensions;

namespace Tiled2Dmap.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            SevenZip.SevenZipExtractor.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
            CoconaApp.Run<Program>(args, options =>
            {
                options.TreatPublicMethodsAsCommands = false;
            });

        }

        public record CommonParameters(
            [Option('p', Description = "Project Directory")]
            [DirectoryExists]
            string ProjectDirectory,

            [Option('x', Description = "Clear Resources")]
            bool Clear = true

            ) : ICommandParameterSet;
        [Command("preview")]
        public void PreviewMap(
            [Argument("client", Description = "Directory of client resources")][DirectoryExists] string clientPath,
            [Argument("dmap", Description = "Path to Dmap File")] string dmapPath ="",
            [Option("width", new char[] { 'w' }, Description ="Preview Window Width")] int Width = 1024,
            [Option("height", new char[] { 'h' }, Description = "Preview Window Height")] int Height = 768
            )
        {
            using (var game = new Preview.RenderWindow(clientPath, Width, Height, dmapPath))
                game.Run();
        }
        [Command("extract")]
        public void Extract(
            [Argument("output", Description = "Output Directory")][DirectoryExists] string outputDir,
            [Argument("dmap", Description = "Path to Dmap File")] string dmapPath,
            [Argument("client", Description = "Directory of client resources")][DirectoryExists] string clientPath,
            [Argument("name", Description = "Name of the new dmap")] string dmapName
            )
        {
            string outDir = Path.Combine(outputDir, dmapName);
            if (!Path.IsPathFullyQualified(dmapPath)) dmapPath = Path.Combine(clientPath, dmapPath);
            Directory.CreateDirectory(outDir);
            DmapExtract dmapExtract = new DmapExtract(new Utility.ClientResources(clientPath), new DmapFile(dmapPath, clientPath), dmapName);
            dmapExtract.Extract(outDir);
        }
        [Command("install")]
        public void Install(
            [Argument("project", Description = "Project Directory")][DirectoryExists] string inputDir,
            [Argument("mapId", Description = "New Map Id")] ushort mapId,
            [Argument("client", Description = "Client root directory to install")][DirectoryExists] string outputDir,
            [Option("puzzleSize", Description = "Size in pixels of puzzle pieces")] ushort puzzleSize = 256
            )
        {
            string gameMapDat = Path.Combine(outputDir, "ini\\GameMap.dat");
            if(!File.Exists(gameMapDat))
            {
                Log.Error($"{gameMapDat} does not exist.");
                return;
            }

            string projectName = (new DirectoryInfo(inputDir).Name);
            string relativeDmapPath = $"map/map/{projectName}.dmap";
            if (!File.Exists(Path.Combine(inputDir, relativeDmapPath)))
            {
                Log.Error($"{relativeDmapPath} does not exist");
                return;
            }

            GameMapDat.GameMapDatFile mapDat = new GameMapDat.GameMapDatFile(gameMapDat);
            if(!mapDat.TryAdd(mapId, relativeDmapPath, puzzleSize))
                return;


            //Copy resources.
            foreach(var file in Directory.EnumerateFiles(inputDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(inputDir, file);
                string newPath = Path.Combine(outputDir, relativePath);
                if(File.Exists(newPath))
                {
                    Log.Info($"File Already exists {relativePath}");
                    continue;
                }

                string? outDir = Path.GetDirectoryName(newPath);
                if (outDir == null) { Log.Warn($"Unable to copy resources to {newPath}, can't retrieve directory"); return; }

                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                File.Copy(file, newPath);
            }
            mapDat.Write();

        }
        [Command("dmap2tiled")]
        public void Dmap2Tiled(
            [Option(Description ="Directory of Project")][DirectoryExists] string project, 
            [Option("client", Description = "Directory of client resources")][DirectoryExists] string clientPath,
            [Option("dmap", Description ="Path to Dmap File")] string dmapPath,
            [Option('s', Description ="Saves the stiched together map background")] bool saveBackground = false
            )
        {
            Console.WriteLine($"Called dmap2tiled --project {project} --client {clientPath} --dmap {dmapPath}, -s {saveBackground}");
            Utility.ClientResources clientResources = new(clientPath);
            var dmap = new DmapFile(dmapPath, clientPath);
            Tiled.TiledProject.FromDmap(project, clientResources, dmap);
        }
        [Command("tiled2dmap")]
        public void Tiled2Dmap(
            [Option(Description = "Directory of Project")][DirectoryExists] string project,
            [Option(Description = "Name of the map")]string MapName
            )
        {
            Console.WriteLine($"Called tiled2dmap --project {project} --map-name {MapName}");
            DmapProject dmapProject = new(project, MapName);
            dmapProject.AssembleDmap();
            dmapProject.AssemblePuzzle();
        }

        [Command]
        public void StitchDmap(
            [Option(Description = "Directory of Project")] string output,
            [Option("client", Description = "Directory of client resources")][DirectoryExists] string ConquerDirectory,
            [Option("dmap", Description = "Path to Dmap File")] string dmapPath
            )
        {
            Console.WriteLine($"Called stitchdmap --output {output} --client {ConquerDirectory} --dmap {dmapPath}");

            Utility.ClientResources clientResources = new(ConquerDirectory);
            DmapFile dmapFile = new(dmapPath, ConquerDirectory);
            //Get full path to allow relative output paths.
            output = Path.GetFullPath(output);
            
            using (var stitch = new ImageServices.Stitch(clientResources, new PuzzleFile(ConquerDirectory, dmapFile.PuzzleFile)))
            {
                stitch.Image.Save(output);
            }

        }

        [Command]
        public void NewProject(
            [Option(Description = "Name of the project")] string Project, 
            [Option("directory", Description = "Directory for project folder to be created, default is current dir")][DirectoryExists] string RootDirectory = null)
        {
            string projectDir;
            if (RootDirectory == null)
            {
                if (Path.IsPathFullyQualified(Project))
                    projectDir = Project;
                else
                    projectDir = Path.GetFullPath(Project);
            }
            else
                projectDir = Path.Combine(RootDirectory, Project);

            if (Directory.Exists(projectDir))
                Console.WriteLine("Project Directory already exists");
            else
            {
                var info = Directory.CreateDirectory(Path.Combine(RootDirectory, Project));

                if(info.Exists)
                    Console.WriteLine($"Succesfully created project directory\r\n{projectDir}");
                else
                    Console.WriteLine("Failed to create project directory");
                //TODO: Scaffold Project folder structure.

            }


        }
        [Command]
        public void Serialize(
            [Argument("dmap", Description = "Path to Dmap File")][FileExists] string dmapPath,
            [Argument("output", Description = "Output Directory for json file")][DirectoryExists] string outputDir,
            [Option('x', Description = "Exclude tile set in serlaization")] bool ExcludeTiles)
        {
            DmapFile dmapFile = new(dmapPath);
            Dmap.Json.JsonDmapFile jsonDmapFile = Dmap.Json.JsonDmapFile.MapFrom(dmapFile, ExcludeTiles);

            string fileName = Path.GetFileNameWithoutExtension(dmapPath);

            using (FileStream fs = File.OpenWrite(Path.Combine(outputDir, $"{fileName}.json")))
                JsonSerializer.Serialize<Dmap.Json.JsonDmapFile>(fs, jsonDmapFile, new JsonSerializerOptions() { WriteIndented = true });
        }
        [Command]
        public void Deserialize(
            [Argument("json", Description = "Path to Json File")][FileExists] string jsonPath,
            [Argument("output", Description = "Output Directory for dmap file")][DirectoryExists] string outputDir)
        {
            string fileName = Path.GetFileNameWithoutExtension(jsonPath);

            using (FileStream fs = File.OpenRead(jsonPath))
            {
                var dmap = Dmap.Json.JsonDmapFile.MapTo(JsonSerializer.Deserialize<Dmap.Json.JsonDmapFile>(fs));
                using(FileStream outFs = File.OpenWrite(Path.Combine(outputDir, $"{fileName}.dmap")))
                {
                    dmap.Save(outFs);
                }
            }
        }

        [Command]
        public void TestParseDmap(
            [Argument("client", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory,
            [Option("dmap", Description = "Specific Dmap to parse")][FileExists] string DmapPath = ""
            )
        {
            if (DmapPath.Length > 0)
            {
                var dmap = new DmapFile(DmapPath, ConquerDirectory);
            }
            else
            {
                Console.WriteLine($"Loading All Puzzle Files in {Path.Combine(ConquerDirectory, @"map\map")}");
                foreach (string file in Directory.GetFiles(Path.Combine(ConquerDirectory, @"map\map"), "*.*"))
                {
                    if (!((file.ToLower()).EndsWith("7z") || (file.ToLower()).EndsWith("zmap") || (file.ToLower()).EndsWith("dmap"))) continue;
                    var dmap = new DmapFile(file, ConquerDirectory);
                    if(dmap.PuzzleFile.EndsWith(".pux"))
                    {
                        Log.Warn($"Pux File: {dmap.PuzzleFile}");
                    }
                }
            }
        }

        [Command]
        public void TestParsePuzzle(
            [Option("directory", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory
            )
        {
            Console.WriteLine($"Loading All Puzzle Files in {Path.Combine(ConquerDirectory, @"map\puzzle")}");
            foreach (string file in Directory.GetFiles(Path.Combine(ConquerDirectory, @"map\puzzle"), "*.pul"))
            {
                var puzzle = new PuzzleFile(ConquerDirectory, file);
            }
        }
        [Command]
        public void TestParsePux(
            [Argument("directory", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory
            )
        {
            Console.WriteLine($"Loading All Pux Files in {Path.Combine(ConquerDirectory, @"map\PuzzleSave")}");
            string file = @"C:\Program Files (x86)\Conquer Online\Conquer Online 3.0\map\PuzzleSave\faction.pux";
            //foreach (string file in Directory.GetFiles(Path.Combine(ConquerDirectory, @"map\PuzzleSave"), "*.pux"))
            //{
                var puzzle = new PuxFile(ConquerDirectory, file);
            //    break;
            //}
        }

        [Command]
        public void TestParseAni(
            [Option("directory", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory
            )
        {
            Console.WriteLine($"Loading All Ani Files in {Path.Combine(ConquerDirectory, @"ani")}");
            foreach (string file in Directory.GetFiles(Path.Combine(ConquerDirectory, @"ani"), "*.ani"))
            {
                var ani = new AniFile(ConquerDirectory, file);
            }
        }

        //test-parse-scene --client D:\Programming\Conquer\Clients\5165
        [Command]
        public void TestParseScene(
            [Option("client", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory
            )
        {
            Utility.ClientResources clientResources = new(ConquerDirectory);
            Console.WriteLine($"Loading All Scene Files in {Path.Combine(ConquerDirectory, @"map/Scene")}");
            Dictionary<string, AniFile> aniFileCache = new();
            foreach (string file in Directory.GetFiles(Path.Combine(ConquerDirectory, @"map/Scene"), "*.scene"))
            {
                var scene = new SceneFile(ConquerDirectory, file);

                int left = 0;
                int top = 0;
                int right = 0;
                int bottom = 0;

                foreach(var spart in scene.SceneParts)
                {
                    try
                    {
                        AniFile aniFile;
                        if (!aniFileCache.TryGetValue(spart.AniPath, out aniFile))
                        {
                            aniFile = new AniFile(ConquerDirectory, spart.AniPath);
                            aniFileCache.Add(spart.AniPath, aniFile);
                        }
                        Ani ani = aniFile.Anis[spart.AniName];
                        if (ani.Frames.Count > 1)
                            Log.Info("Scene Part has animated frame");
                        using (Bitmap sceneBmp = ImageServices.DDSConvert.StreamToPng(clientResources.GetFile(ani.Frames.Peek().Replace(".msk", ".dds").Replace(".MSK", ".dds"))))
                        {
                            Console.WriteLine($"ScenePart: {spart.AniPath}-{spart.AniName} Position {spart.TileOffset.X},{spart.TileOffset.Y} Offset {spart.PixelLocation.X},{spart.PixelLocation.Y}");
                            //Need to compensate for tile position
                            int isoX = spart.TileOffset.X * Constants.DmapTileHeight * -1;
                            int isoY = spart.TileOffset.Y * Constants.DmapTileHeight * -1;
                            int orthoX = isoY - isoX;
                            int orthoY = (int)(0.5 * (isoY + isoX));
                            orthoX += spart.PixelLocation.X;
                            orthoY -= spart.PixelLocation.Y;


                            if (orthoX < left) left = orthoX;
                            if (orthoY < top) top = orthoY;
                            if ((orthoX + sceneBmp.Width) > right) right = (orthoX + sceneBmp.Width);
                            if ((orthoY + sceneBmp.Height) > bottom) bottom = (orthoY + sceneBmp.Height);
                        }
                    }
                    catch(KeyNotFoundException knfe)
                    {
                        Log.Warn($"Could not find ani entry in {spart.AniPath} entry {spart.AniName}");
                    }
                    catch(FileNotFoundException fnfe)
                    {
                        Log.Warn(fnfe.Message);
                    }
                }
                int totalWidth = 1627;//right - left;
                int totalHeight = 915;// bottom - top;
                Log.Info($"");
                using (Bitmap totalScene = new(totalWidth, totalHeight))
                using (Graphics graphic = Graphics.FromImage(totalScene))
                {
                    Log.Info($"Total Scene Size: {totalScene.Width}, {totalScene.Height}");
                    foreach (var spart in scene.SceneParts)
                    {
                        try
                        {
                            AniFile aniFile;
                            if (!aniFileCache.TryGetValue(spart.AniPath, out aniFile))
                            {
                                aniFile = new AniFile(ConquerDirectory, spart.AniPath);
                                aniFileCache.Add(spart.AniPath, aniFile);
                            }
                            Ani ani = aniFile.Anis[spart.AniName];
                            if (ani.Frames.Count > 1)
                                Log.Info("Scene Part has animated frame");
                            using (Bitmap sceneBmp = ImageServices.DDSConvert.StreamToPng(clientResources.GetFile(ani.Frames.Peek().Replace(".msk", ".dds").Replace(".MSK", ".dds"))))
                            {
                                int isoX = spart.TileOffset.X * Constants.DmapTileHeight * -1;
                                int isoY = spart.TileOffset.Y * Constants.DmapTileHeight * -1;
                                int orthoX = isoY - isoX;
                                int orthoY = (int)(0.5 * (isoY + isoX));
                                orthoX += spart.PixelLocation.X;
                                orthoY -= spart.PixelLocation.Y;
                                orthoY = totalScene.Height - orthoY;
                                orthoX -= left;
                                orthoY += top;
                                orthoY -= 155;
                                Log.Info($"Drawing {spart.AniName} at {orthoX},{orthoY}");
                                graphic.DrawImage(sceneBmp, new Point(orthoX, orthoY));
                            }
                        }

                        catch (KeyNotFoundException knfe)
                        {
                            Log.Warn($"Could not find ani entry in {spart.AniPath} entry {spart.AniName}");
                        }
                        catch (FileNotFoundException fnfe)
                        {
                            Log.Warn(fnfe.Message);
                        }
                    }
                    totalScene.Save($"C:/Temp/Scenes/{Path.GetFileNameWithoutExtension(file)}.png");
                    break;

                }

            }
        }

        [Command]
        public void TestStitch(
            [Option("directory", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory
            )
        {
            Console.WriteLine($"Stitching All Puzzle Files in {Path.Combine(ConquerDirectory, @"map/puzzle")}");
            Utility.ClientResources clientResources = new(ConquerDirectory);
            foreach (string file in Directory.GetFiles(Path.Combine(ConquerDirectory, @"map/puzzle"), "*.pul"))
            {
                try
                {
                    using(var stitch = new ImageServices.Stitch(clientResources, new PuzzleFile(ConquerDirectory, file)))
                    {
                        stitch.Image.Save(Path.Combine("C:/Temp/Stitch", Path.GetFileName(stitch.PuzzleFile.PuzzlePath) + ".png"));
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine($"Failed to load {file}");
                    Console.WriteLine(e.ToString());
                }
            }
        }
        [Command]
        public void TestScaleStitch(
            [Argument("directory", Description = "Conquer Client Root Directory")][DirectoryExists] string ConquerDirectory,
            [Argument("output", Description = "Output Directory")][DirectoryExists] string OutputDirectory
            )
        {
            string gamemapdat = Path.Combine(ConquerDirectory, "ini/GameMap.dat");
            if (File.Exists(gamemapdat))
            {
                Utility.ClientResources clientResources = new(ConquerDirectory);


                List<string> dmapsExported = new();
                foreach(string file in Directory.GetFiles(OutputDirectory))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    dmapsExported.Add(name);
                }
                dmapsExported.Add("island-snail");
                dmapsExported.Add("canyon-fairy");
                dmapsExported.Add("Gulf");
                dmapsExported.Add("halloween02");

                using (BinaryReader reader = new BinaryReader(File.OpenRead(gamemapdat)))
                {
                    uint mapCnt = reader.ReadUInt32();
                    for (int i = 0; i < mapCnt; i++)
                    {
                        uint mapId = reader.ReadUInt32();
                        string dMap = reader.ReadASCIIString(reader.ReadInt32());
                        uint pieceSize = reader.ReadUInt32();

                        string dmapPath = Path.Combine(ConquerDirectory, dMap);


                        if (!File.Exists(dmapPath)) continue;
                        try
                        {
                            DmapFile dmapFile = new(dmapPath, ConquerDirectory);

                            string rootName = Path.GetFileNameWithoutExtension(dmapPath);
                            if (dmapsExported.Contains(rootName)) continue;

                            string outputpath = Path.Combine(OutputDirectory, $"{rootName}.png");

                            using (var stitch = new ImageServices.Stitch(clientResources, new PuzzleFile(ConquerDirectory, dmapFile.PuzzleFile)))
                            {
                                using (Bitmap scaled = new Bitmap(stitch.Image, new Size((int)(stitch.Image.Width * .05), (int)(stitch.Image.Height * .05))))
                                {
                                    scaled.Save(outputpath);
                                    dmapsExported.Add(rootName);
                                }
                            }
                        }
                        catch (Exception e) { }
                    }
                }
            }
            else
                Log.Error($"{gamemapdat} not found");
        }
        [Command]
        public void TestTiledMap(
            [Option(Description = "Directory of Project")][DirectoryExists] string project
            )
        {
            string fileName = new DirectoryInfo(project).Name;
            string filePath = Path.Combine(project, $"{fileName}.json");
            Tiled.TiledMapFile tiledMapFile = new()
            {
                WidthTiles = 12,
                HeightTiles = 12,
                TileWidth = 128,
                TileHeight = 64
            };
            tiledMapFile.Layers.Add(new Tiled.TileLayer()
            {
                Name = "default",
                WidthTiles = 12,
                HeightTiles = 12
            });
            tiledMapFile.Layers.Add(new Tiled.ObjectLayer()
            {
                Name = "sounds"
            });

            JsonSerializerOptions jsOptions = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = new Tiled.Json.LowerCaseNamingPolicy(),
                WriteIndented = true
            };
            jsOptions.Converters.Add(new Tiled.Json.TiledLayerConverter());

            string json = JsonSerializer.Serialize(tiledMapFile, jsOptions);

            File.WriteAllText(filePath, json);

            Console.WriteLine($"Wrote Tiled Map to {filePath}");
        }

        [Command]
        public void TestGenerateTile()
        {
            using Bitmap bitmap = ImageServices.ImageFont.GetNumberBitmap(1234, 4);
            bitmap.Save("C:/Temp/textFontpng.png");
        }
        [Command]
        public void TestConvertMsk([FileExists]string FilePath)
        {
            byte[] data = File.ReadAllBytes(FilePath);
            int width = (int)Math.Sqrt(data.Length * 8);
            int height = width;
            Log.Info($"Msk File loaded estimated size {width},{height}");

            using (Bitmap maskBmp = new(width, height))
            {
                for(int x = 0; x < width; x++)
                {
                    for(int y= 0; y < height; y++)
                    {
                        int idx = x + y * width;
                        int byteIdx = idx / 8;
                        byte mask8 = data[byteIdx];
                        int bitIdx = idx % 8;

                        bool isTrue = (mask8 & (1 << bitIdx)) != 0;

                        if (isTrue)
                            maskBmp.SetPixel(x, y, Color.Black);
                        else
                            maskBmp.SetPixel(x, y, Color.White);

                    }
                }
                maskBmp.Save($"C:/Temp/{Path.GetFileNameWithoutExtension(FilePath)}.png");
            }
        }
    }

}
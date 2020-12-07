// Copyright (c) 2018-2020 Shawn Bozek.
// Licensed under EULA https://docs.google.com/document/d/1xPyZLRqjLYcKMxXLHLmA5TxHV-xww7mHYVUuWLt2q9g/edit?usp=sharing

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using Prion.Nucleus.Debug;
using Prion.Nucleus.Debug.Benchmarking;
using Prion.Nucleus.IO;
using System.Numerics;
using FreeImageAPI;
using Prion.Mitochondria;
using Prion.Mitochondria.Graphics;
using Prion.Mitochondria.Graphics.Roots;
using Prion.Mitochondria.Graphics.UI;
using Prion.Nucleus.Utilities;

namespace BedRocker
{
    public class BedRocker : Game
    {
        public const string SPECULAR_EXTENTION = "_s.png";
        public const string NORMAL_EXTENTION = "_n.png";

        public static void Main(string[] args)
        {
            using (BedRocker rocker = new BedRocker(args))
            {
                rocker.UpdateFrequency = 30;
                Renderer.DrawFrequency = 30;
                rocker.Start(new MainMenu());
                rocker.Dispose();
            }
        }

        protected override bool UseLocalDataStorage => true;

        public BedRocker(string[] args) : base("BedRocker", args)
        {
        }

        private class MainMenu : Root
        {
            public MainMenu()
            {
                Add(new Button
                {
                    Position = new Vector2(0, -180),
                    Size = new Vector2(200, 100),

                    Background = TextureStore.GetTexture("square.png"),
                    BackgroundSprite =
                    {
                        Color = Color.BlueViolet
                    },

                    Text = "SMEtoMER",

                    OnClick = () => ScheduleLoad(() => Load("DXR OFF", "DXR ON"))
                });

                Add(new Button
                {
                    Size = new Vector2(200, 100),

                    Background = TextureStore.GetTexture("square.png"),
                    BackgroundSprite =
                    {
                        Color = Color.Blue
                    },

                    Text = "NORMALtoHEIGHT",

                    OnClick = () => ScheduleLoad(() => Heightmap("DXR ON"))
                });
            }
        }

        public static void Load(string input, string output)
        {
            Benchmark b = new Benchmark("Convert Pack to DXR", true);

            List<string> textures = new List<string>();

            Storage sme = ApplicationDataStorage.GetStorage($"{input}\\assets\\minecraft\\textures\\block");
            Storage mer = ApplicationDataStorage.GetStorage($"{output}");

            //index the files
            foreach (string file in sme.GetFiles())
            {
                if (!file.Contains(SPECULAR_EXTENTION) && !file.Contains(NORMAL_EXTENTION))
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    textures.Add(name);
                    Logger.Log($"Found {name}...");
                }
            }

            Benchmark o;

            //convert them now
            for (int i = 0; i < textures.Count; i++)
            {
                o = new Benchmark($"Convert {textures[i]} to DXR", true);

                Logger.Log($"Converting {textures[i]}...");
                File.Copy($"{sme.Path}\\{textures[i]}.png", $"{mer.Path}\\{textures[i]}.png");
                File.Copy($"{sme.Path}\\{textures[i]}{NORMAL_EXTENTION}", $"{mer.Path}\\{textures[i]}_normal.png");

                //create the json file
                using (FileStream json = mer.CreateFile($"{textures[i]}.texture_set.json"))
                using (StreamWriter writer = new StreamWriter(json))
                    writer.Write(GetJSON(textures[i], $"{textures[i]}_mer", $"{textures[i]}_normal"));

                Stream stream = sme.GetStream(textures[i] + SPECULAR_EXTENTION);
                Bitmap bitmap = new Bitmap(stream);
                //bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);

                //if it isnt a square then rip for now
                int size = bitmap.Width;

                //SME to MER isn't a straight copy sadly, but this should convert it quite nicely
                byte[] data = ConvertSMEtoMER(bitmap.To32BppRgba());

                bitmap.Dispose();
                stream.Dispose();

                bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

                //Write MER array to bitmap now
                int p = 0;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Color c = Color.FromArgb(
                            255,
                            data[p],
                            data[p + 1],
                            data[p + 2]
                        );
                        bitmap.SetPixel(x, y, c);
                        p += 3;
                    }
                }

                //TODO: Remove Transparency before saving!
                stream = mer.GetStream($"{textures[i]}_mer.png", FileAccess.Write, FileMode.Create);
                bitmap.Save(stream, ImageFormat.Png);

                Logger.Log($"Saved {textures[i]}!");

                //cleanup
                bitmap.Dispose();
                stream.Dispose();

                o.Finish();
            }

            Logger.Log("Convertion Complete!");
            b.Finish();
        }

        public static void Heightmap(string input)
        {
            Benchmark b = new Benchmark("Convert DXR to HEIGHT", true);

            List<string> textures = new List<string>();

            Storage folder = ApplicationDataStorage.GetStorage($"{input}");

            //index the files
            foreach (string file in folder.GetFiles())
            {
                if (!file.Contains(".texture_set.json") && !file.Contains("_mer.png") && !file.Contains("_normal.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    textures.Add(name);
                    Logger.Log($"Found {name}...");
                }
            }

            Benchmark o;

            //convert them now
            for (int i = 0; i < textures.Count; i++)
            {
                o = new Benchmark($"Convert {textures[i]} to HEIGHT", true);

                Logger.Log($"Converting {textures[i]}...");

                //delete the old one
                folder.DeleteFile($"{textures[i]}.texture_set.json");

                //create the new json file
                using (FileStream json = folder.CreateFile($"{textures[i]}.texture_set.json"))
                using (StreamWriter writer = new StreamWriter(json))
                    writer.Write(GetNewJSON(textures[i], $"{textures[i]}_mer", $"{textures[i]}_height"));

                Stream stream = folder.GetStream(textures[i] + "_normal.png");
                Bitmap bitmap = new Bitmap(stream);
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

                //if it isnt a square then rip for now
                int size = bitmap.Width;

                //convert it to an 8bpp array
                byte[] data = ConvertNORMALtoHEIGHT(bitmap.To24BppRgb());

                bitmap.Dispose();
                stream.Dispose();

                FIBITMAP bit = FreeImage.Allocate(size, size, 8);

                //Write HEIGHT array to bitmap now
                int p = 0;
                for (uint y = 0; y < size; y++)
                {
                    for (uint x = 0; x < size; x++)
                    {
                        FreeImage.SetPixelIndex(bit, x, y, ref data[p]);
                        p++;
                    }
                }

                stream = folder.GetStream($"{textures[i]}_height.png", FileAccess.Write, FileMode.Create);
                FreeImage.SaveToStream(bit, stream, FREE_IMAGE_FORMAT.FIF_PNG);

                Logger.Log($"Saved {textures[i]}!");

                //cleanup
                stream.Dispose();
                folder.DeleteFile(textures[i] + "_normal.png");

                o.Finish();
            }

            b.Finish();
        }

        public static byte[] ConvertSMEtoMER(byte[] file)
        {
            //we don't want alpha
            byte[] adjusted = new byte[file.Length / 4 * 3];

            //SME -> MER
            int o = 0;
            for (int i = 0; i < adjusted.Length; i += 3)
            {
                adjusted[i] = file[o + 1];
                adjusted[i + 1] = file[o + 2];
                adjusted[i + 2] = (byte) (255 - file[o]);
                o += 4;
            }

            return adjusted;
        }

        public static byte[] ConvertNORMALtoHEIGHT(byte[] file)
        {
            float min = 255;
            float max = 0;

            byte[] adjusted = new byte[file.Length / 3];

            int o = 0;
            //NORMAL -> HEIGHT
            for (int i = 0; i < file.Length; i += 3)
            {
                //byte R = file[i];
                //byte G = file[i + 1];
                byte B = file[i + 2];

                //byte avg = (byte)((R + G + B) / 3);
                byte avg = B;
                adjusted[o] = avg;

                min = Math.Min(min, avg);
                max = Math.Max(max, avg);

                o++;
            }

            //remap averages
            for (int i = 0; i < adjusted.Length; i++)
                adjusted[i] = (byte)PrionMath.Scale(adjusted[i], min, max, 0, 255);

            return adjusted;
        }

        public static string GetJSON(string color, string mer, string normal) =>
            "{\"format_version\": \"1.16.100\",\"minecraft:texture_set\": {\"color\": \"" + color +
            "\",\"metalness_emissive_roughness\": \"" + mer + "\",\"normal\": \"" + normal + "\"}}";

        public static string GetNewJSON(string color, string mer, string height) =>
            "{\"format_version\": \"1.16.100\",\"minecraft:texture_set\": {\"color\": \"" + color +
            "\",\"metalness_emissive_roughness\": \"" + mer + "\",\"heightmap\": \"" + height + "\"}}";
    }
}
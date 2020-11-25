// Copyright (c) 2018-2020 Shawn Bozek.
// Licensed under EULA https://docs.google.com/document/d/1xPyZLRqjLYcKMxXLHLmA5TxHV-xww7mHYVUuWLt2q9g/edit?usp=sharing

#define HEADLESS

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using Prion.Nucleus.Debug;
using Prion.Nucleus.IO;
#if HEADLESS
using System;
using Prion.Nucleus;
#else
using System.Numerics;
using Prion.Mitochondria;
using Prion.Mitochondria.Graphics.Contexts;
using Prion.Mitochondria.Graphics.Roots;
using Prion.Mitochondria.Graphics.UI;
#endif

namespace BedRocker
{
    public class BedRocker :
#if HEADLESS
        Application
#else
        Game
#endif
    {
        public const string SPECULAR_EXTENTION = "_s.png";
        public const string NORMAL_EXTENTION = "_n.png";

        public static void Main(string[] args)
        {
            using (BedRocker rocker = new BedRocker(args))
            {
                rocker.UpdateFrequency = 10;
#if HEADLESS
                rocker.Start();
#else
                rocker.Start(new MainMenu());
#endif
                rocker.Dispose();
            }
        }

        protected override bool UseLocalDataStorage => true;

        public BedRocker(string[] args) : base("BedRocker", args)
        {
        }

#if HEADLESS
        protected override void Update()
        {
            base.Update();
            Load("RTX OFF", "RTX ON");
            Logger.SystemConsole("Press Enter To Close", ConsoleColor.Magenta);
            Console.ReadLine();
            Exit();
        }
#else
        protected override GraphicsContext GetContext(string name)
        {
            switch (name)
            {
                default:
                    return base.GetContext("GL46");
                case "Legacy":
                case "GL41":
                    return base.GetContext("GL41");
                case "DX12":
                    return base.GetContext("DX12");
            }
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

                    Text = "DO IT!",

                    OnClick = () => ScheduleLoad(() => Load("RTX OFF", "RTX ON"))
                });
            }
        }
#endif

        public static void Load(string input, string output)
        {
            List<string> textures = new List<string>();

            Storage sme = ApplicationDataStorage.GetStorage($"{input}\\assets\\minecraft\\textures\\block");
            Storage mer = ApplicationDataStorage.GetStorage($"{output}");

            foreach (string file in sme.GetFiles())
            {
                if (!file.Contains(SPECULAR_EXTENTION) && !file.Contains(NORMAL_EXTENTION))
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    textures.Add(name);
                    Logger.Log($"Found {name}...");
                }
            }

            for (int i = 0; i < textures.Count; i++)
            {
                Logger.Log($"Converting {textures[i]}...");
                File.Copy($"{sme.Path}\\{textures[i]}.png", $"{mer.Path}\\{textures[i]}.png");
                File.Copy($"{sme.Path}\\{textures[i]}{NORMAL_EXTENTION}", $"{mer.Path}\\{textures[i]}_normal.png");

                //create the json file
                using (FileStream json = mer.CreateFile($"{textures[i]}.texture_set.json"))
                using (StreamWriter writer = new StreamWriter(json))
                    writer.Write(GetJSON(textures[i], $"{textures[i]}_mer", $"{textures[i]}_normal"));

                Stream stream = sme.GetStream(textures[i] + SPECULAR_EXTENTION);
                Bitmap bitmap = new Bitmap(stream);

                //if it isnt a square then rip for now
                int size = bitmap.Width;

                //SME to MER isn't a straight copy sadly, but this should convert it quite nicely
                byte[] data = ConvertSMEtoMER(To32BppRgba(bitmap));

                bitmap.Dispose();
                stream.Dispose();

                bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

                //Write MER array to bitmap now
                int p = 0;
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
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
            }

            Logger.Log("Convertion Complete!");
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

        public static string GetJSON(string color, string mer, string normal) =>
            "{\"format_version\": \"1.16.100\",\"minecraft:texture_set\": {\"color\": \"" + color +
            "\",\"metalness_emissive_roughness\": \"" + mer + "\",\"normal\": \"" + normal + "\"}}";


        /// <summary>
        ///     byte[] Prion.Mitochondria.Graphics.GraphicsUtilities.To32BppRgba(this Bitmap bitmap);
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static byte[] To32BppRgba(Bitmap bitmap)
        {
            Rectangle sourceArea = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            List<byte> data = new List<byte>();

            BitmapData bitmapData = bitmap.LockBits(sourceArea, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

            // Convert all pixels 
            for (int y = 0; y < bitmap.Height; y++)
            {
                int offset = bitmapData.Stride * y;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    byte B = Marshal.ReadByte(bitmapData.Scan0, offset++);
                    byte G = Marshal.ReadByte(bitmapData.Scan0, offset++);
                    byte R = Marshal.ReadByte(bitmapData.Scan0, offset++);
                    byte A = Marshal.ReadByte(bitmapData.Scan0, offset++);

                    data.Add(R);
                    data.Add(G);
                    data.Add(B);
                    data.Add(A);
                }
            }

            bitmap.UnlockBits(bitmapData);
            return data.ToArray();
        }
    }
}
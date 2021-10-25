// Copyright (c) 2018-2020 Shawn Bozek.
// Licensed under EULA https://docs.google.com/document/d/1xPyZLRqjLYcKMxXLHLmA5TxHV-xww7mHYVUuWLt2q9g/edit?usp=sharing

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Prion.Nucleus.Debug;
using Prion.Nucleus.Debug.Benchmarking;
using Prion.Nucleus.IO;
using System.Numerics;
using FreeImageAPI;
using Prion.Golgi.Graphics.Overlays;
using Prion.Mitochondria;
using Prion.Mitochondria.Graphics;
using Prion.Mitochondria.Graphics.Drawables;
using Prion.Mitochondria.Graphics.Layers._2D;
using Prion.Mitochondria.Graphics.Roots;
using Prion.Mitochondria.Graphics.Sprites;
using Prion.Mitochondria.Graphics.Text;
using Prion.Mitochondria.Graphics.UI;
using Prion.Nucleus;
using Prion.Nucleus.Settings;
using Prion.Nucleus.Threads;
using Prion.Nucleus.Utilities;
using Prion.Nucleus.Utilities.Vectors;

namespace BedRocker
{
    public class BedRocker : Game
    {
        public const string SPECULAR_EXTENTION = "_s.png";
        public const string NORMAL_EXTENTION = "_n.png";

        public static void Main(string[] args)
        {

            #region Startup


            //Do Launch args stuff first incase someone wants STATIC READONLYS!!!!
            List<string> launch = new(args);

#if !PUBLISH || PERSONAL
            if (!launch.Any(arg => arg.Contains("Features")))
                launch.Add($"Features={Features.Radioactive}");
#endif

            MitochondriaLaunchArgs m = new()
            {
                Name = "Bedrocker",
            };
            MitochondriaLaunchArgs.DisableAudio = true;
            MitochondriaLaunchArgs.ProccessArgs(launch.ToArray());


            #endregion


            BedRocker rocker = new BedRocker(m);
            rocker.Start(new MainMenu());
        }

        protected override bool UseLocalDataStorage => true;

        protected BedRocker(MitochondriaLaunchArgs args) : base(args)
        {
            int dtco = Settings.GetInt(NucleusSetting.DynamicThreadCountOverride);

            if (dtco <= -1)
            {
                while (FreeProcessors > 0)
                    CreateDynamicTask();
            }
            else
            {
                while (DynamicThreads.Count < dtco)
                    CreateDynamicTask();
            }
        }

        private class MainMenu : Root
        {
            private readonly TextBox r;
            private readonly TextBox g;
            private readonly TextBox b;

            public byte R = 255;
            public byte G = 255;
            public byte B = 255;

            public MainMenu()
            {
                Renderer.Window.CursorHidden = true;

                Add(new Layer2D<Sprite>
                {
                    Child = new Box
                    {
                        Size = new Vector2(8000),
                        Color = Color.DodgerBlue,
                        Alpha = 0.8f
                    }
                });

                Add(new ListLayer<Button>
                {
                    Size = new Vector2(200, 550),
                    Position = new Vector2(0, -275),
                    Spacing = 10,

                    Children = new []
                    {
                        new Button
                        {
                            Size = new Vector2(200, 100),

                            Background = TextureStore.GetTexture("square.png"),
                            BackgroundSprite =
                            {
                                Color = Color.BlueViolet
                            },

                            Text = "SMEtoMER",

                            OnClick = () => ScheduleLoad(() => SMEtoMER("DXR OFF", "DXR ON"))
                        },
                        new Button
                        {
                            Size = new Vector2(200, 100),

                            Background = TextureStore.GetTexture("square.png"),
                            BackgroundSprite =
                            {
                                Color = Color.DarkOrchid
                            },

                            Text = "LABtoMER",

                            Disabled = true,
                            OnClick = () => ScheduleLoad(() => LABtoMER("DXR OFF", "DXR ON"))
                        },
                        new Button
                        {
                            Size = new Vector2(200, 100),

                            Background = TextureStore.GetTexture("square.png"),
                            BackgroundSprite =
                            {
                                Color = Color.DeepPink
                            },

                            Text = "Optimize",

                            Disabled = true,
                            //OnClick = () => ScheduleLoad(() => Optimize("DXR ON"))
                        },
                        new Button
                        {
                            Size = new Vector2(200, 100),

                            Background = TextureStore.GetTexture("square.png"),
                            BackgroundSprite =
                            {
                                Color = Color.Blue
                            },

                            Text = "NORMALtoHEIGHT",

                            OnClick = () => ScheduleLoad(() => Heightmap("DXR ON"))
                        },
                        new Button
                        {
                            Size = new Vector2(200, 100),

                            Background = TextureStore.GetTexture("square.png"),
                            BackgroundSprite =
                            {
                                Color = Color.OrangeRed
                            },

                            Text = "MERman",

                            OnClick = () => ScheduleLoad(() => Glow("DXR ON", R, G, B))
                        },
                        new Button
                        {
                            Size = new Vector2(200, 100),

                            Background = TextureStore.GetTexture("square.png"),
                            BackgroundSprite =
                            {
                                Color = Color.MediumSeaGreen
                            },

                            Text = "Clean",

                            OnClick = () => ScheduleLoad(() => Clean(new[]
                            {
                                "DXR OFF",
                                "DXR ON"
                            }))
                        }
                    }
                });

                Add(new ListLayer<IDrawable2D>
                {
                    Size = new Vector2(100, 200),
                    Position = new Vector2(240, 0),
                    Spacing = 10,

                    Children = new IDrawable2D[]
                    {
                        new Text2D
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = "RGB (0 - 255)",
                            FontScale = 0.5f
                        },
                        new Text2D
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = "R",
                            FontScale = 0.25f
                        },
                        r = new TextBox
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = R.ToString(),
                            Scale = new Vector2(0.25f),
                            OnEnter = value =>
                            {
                                byte v;
                                try
                                {
                                    v = byte.Parse(value);
                                }
                                catch
                                {
                                    v = 255;
                                }

                                R = Math.Clamp(v, (byte)0, (byte)255);
                                r.Text = R.ToString();
                            }
                        },

                        new Text2D
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = "G",
                            FontScale = 0.25f
                        },
                        g = new TextBox
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = G.ToString(),
                            Scale = new Vector2(0.25f),
                            OnEnter = value =>
                            {
                                byte v;
                                try
                                {
                                    v = byte.Parse(value);
                                }
                                catch
                                {
                                    v = 255;
                                }

                                G = Math.Clamp(v, (byte)0, (byte)255);
                                g.Text = G.ToString();
                            }
                        },

                        new Text2D
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = "B",
                            FontScale = 0.25f
                        },
                        b = new TextBox
                        {
                            Origin = Mounts.TopLeft,
                            ParentOrigin = Mounts.TopLeft,
                            Text = B.ToString(),
                            Scale = new Vector2(0.25f),
                            OnEnter = value =>
                            {
                                byte v;
                                try
                                {
                                    v = byte.Parse(value);
                                }
                                catch
                                {
                                    v = 255;
                                }

                                B = Math.Clamp(v, (byte)0, (byte)255);
                                b.Text = B.ToString();
                            }
                        },
                    }
                });

                Add(new PerformanceDisplay(DisplayType.FPS));
                Add(Cursor = new Cursor());
            }

            public override void LoadingComplete()
            {
                Parent.UpdateFrequency = 30;
                Renderer.DrawFrequency = 30;
                base.LoadingComplete();
            }

            private Benchmark benchmark;
            private bool running;

            public override void Update()
            {
                base.Update();

                if (!ThreadsRunning() && running)
                {
                    running = false;

                    Logger.Log("\n\n\n\nConvertion Complete!");
                    benchmark.Finish();
                }
            }

            public void SMEtoMER(string input, string output)
            {
                if (ThreadsRunning()) return;

                benchmark = new Benchmark("Convert Pack to DXR", true);

                ConcurrentQueue<string> textures = new ConcurrentQueue<string>();

                if (ApplicationDataStorage.Exists(output))
                    ApplicationDataStorage.DeleteDirectory(output, true);

                Storage sme = ApplicationDataStorage.GetStorage($"{input}\\assets\\minecraft\\textures\\block");
                Storage mer = ApplicationDataStorage.GetStorage($"{output}");

                //index the files
                foreach (string file in sme.GetFiles())
                {
                    if (!file.Contains(SPECULAR_EXTENTION) && !file.Contains(NORMAL_EXTENTION))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        textures.Enqueue(name);
                        Logger.Log($"Found {name}...");
                    }
                }

                foreach (DynamicThread thread in DynamicThreads)
                    thread.Task = convert;

                running = true;
                RunThreads();

                void convert()
                {
                    Benchmark o;

                    //convert them now
                    while (textures.TryDequeue(out string t))
                    {
                        o = new Benchmark($"Convert {t} to DXR", true);

                        string java = t;
                        string bedrock = GetBedrockTextureName(java);
                        string folder = string.Empty;

                        Logger.Log($"Converting {java} to {bedrock}...");

                        Stream stream;
                        Bitmap bitmap;

                        int size;
                        byte[] data;
                        int p;

                        switch (java)
                        {
                            default:
                                File.Copy($"{sme.Path}\\{java}.png", $"{mer.Path}\\{bedrock}.png");
                                break;
                            case "grass_block_top":
                            case "grass_block_side_overlay":
                                stream = sme.GetStream($"{java}.png");
                                bitmap = new Bitmap(stream);

                                size = bitmap.Width;

                                data = bitmap.BgraToRgba32Bpp();

                                bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

                                //Write grayscaled array to bitmap now
                                p = 0;
                                for (int y = 0; y < size; y++)
                                {
                                    for (int x = 0; x < size; x++)
                                    {
                                        int gray = grayscale(new Vector3(data[p], data[p + 1], data[p + 2]));
                                        Color c = Color.FromArgb(
                                            data[p + 3],
                                            gray,
                                            gray,
                                            gray);
                                        bitmap.SetPixel(x, y, c);
                                        p += 3;
                                    }
                                }

                                stream = mer.GetStream($"{bedrock}.png", FileAccess.Write, FileMode.Create);
                                bitmap.Save(stream, ImageFormat.Png);

                                bitmap.Dispose();
                                stream.Dispose();
                                break;

                            case "warped_stem":
                            case "warped_nylium":
                            case "crimson_stem":
                            case "crimson_nylium":
                                folder = "huge_fungus\\";
                                if (!Directory.Exists($"{mer.Path}\\huge_fungus"))
                                    Directory.CreateDirectory($"{mer.Path}\\huge_fungus");

                                File.Copy($"{sme.Path}\\{java}.png", $"{mer.Path}\\{folder}{bedrock}.png");
                                break;
                        }

                        File.Copy($"{sme.Path}\\{java}{NORMAL_EXTENTION}", $"{mer.Path}\\{folder}{bedrock}_normal.png");

                        //create the json file
                        using (FileStream json = mer.CreateFile($"{folder}{bedrock}.texture_set.json"))
                        using (StreamWriter writer = new StreamWriter(json))
                            writer.Write(GetJSON(bedrock, $"{bedrock}_mer", $"{bedrock}_normal"));

                        stream = sme.GetStream(java + SPECULAR_EXTENTION);
                        bitmap = new Bitmap(stream);

                        //if it isnt a square then rip for now
                        size = bitmap.Width;

                        //SME to MER isn't a straight copy sadly, but this should convert it quite nicely
                        data = ConvertSMEtoMER(bitmap.BgraToRgba32Bpp());

                        bitmap.Dispose();
                        stream.Dispose();

                        bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

                        //Write MER array to bitmap now
                        p = 0;
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
                        stream = mer.GetStream($"{folder}{bedrock}_mer.png", FileAccess.Write, FileMode.Create);
                        bitmap.Save(stream, ImageFormat.Png);

                        Logger.Log($"Saved {folder}{bedrock}!");

                        //cleanup
                        bitmap.Dispose();
                        stream.Dispose();

                        o.Finish();
                    }
                }

                int grayscale(Vector3 color)
                {
                    const float r = 0.299f;
                    const float g = 0.587f;
                    const float b = 0.114f;

                    float gray;

                    gray = Vector3.Dot(color, new Vector3(r, g, b));

                    return (int)gray;
                }
            }

            public void LABtoMER(string input, string output)
            {
                Logger.SystemConsole($"NOT IMPLEMENTED! ({input} to {output})", ConsoleColor.Yellow);
            }
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
                byte[] data = ConvertNORMALtoHEIGHT(bitmap.BgrToRgb24Bpp());

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

        public static void Glow(string input, byte r, byte g, byte b)
        {
            Benchmark c = new Benchmark("Convert DXR to GLOW", true);

            List<string> textures = new List<string>();

            Storage folder = ApplicationDataStorage.GetStorage($"{input}");

            //index the files
            foreach (string file in folder.GetFiles())
            {
                if (!file.Contains(".texture_set") && !file.Contains("_mer") && !file.Contains("_normal"))
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
                if (!folder.Exists(textures[i] + "_mer.png"))
                {
                    Logger.Warning($"Skipping {textures[i]}...", LogType.IO);
                    continue;
                }

                o = new Benchmark($"Convert {textures[i]} to GLOW", true);

                Logger.Log($"Converting {textures[i]}...");

                Stream stream = folder.GetStream(textures[i] + "_mer.png");
                Bitmap bitmap = new Bitmap(stream);

                //if it isnt a square then rip for now
                int size = bitmap.Width;

                bitmap.Dispose();
                stream.Dispose();

                FIBITMAP bit = FreeImage.Allocate(size, size, 24);

                //Write GLOW array to bitmap now
                for (uint y = 0; y < size; y++)
                {
                    for (uint x = 0; x < size; x++)
                    {
                        RGBQUAD quad = new RGBQUAD(Color.FromArgb(r, g, b));
                        FreeImage.SetPixelColor(bit, x, y, ref quad);
                    }
                }

                folder.DeleteFile(textures[i] + "_mer.png");
                stream = folder.GetStream($"{textures[i]}_mer.png", FileAccess.Write, FileMode.Create);
                FreeImage.SaveToStream(bit, stream, FREE_IMAGE_FORMAT.FIF_PNG);

                Logger.Log($"Saved {textures[i]}!");

                //cleanup
                stream.Dispose();

                o.Finish();
            }

            c.Finish();
        }

        public static void Clean(string[] folders)
        {
            for (int i = 0; i < folders.Length; i++)
                if (ApplicationDataStorage.Exists(folders[i]))
                    ApplicationDataStorage.DeleteDirectory(folders[i], true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
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
                adjusted[i] = (byte)PrionMath.Remap(adjusted[i], min, max, 0, 255);

            return adjusted;
        }

        public static string GetJSON(string color, string mer, string normal) =>
            "{\"format_version\": \"1.16.100\",\"minecraft:texture_set\": {\"color\": \"" + color +
            "\",\"metalness_emissive_roughness\": \"" + mer + "\",\"normal\": \"" + normal + "\"}}";

        public static string GetNewJSON(string color, string mer, string height) =>
            "{\"format_version\": \"1.16.100\",\"minecraft:texture_set\": {\"color\": \"" + color +
            "\",\"metalness_emissive_roughness\": \"" + mer + "\",\"heightmap\": \"" + height + "\"}}";

        public static string GetBedrockTextureName(string java)
        {
            return java switch
            {
                "grass_block_top" => "grass_top",
                "grass_block_side_overlay" => "grass_side",
                "grass_block_snow" => "grass_side_snowed",

                "furnace_front" => "furnace_front_off",

                "tall_grass_top" => "double_plant_grass_top",
                "tall_grass_bottom" => "double_plant_grass_bottom",

                "oak_log_top" => "log_oak_top",
                "oak_log" => "log_oak",
                "oak_planks" => "planks_oak",
                "oak_door_top" => "door_oak_upper",
                "oak_door_bottom" => "door_oak_lower",

                "spruce_log_top" => "log_spruce_top",
                "spruce_log" => "log_spruce",
                "spruce_planks" => "planks_spruce",
                "spruce_door_top" => "door_spruce_upper",
                "spruce_door_bottom" => "door_spruce_lower",

                "birch_log_top" => "log_birch_top",
                "birch_log" => "log_birch",
                "birch_planks" => "planks_birch",
                "birch_door_top" => "door_birch_upper",
                "birch_door_bottom" => "door_birch_lower",

                "jungle_log_top" => "log_jungle_top",
                "jungle_log" => "log_jungle",
                "jungle_planks" => "planks_jungle",
                "jungle_door_top" => "door_jungle_upper",
                "jungle_door_bottom" => "door_jungle_lower",

                "acacia_log_top" => "log_acacia_top",
                "acacia_log" => "log_acacia",
                "acacia_planks" => "planks_acacia",
                "acacia_door_top" => "door_acacia_upper",
                "acacia_door_bottom" => "door_acacia_lower",

                "dark_oak_log_top" => "log_big_oak_top",
                "dark_oak_log" => "log_big_oak",
                "dark_oak_planks" => "planks_big_oak",
                "dark_oak_door_top" => "door_dark_oak_upper",
                "dark_oak_door_bottom" => "door_dark_oak_lower",

                "warped_stem" => "warped_stem_side",
                "warped_nylium" => "warped_nylium_top",

                "crimson_stem" => "crimson_log_side",
                "crimson_nylium" => "crimson_nylium_top",

                "nether_wart_stage0" => "nether_wart_stage_0",
                "nether_wart_stage1" => "nether_wart_stage_1",
                "nether_wart_stage2" => "nether_wart_stage_2",

                "bricks" => "brick",

                "mossy_cobblestone" => "cobblestone_mossy",
                "mossy_stone_bricks" => "stonebrick_mossy",

                "andesite" => "stone_andesite",
                "diorite" => "stone_diorite",
                "granite" => "stone_granite",

                "polished_andesite" => "stone_andesite_smooth",
                "polished_diorite" => "stone_diorite_smooth",
                "polished_granite" => "stone_granite_smooth",

                "cracked_stone_bricks" => "stonebrick_cracked",

                "nether_bricks" => "nether_brick",

                "end_stone_bricks" => "end_bricks",

                "podzol_top" => "dirt_podzol_top",
                "podzol_side" => "dirt_podzol_side",

                "black_wool" => "wool_colored_black",
                "blue_wool" => "wool_colored_blue",
                "brown_wool" => "wool_colored_brown",
                "cyan_wool" => "wool_colored_cyan",
                "gray_wool" => "wool_colored_gray",
                "green_wool" => "wool_colored_green",
                "light_blue_wool" => "wool_colored_light_blue",
                "light_gray_wool" => "wool_colored_light_gray",
                "lime_wool" => "wool_colored_lime",
                "magenta_wool" => "wool_colored_magenta",
                "orange_wool" => "wool_colored_orange",
                "pink_wool" => "wool_colored_pink",
                "purple_wool" => "wool_colored_purple",
                "red_wool" => "wool_colored_red",
                "white_wool" => "wool_colored_white",
                "yellow_wool" => "wool_colored_yellow",

                "black_terracotta" => "glazed_terracotta_black",
                "blue_terracotta" => "glazed_terracotta_blue",
                "brown_terracotta" => "glazed_terracotta_brown",
                "cyan_terracotta" => "glazed_terracotta_cyan",
                "gray_terracotta" => "glazed_terracotta_gray",
                "green_terracotta" => "glazed_terracotta_green",
                "light_blue_terracotta" => "glazed_terracotta_light_blue",
                "light_gray_terracotta" => "glazed_terracotta_silver",
                "lime_terracotta" => "glazed_terracotta_lime",
                "magenta_terracotta" => "glazed_terracotta_magenta",
                "orange_terracotta" => "glazed_terracotta_orange",
                "pink_terracotta" => "glazed_terracotta_pink",
                "purple_terracotta" => "glazed_terracotta_purple",
                "red_terracotta" => "glazed_terracotta_red",
                "terracotta" => "glazed_terracotta",
                "white_terracotta" => "glazed_terracotta_white",
                "yellow_terracotta" => "glazed_terracotta_yellow",
                _ => java
            };
        }
    }
}
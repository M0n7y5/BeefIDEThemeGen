using ImageProcessor;
using ImageProcessor.Imaging;
using ImageProcessor.Imaging.Formats;
using MoreLinq;
using Serilog;
using SkiaSharp;
using Svg;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BeefIDEThemeGen
{
    internal class Program
    {
        private static Dictionary<string, (int width, int height, int scale)> Sizes = new Dictionary<string, (int width, int height, int scale)>();
        private static Dictionary<string, (int size, double sigma)> SharpenFilter = new Dictionary<string, (int size, double sigma)>();

        private static List<List<string>> ImageRows;// = new List<List<string>>();

        //width and height of rectangle
        private static int _DefaultWH = 80;

        private static string _CurrentDirectoryPath;
        private static string _CurrentImagesPath;

        private static ILogger L;

        private static void Main(string[] args)
        {
            L = new LoggerConfiguration()
              .MinimumLevel.Information()
              .WriteTo.Console()
              .CreateLogger();

            L.Information("Beef Theme Generator v0.1");

            // we are working with 4x scale by default so we must go backwards
            Sizes.Add("UI.png", (400, 160, 4));
            Sizes.Add("UI_2.png", (800, 320, 2));
            Sizes.Add("UI_4.png", (1600, 640, 1));

            ImageRows = Enum.GetNames(typeof(ImageIdx))
                .Batch(20, x => x.ToList())
                .ToList();

            _CurrentDirectoryPath = Environment.CurrentDirectory;
            _CurrentImagesPath = _CurrentDirectoryPath + "\\images";

            FilterConfig cfg = null;

            if (File.Exists(_CurrentDirectoryPath + "\\FilterConfig.yaml"))
            {
                var cfgText = File.ReadAllText(_CurrentDirectoryPath + "\\FilterConfig.yaml");

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)  // see height_in_inches in sample yml
                    .Build();

                cfg = deserializer.Deserialize<FilterConfig>(cfgText);
            }

            foreach (var size in Sizes)
            {
                var info = size.Value;
                var fileName = size.Key;
                var wh = _DefaultWH / info.scale;

                // Rows of bitmaps from loaded files and rasterized vector graphics
                List<List<(Bitmap data, string name)>> bitmaps = GetRowBitmaps(wh);

                using (var img = new Bitmap(info.width, info.height))
                using (ImageFactory canvas = new ImageFactory())
                {
                    canvas.Load(img);

                    var originY = 0;
                    foreach (var row in bitmaps)
                    {
                        var originX = 0;
                        foreach (var image in row)
                        {
                            if (image.data == null)
                            {
                                // skip images that are not loaded
                                originX += wh;
                                L.Warning($"Skipping {image.name}! Data not present ...");
                                continue;
                            }

                            using (ImageFactory layerCanvas = new ImageFactory())
                            {
                                layerCanvas
                                    .Load(image.data);

                                if (info.scale == 4) // apply sharpening filter to only smallest scale
                                    if (cfg != null)
                                        if (cfg.ApplyFilterOn.Contains(image.name))
                                            layerCanvas.GaussianSharpen(new GaussianLayer(cfg.SharpeningSize, cfg.SharpeningSigma));

                                var layer = new ImageLayer()
                                {
                                    Image = layerCanvas.Image,
                                    Opacity = 100,
                                    Position = new Point(originX, originY),
                                    Size = new Size(wh, wh)
                                };

                                L.Information($"Adding {image.name}, X:{originX}, Y:{originY}, Size:{wh}x{wh}");

                                canvas.Overlay(layer);
                            }

                            originX += wh;
                        }

                        // adding offset to Y origin after each row is finished
                        originY += wh;
                    }

                    canvas
                        .Format(new PngFormat())
                        .Save(_CurrentDirectoryPath + $"\\{fileName}");
                }
            }

            L.Information("Done!");
            Console.ReadLine();
        }

        private static List<List<(Bitmap data, string name)>> GetRowBitmaps(int size)
        {
            var result = new List<List<(Bitmap data, string name)>>();

            foreach (var row in ImageRows)
            {
                var list = new List<(Bitmap data, string name)>();

                foreach (var image in row)
                {
                    // Currently we gonna support only SVG and PNG
                    (Bitmap data, string name) res = default;
                    res.name = image;
                    if (File.Exists(_CurrentImagesPath + $"\\{image}.svg"))
                    {
                        res.data = GetImageDataFromSVG(_CurrentImagesPath + $"\\{image}.svg", size);
                    }
                    else if (File.Exists(_CurrentImagesPath + $"\\{image}.png"))
                    {
                        res.data = GetImageDataFromPNG(_CurrentImagesPath + $"\\{image}.png", size);
                    }
                    else
                    {
                        // Image not found skipping
                        L.Warning($"Image for {image} was not found! Skipping ...");
                    }

                    list.Add(res);
                }

                result.Add(list);
            }

            return result;
        }

        private static Bitmap GetImageDataFromPNG(string path, int size)
        {
            using (var stream = new MemoryStream())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    // Load, resize, set the format and quality and save an image.
                    imageFactory
                        .Load(path)
                        .Resize(new Size(size, size))
                        .Save(stream);

                    return new Bitmap(stream);
                }
            }
        }

        private static Bitmap GetImageDataFromSVG(string path, int size)
        {
            var svgFile = SvgDocument.Open(path);

            if (path.Contains("DropShadow") || path.Contains("GlowDot") || path.Contains("WhiteCircle"))
            {
                svgFile.Height = size;
                svgFile.Width = size;
                using (var stream = new MemoryStream())
                using (var svg = new SKSvg())
                {
                    svg.FromSvgDocument(svgFile);
                    svg.Save(stream, SKColor.Empty);

                    return new Bitmap(stream);
                }
            }
            else
            {
                return svgFile.Draw(size, size);
            }
        }

        public enum ImageIdx
        {
            Bkg,
            Window,
            Dots,
            RadioOn,
            RadioOff,
            MainBtnUp,
            MainBtnDown,
            BtnUp,
            BtnOver,
            BtnDown,
            Separator,
            TabActive,
            TabActiveOver,
            TabInactive,
            TabInactiveOver,
            EditBox,
            Checkbox,
            CheckboxOver,
            CheckboxDown,
            Check,

            Close,
            CloseOver,
            DownArrow,
            GlowDot,
            ArrowRight,
            WhiteCircle,
            DropMenuButton,
            ListViewHeader,
            ListViewSortArrow,
            Outline,
            Scrollbar,
            ScrollbarThumbOver,
            ScrollbarThumb,
            ScrollbarArrow,
            ShortButton,
            ShortButtonDown,
            VertScrollbar,
            VertScrollbarThumbOver,
            VertScrollbarThumb,
            VertScrollbarArrow,

            VertShortButton,
            VertShortButtonDown,
            Grabber,
            DropShadow,
            Menu,
            MenuSepVert,
            MenuSepHorz,
            MenuSelect,
            TreeArrow,
            UIPointer,
            UIImage,
            UIComposition,
            UILabel,
            UIButton,
            UIEdit,
            UICombobox,
            UICheckbox,
            UIRadioButton,
            UIListView,
            UITabView,

            EditCorners,
            EditCircle,
            EditPathNode,
            EditPathNodeSelected,
            EditAnchor,
            UIBone,
            UIBoneJoint,
            VisibleIcon,
            LockIcon,
            LeftArrow,
            KeyframeMakeOff,
            RightArrow,
            LeftArrowDisabled,
            KeyframeMakeOn,
            RightArrowDisabled,
            TimelineSelector,
            TimelineBracket,
            KeyframeOff,
            KeyframeOn,
            LinkedIcon,

            CheckboxLarge,
            ComboBox,
            ComboEnd,
            ComboSelectedIcon,
            LinePointer,
            RedDot,
            Document,
            ReturnPointer,
            RefreshArrows,
            MoveDownArrow,
            IconObject,
            IconObjectDeleted,
            IconObjectAppend,
            IconObjectStack,
            IconValue,
            IconPointer,
            IconType,
            IconError,
            IconBookmark,
            ProjectFolder,

            Project,
            ArrowMoveDown,
            Workspace,
            MemoryArrowSingle,
            MemoryArrowDoubleTop,
            MemoryArrowDoubleBottom,
            MemoryArrowTripleTop,
            MemoryArrowTripleMiddle,
            MemoryArrowTripleBottom,
            MemoryArrowRainbow,
            Namespace,
            ResizeGrabber,
            AsmArrow,
            AsmArrowRev,
            AsmArrowShadow,
            MenuNonFocusSelect,
            StepFilter,
            WaitSegment,
            FindCaseSensitive,
            FindWholeWord,

            RedDotUnbound,
            MoreInfo,
            Interface,
            Property,
            Field,
            Method,
            Variable,
            Constant,

            Type_ValueType,
            Type_Class,

            LinePointer_Prev,
            LinePointer_Opt,
            RedDotEx,
            RedDotExUnbound,
            RedDotDisabled,
            RedDotExDisabled,
            RedDotRunToCursor,

            GotoButton,
            YesJmp,
            NoJmp,
            WhiteBox,
            UpDownArrows,
            EventInfo,
            WaitBar,
            HiliteOutline,
            HiliteOutlineThin,

            IconPayloadEnum,
            StepFilteredDefault,

            ThreadBreakpointMatch,
            ThreadBreakpointNoMatch,
            ThreadBreakpointUnbound,
            Search,
            CheckIndeterminate,
            CodeError,
            CodeWarning,
            ComboBoxFrameless,
            PanelHeader,

            ExtMethod,

            //COUNT
        };
    }
}
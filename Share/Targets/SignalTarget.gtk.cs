using Avalonia.Media.Imaging;
using Microsoft.Maui.Essentials;
using Microsoft.Maui.Storage;
using SkiaSharp;
using SkiaSharp.QrCode;
using System.Diagnostics;
using System.Reflection;
using static Microsoft.Maui.ApplicationModel.DataTransfer.ShareImplementation;

namespace Microsoft.Maui.ApplicationModel.DataTransfer
{
    internal class SignalTarget : ShareTarget
    {
        private static string? Title { get; set; }
        private static string[]? Attachments { get; set; }

        public override Task<bool> Invoke
        {
            get
            {
                var account = Preferences.Get("signal-cli", string.Empty);
                if (string.IsNullOrWhiteSpace(account))
                {
                    return Register();
                }

                return Execute();
            }
        }

        public SignalTarget(string title, params string[] attachments)
            : base("Signal", "signal-desktop")
        {
            Title = title;
            Attachments = attachments;
        }

        private static async Task<bool> Execute()
        {
            string account = Preferences.Get("signal-desktop", string.Empty);

            while (string.IsNullOrWhiteSpace(account))
            {
                account = await new TokenWindow("Account").ShowDialog<string>(SharePicker.GetMainWindow());
                if (!string.IsNullOrWhiteSpace(account))
                    Preferences.Set("signal-desktop", account);

                account = Preferences.Get("signal-desktop", string.Empty);
            }

            foreach (var attachment in (Attachments ?? []).Select(a => a.Replace("file://", string.Empty)))
            {
                await ProcessHelper.ExecuteProcess("signal-cli", $"-u +{account} send -m \"{Title.Replace("\"", "\\\"")}\" +33652760516 --attachment {attachment}");
            }
            return true;
        }
        private static async Task<bool> Register()
        {
            if (!await ProcessHelper.IsProgramInstalled("signal-cli"))
            {
                await new TokenWindow("https://github.com/AsamK/signal-cli").ShowDialog(SharePicker.GetMainWindow());
            }

            if (await ProcessHelper.IsProgramInstalled("signal-cli"))
            {
                var appName = Assembly.GetEntryAssembly()?.GetName().Name;
                if (!string.IsNullOrWhiteSpace(appName))
                {
                    var output = await Link(appName);
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var bitmap = GenerateBitmap(output);
                        var window = new TokenWindow("Test", bitmap, (text) => text.Contains("successful"));
                        if (await window.ShowDialog<bool>(SharePicker.GetMainWindow()))
                            Preferences.Set("signal-cli", appName);
                    }
                }
            }

            return false;
        }

        private static async Task<string?> Link(string name)
        {
            using var c = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "signal-cli",
                    Arguments = $@"link -n {name}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                }
            };

            c.Start();
            string? output = string.Empty;
            while (!c.StandardOutput.EndOfStream)
            {
                output = await c.StandardOutput.ReadLineAsync();
                if (true == output?.StartsWith("sgnl"))
                    break;
            }
            return output;
        }

        private static Bitmap GenerateBitmap(string output)
        {
            using var generator = new QRCodeGenerator();
            // Generate QrCode
            var qr = generator.CreateQrCode(output, ECCLevel.L);

            // Render to canvas
            var info = new SKImageInfo(512, 512);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Render(qr, info.Width, info.Height);

            // Output to Stream -> File
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var path = Path.Combine(FileSystem.CacheDirectory, "qrcode.png");
            using (var stream = File.OpenWrite(path))
            {
                data.SaveTo(stream);
            }
            return new Bitmap(File.OpenRead(path));
        }
    }
}

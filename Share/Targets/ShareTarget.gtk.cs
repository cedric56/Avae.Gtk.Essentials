using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Microsoft.Maui.Authentication;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;

namespace Microsoft.Maui.ApplicationModel.DataTransfer
{
    internal class WhatsAppTarget : ShareTarget
    {
        public class WhatsAppBot
        {
            private ChromeDriver _driver;

            public async Task Start(string phoneNumber, string message, string attachmentPath = null)
            {
                await Task.Run(async () =>
                {
                    var options = new ChromeOptions();
                    options.AddArgument("--no-sandbox");
                    options.AddArgument("--disable-dev-shm-usage");
                    options.AddArgument("--user-data-dir=selenium_profile");
                    options.AddArgument("--window-size=1200,800");

                    _driver = new ChromeDriver(options);

                    string url = $"https://web.whatsapp.com/send?phone={phoneNumber}&text={Uri.EscapeDataString(message)}";
                    _driver.Navigate().GoToUrl(url);

                    await Task.Delay(10000); // Wait for page to load

                    // Give user time to scan QR if needed
                    WaitForQrCodeToBeScanned();

                    // Click send button
                    //try
                    //{
                    //    var sendButton = _driver.FindElement(By.XPath("//span[@data-icon='send']"));
                    //    sendButton.Click();
                    //}
                    //catch { Console.WriteLine("Send button not found. Maybe already sent."); }

                    if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    {
                        SendAttachment(attachmentPath);
                    }

                    // Wait a bit before closing or reuse
                    Task.Delay(5000).Wait();
                });
            }

            private void WaitForQrCodeToBeScanned()
            {
                bool qrVisible = true;
                int timeoutSeconds = 60;
                int elapsed = 0;

                while (qrVisible && elapsed < timeoutSeconds)
                {
                    try
                    {
                        // Cherche le QR code par son ID ou son XPath
                        var qrElement = _driver.FindElement(By.XPath("//*[contains(text(), 'Log in with phone number')]"));
                        Console.WriteLine("QR Code visible... en attente du scan.");
                        Task.Delay(1000).Wait();
                        elapsed++;
                    }
                    catch (NoSuchElementException)
                    {
                        try
                        {
                            // Cherche le QR code par son ID ou son XPath
                            var qrElement = _driver.FindElement(By.XPath("//*[contains(text(), 'Enter phone number')]"));
                            Console.WriteLine("QR Code visible... en attente du scan.");
                            Task.Delay(1000).Wait();
                            elapsed++;
                        }
                        catch (NoSuchElementException)
                        {
                            // QR Code disparu = utilisateur connecté
                            qrVisible = false;
                            Console.WriteLine("QR Code non visible, l'utilisateur est connecté.");
                        }
                    }
                }

                if (qrVisible)
                    Console.WriteLine("⏳ Timeout : QR code toujours visible après 60s.");
            }

            private void SendAttachment(string filePath)
            {
                try
                {
                    var attachBtn = _driver.FindElement(By.XPath("//div[@title='Attach']"));
                    attachBtn.Click();

                    Task.Delay(1000).Wait(); // Wait for input field

                    var input = _driver.FindElement(By.XPath("//input[@accept='*']"));
                    input.SendKeys(filePath);

                    Task.Delay(3000).Wait(); // Wait for upload preview

                    var sendFileBtn = _driver.FindElement(By.XPath("//span[@data-icon='send']"));
                    sendFileBtn.Click();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attachment error: {ex.Message}");
                }
            }

            public void Close()
            {
                _driver?.Quit();
            }
        }


        private static string? Title { get; set; }
        private static string[]? Attachments { get; set; }
        public override Task<bool> Invoke
        {
            get
            {
                return Execute();
            }
        }
        public WhatsAppTarget(string title, params string[] attachments)
            : base("WhatsApp", "whatsapp")
        {
            Title = title;
            Attachments = attachments;
        }

        private static async Task<bool> Execute()
        {
            var bot = new WhatsAppBot();
            string phone = "33652760516"; // Use international format without +

            //foreach (var attachment in Attachments)
            //{
            //    if (!File.Exists(attachment))
            //    {
            //        Console.WriteLine($"Attachment file not found: {attachment}");
            //        return false;
            //    }
            //}
            await bot.Start(phone, Title, Attachments.FirstOrDefault());


            bot.Close();
            return true;
        }
    }

    public abstract class ShareTarget
    {
        public string Name { get; set; }

        public IImage Icon { get; set; }

        public abstract Task<bool> Invoke { get; }

        public ShareTarget(string name, string software)
        {
            Name = name;
            Icon = GetIcon(software);
        }

        private static IImage GetIcon(string software)
        {
            if (Gtk.Application.Default is null)
                Gtk.Application.Init();

            var icon = Gtk.IconTheme.Default.LookupIcon(software, 48, Gtk.IconLookupFlags.UseBuiltin);
            if (!string.IsNullOrWhiteSpace(icon?.Filename))
            {
                if (Path.GetExtension(icon?.Filename) == ".svg")
                {
                    using var stream = File.OpenRead(icon!.Filename);
                    return new SvgImage()
                    {

                        Source = SvgSource.Load(stream)
                    };
                }
                return new Bitmap(icon!.Filename);
            }
            
            return null!;
        }
    }

}

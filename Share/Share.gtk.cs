﻿using Avae.Gtk.Essentials;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System.Text.RegularExpressions;

namespace Microsoft.Maui.ApplicationModel.DataTransfer
{
    public interface ISharePicker
    {
        Task Open(ShareTextRequest request, IEnumerable<ShareTarget> targets);
        Task Open(ShareFileRequest request, IEnumerable<ShareTarget> targets);

        Task Open(ShareMultipleFilesRequest request, IEnumerable<ShareTarget> targets);
    }

    class SharePicker : ISharePicker
    {
        public Task Open(ShareTextRequest request, IEnumerable<ShareTarget> targets)
        {
            var mainWindow = new ShareImplementation.ShareWindow(targets);
            mainWindow.ShowDialog(GetMainWindow());
            return Task.CompletedTask;
        }

        public Task Open(ShareFileRequest request, IEnumerable<ShareTarget> targets)
        {
            var mainWindow = new ShareImplementation.ShareWindow(targets, request.File.FullPath);
            mainWindow.ShowDialog(GetMainWindow());
            return Task.CompletedTask;
        }

        public Task Open(ShareMultipleFilesRequest request, IEnumerable<ShareTarget> targets)
        {
            var mainWindow = new ShareImplementation.ShareWindow(targets, request.Files.Select(f => f.FullPath).ToArray());
            mainWindow.ShowDialog(GetMainWindow());
            return Task.CompletedTask;
        }

        public static Window GetMainWindow()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            throw new InvalidOperationException("Main window not found.");
        }
    }

    partial class ShareImplementation : IShare
    {
        private ISharePicker SharePicker { get; }

        public ShareImplementation(ISharePicker? sharePicker = null)
        {
            SharePicker = sharePicker ?? new SharePicker();
        }        

        Task PlatformRequestAsync(ShareTextRequest request) => SharePicker.Open(request, GetShareTargets(request.Title, request.Subject, request.Text));

        Task PlatformRequestAsync(ShareFileRequest request) => SharePicker.Open(request, GetShareTargets(request.Title, string.Empty, string.Empty, request.File.FullPath));


        Task PlatformRequestAsync(ShareMultipleFilesRequest request) => SharePicker.Open(request, GetShareTargets(request.Title, string.Empty, string.Empty, request.Files.Select(f => f.FullPath).ToArray()));

        static string[] DesktopDirs = new[] {
        "/usr/share/applications",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local/share/applications")
    };

        static string[] MessengerKeywords = new[] {
        "messag", "chat", "irc", "telegram", "discord", "slack", "signal", "whatsapp", "skype", "matrix"
    };

        static string ExtractField(string content, string field)
        {
            var match = Regex.Match(content, $"{field}=([^\n\r]+)");
            return match.Success ? match.Groups[1].Value.Trim() : "[Unknown]";
        }

        private List<ShareTarget> GetShareTargets(string title, string subject, string body, params string[] attachements)
        {
            var apps = new List<(string name, string exec, string icon)>();

            foreach (var dir in DesktopDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "*.desktop"))
                {                    
                    string content = File.ReadAllText(file);
                    string name = string.Empty;
                    string exec = string.Empty;
                    string icon = string.Empty;

                    foreach (var keyword in MessengerKeywords)
                    {
                        if (Regex.IsMatch(content, keyword, RegexOptions.IgnoreCase))
                        {
                            name = ExtractField(content, "Name");
                            exec = ExtractField(content, "Exec");
                            icon = ExtractField(content, "Icon");

                            apps.Add((name, exec, icon));
                            break;
                        }
                    }

                    var attachment = attachements.FirstOrDefault();
                    if(string.IsNullOrWhiteSpace(attachment))
                        continue;
                    var lines = File.ReadAllLines(file);
                    bool foundMime = lines.Any(l => l.StartsWith("MimeType=") && l.Contains(MimeHelper.GetMimeType(Path.GetExtension(attachment))));
                    if (!foundMime) continue;

                    name = lines.FirstOrDefault(l => l.StartsWith("Name="))?.Split('=')[1] ?? Path.GetFileName(file);
                    exec = lines.FirstOrDefault(l => l.StartsWith("Exec="))?.Split('=')[1] ?? "Unknown";
                    icon = lines.FirstOrDefault(l => l.StartsWith("Icon="))?.Split('=')[1] ?? "Unknown";
                    apps.Add((name, exec, icon));
                }
            }

            apps.RemoveAll(a => a.exec.Contains("gnome-control-center") ||
                            a.name.Contains("Evolution") ||
                            a.name.Contains("Signal"));

            

            var targets = new List<ShareTarget>
            {
                new EvolutionTarget(subject, body, title, attachements),
                new SignalTarget(title, attachements),
                new WhatsAppTarget(title, attachements)
            };

            foreach (var app in apps)
            {
                targets.Add(new ExecTarget(app.name, app.icon, app.exec, attachements.FirstOrDefault()));
            }

            return targets;
        }

        public class TokenWindow : AnimatableWindow
        {
            private IImage? Image { get; set; }

            private Func<string, object>? Parser { get; set; }

            public TokenWindow(string title, IImage image, Func<string, object> parser)
                : base()
            {
                Parser = parser;
                Image = image;
                Title = title;
                InitializeContent();
            }


            public TokenWindow(string title)
                : base()
            {
                Title = title;
                InitializeContent();
            }

            private void InitializeContent()
            {
                var mainGrid = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(20),
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var text = new TextBlock
                {
                    Text = Title, // Placeholder file name
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Black)
                };

                var tb = new TextBox
                {
                    //Text = text, // Placeholder file name
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Foreground = new SolidColorBrush(Colors.Black)
                };

                var image = new Image()
                {
                    Source = Image
                };

                var grid = new Grid()
                {
                    RowDefinitions = new RowDefinitions("*,Auto, Auto"),
                    Children = { text, tb, image }
                };
                Grid.SetRow(image, 0);
                Grid.SetRow(text, 1);
                Grid.SetRow(tb, 2);                
                Grid.SetRow(grid, 0);
                mainGrid.Children.Add(grid);

                // Share targets grid
                var shareGrid = new Grid
                {
                    Margin = new Thickness(0, 20, 0, 20),
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("Auto,Auto,Auto")
                };
                Grid.SetRow(shareGrid, 1);
                mainGrid.Children.Add(shareGrid);



                var acceptButton = new Button
                {
                    Content = "Accept",
                    Height = 40,
                    FontSize = 16,
                    Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(230, 230, 230)),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(5),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                };
                acceptButton.Click += (_, __) => Close(Parser is null ? tb.Text : Parser.Invoke(tb.Text));

                // Close button
                var closeButton = new Button
                {
                    Content = "Close",
                    Height = 40,
                    FontSize = 16,
                    Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(230, 230, 230)),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(5),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                };
                closeButton.Click += (_, __) => Close();

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { acceptButton, closeButton },
                    Spacing = 10,
                    Margin = new Thickness(10)
                };


                Grid.SetRow(buttonPanel, 2);
                mainGrid.Children.Add(buttonPanel);

                Content = mainGrid;

                // Styles for share target buttons
                Styles.Add(new Style(selector: x => x.OfType<Button>().Class("share-target"))
                {
                    Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.Transparent))
                }
                });
                Styles.Add(new Style(selector: x => x.OfType<Button>().Class("share-target").Class(":pointerover"))
                {
                    Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(Avalonia.Media.Color.FromArgb(50, 200, 200, 200)))
                }
                });

                // Close on click outside
                PointerPressed += (s, e) =>
                {
                    var point = e.GetPosition(this);
                    if (!mainGrid.Bounds.Contains(point))
                        Close();
                };
            }
        }

        public class ShareWindow : AnimatableWindow
        {
            private IEnumerable<ShareTarget> _shareTargets;

            public ShareWindow(IEnumerable<ShareTarget> shareTargets, params string[] attachments)
                : base()
            {
                _shareTargets = shareTargets;
                InitializeContent(attachments);
            }


            private void InitializeContent(params string[] attachments)
            {
                var mainGrid = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(20),
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                if (attachments.Length > 0)
                {
                    string text = attachments.First();
                    if (attachments.Length > 1)
                    {
                        text += $" + {attachments.Length - 1} attachments";
                    }

                    // Thumbnail preview of shared file
                    var thumbnailBorder = new Border
                    {
                        Background = new SolidColorBrush(Colors.White),
                        BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(200, 200, 200)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 10),
                        Child = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                    {
                        new TextBlock
                        {
                            Text = "📄", // Placeholder file icon
                            FontSize = 60,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = text, // Placeholder file name
                            FontSize = 14,
                            Margin = new Thickness(0, 5, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = new SolidColorBrush(Colors.Black)
                        }
                    }
                        }
                    };
                    Grid.SetRow(thumbnailBorder, 0);
                    mainGrid.Children.Add(thumbnailBorder);
                }

                // Share targets grid
                var shareGrid = new Grid
                {
                    Margin = new Thickness(0, 20, 0, 20),
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("Auto,Auto,Auto")
                };
                Grid.SetRow(shareGrid, 1);
                mainGrid.Children.Add(shareGrid);

                // Sample share targets (simulating apps/contacts)


                for (int i = 0; i < _shareTargets.Count(); i++)
                {
                    var target = _shareTargets.ElementAt(i);
                    var button = new Button
                    {
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(10),
                        Margin = new Thickness(5),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                        {
                            new Image
                            {
                                Height = 32,
                                Width = 32,
                                Source = target.Icon,
                                HorizontalAlignment = HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = target.Name,
                                FontSize = 14,
                                Margin = new Thickness(0, 5, 0, 0),
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        }
                        },
                        Tag = target
                    };

                    button.Classes.Add("share-target");
                    button.Click += async (_, __) => 
                    {
                        if (await target.Invoke)
                            Close();
                    };

                    Grid.SetRow(button, i / 2);
                    Grid.SetColumn(button, i % 2);
                    shareGrid.Children.Add(button);
                }

                // Close button
                var closeButton = new Button
                {
                    Content = "Close",
                    Height = 40,
                    FontSize = 16,
                    Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(230, 230, 230)),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(5),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                };
                closeButton.Click += (_, __) => Close();
                Grid.SetRow(closeButton, 2);
                mainGrid.Children.Add(closeButton);

                Content = mainGrid;

                // Styles for share target buttons
                Styles.Add(new Style(selector: x => x.OfType<Button>().Class("share-target"))
                {
                    Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.Transparent))
                }
                });
                Styles.Add(new Style(selector: x => x.OfType<Button>().Class("share-target").Class(":pointerover"))
                {
                    Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(Avalonia.Media.Color.FromArgb(50, 200, 200, 200)))
                }
                });

                // Close on click outside
                PointerPressed += (s, e) =>
                {
                    var point = e.GetPosition(this);
                    if (!mainGrid.Bounds.Contains(point))
                        Close();
                };
            }
        }
    }
}

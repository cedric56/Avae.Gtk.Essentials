using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

namespace Avae.Gtk.Essentials
{
    public static class ServiceCollectionExtensions
    {

        public static bool IsStandardWindows { get; private set; } = false;
        public static ICapturePicker? CapturePicker { get; private set; }
        public static IAccountPicker? AccountPicker { get; private set; }

        public static ISharePicker? SharePicker { get; private set; }

        public static void ConfigureEssentials(this IServiceCollection services, bool isStandardWindows = false, IAccountPicker? accountPicker = null, ICapturePicker? capturePicker = null, ISharePicker? sharePicker = null)
        {
            GLib.ExceptionManager.UnhandledException += (e) =>
            {
                // Handle unhandled exceptions globally
                Console.WriteLine($"Unhandled exception: {e.ExceptionObject}");
            };

            AccountPicker = accountPicker;
            CapturePicker = capturePicker;
            IsStandardWindows = isStandardWindows;
            SharePicker = sharePicker;
            services.AddSingleton(_ => Compass.Default);
            services.AddSingleton(_ => MediaPicker.Default);
            services.AddSingleton(_ => Accelerometer.Default);
            services.AddSingleton(_ => Magnetometer.Default);
            services.AddSingleton(_ => Barometer.Default);
            services.AddSingleton(_ => OrientationSensor.Default);
            services.AddSingleton(_ => Vibration.Default);
            services.AddSingleton(_ => WebAuthenticator.Default);
            services.AddSingleton(_ => Geolocation.Default);
            services.AddSingleton(_ => Geocoding.Default);
            services.AddSingleton(_ => Battery.Default);
            services.AddSingleton(_ => Sms.Default);
            services.AddSingleton(_ => Preferences.Default);
            services.AddSingleton(_ => TextToSpeech.Default);
            services.AddSingleton(_ => Email.Default);
            services.AddSingleton(_ => Contacts.Default);
            services.AddSingleton(_ => Connectivity.Current);
            services.AddSingleton(_ => AppInfo.Current);
            services.AddSingleton(_ => Browser.Default);
            services.AddSingleton(_ => Map.Default);
            services.AddSingleton(_ => DeviceInfo.Current);
            services.AddSingleton(_ => Share.Default);
            services.AddSingleton(_ => Flashlight.Default);
            services.AddSingleton(_ => PhoneDialer.Default);
            services.AddSingleton(_ => Launcher.Default);
            services.AddSingleton(_ => HapticFeedback.Default);
            services.AddSingleton(_ => Gyroscope.Default);
            services.AddSingleton(_ => VersionTracking.Default);
            services.AddSingleton(_ => SemanticScreenReader.Default);
            services.AddSingleton(_ => SecureStorage.Default);
            services.AddSingleton(_ => FileSystem.Current);
            services.AddSingleton(_ => DeviceDisplay.Current);
            services.AddSingleton(_ => FilePicker.Default);
            services.AddSingleton(_ => AppActions.Current);
            services.AddSingleton(_ => Screenshot.Default);
        }
    }

    internal class AnimatableWindow : Avalonia.Controls.Window
    {
        public AnimatableWindow()
        {
            Width = 400;
            Height = 600;            
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (!ServiceCollectionExtensions.IsStandardWindows)
            {
                CanResize = false;
                Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(180, 255, 255, 255)); // Semi-transparent white
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
                ExtendClientAreaToDecorationsHint = true;
                SystemDecorations = SystemDecorations.None;

                // Apply rounded corners
                ClipToBounds = true;
                this.GetObservable(BoundsProperty).Subscribe(bounds =>
                {
                    if (bounds.Width > 0 && bounds.Height > 0)
                    {
                        double radius = 10;
                        this.Clip = CreateRoundedClip(bounds.Width, bounds.Height, radius);
                    }
                });

                // Open animation
                Dispatcher.UIThread.Post(async () =>
                {
                    var animation = new Animation
                    {
                        Duration = TimeSpan.FromSeconds(0.3),
                        Easing = new CubicEaseOut(),
                        Children =
                        {
                        new KeyFrame
                        {
                            Setters = { new Setter(OpacityProperty, 0.0), new Setter(ScaleTransform.ScaleYProperty, 0.8) },
                            Cue = new Cue( 0.0)
                        },
                        new KeyFrame
                        {
                            Setters = { new Setter(OpacityProperty, 1.0), new Setter(ScaleTransform.ScaleYProperty, 1.0) },
                            Cue = new Cue( 1.0)
                        }
                        }
                    };
                    var transform = new ScaleTransform();
                    RenderTransform = transform;
                    await animation.RunAsync(this);
                });
            }
        }
        private Geometry CreateRoundedClip(double width, double height, double radius)
        {
            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                // Start at top-left corner, offset by radius
                ctx.BeginFigure(new Point(radius, 0), true); // is filled

                // Top edge
                ctx.LineTo(new Point(width - radius, 0));
                ctx.ArcTo(new Point(width, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);

                // Right edge
                ctx.LineTo(new Point(width, height - radius));
                ctx.ArcTo(new Point(width - radius, height), new Size(radius, radius), 0, false, SweepDirection.Clockwise);

                // Bottom edge
                ctx.LineTo(new Point(radius, height));
                ctx.ArcTo(new Point(0, height - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);

                // Left edge
                ctx.LineTo(new Point(0, radius));
                ctx.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise);

                ctx.EndFigure(true);
            }

            return geometry;
        }
    }
}

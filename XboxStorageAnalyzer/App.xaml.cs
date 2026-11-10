using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
namespace XboxStorageAnalyzer
{
    sealed partial class App : Application
    {
        public App() { InitializeComponent(); }
        protected override void OnLaunched(Windows.ApplicationModel.Activation.LaunchActivatedEventArgs e)
        {
            if (Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox")
            {
                // gamepad focus navigation instead of mouse mode; opt out of overscan margins
                this.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
                Windows.UI.ViewManagement.ApplicationViewScaling.TrySetDisableLayoutScaling(true);
            }
            if (!(Window.Current.Content is Frame f)) { f = new Frame(); Window.Current.Content = f; }
            if (f.Content == null) f.Navigate(typeof(MainPage), e.Arguments);
            Window.Current.Activate();
        }
    }
}

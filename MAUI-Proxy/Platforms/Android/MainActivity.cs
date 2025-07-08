using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MAUI_Proxy
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true)]
    public class MainActivity : MauiAppCompatActivity
    {
        public Action<Result> VpnPermissionResultHandler { get; set; }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            CreateNotificationChannel();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(
                    "vpn_channel",
                    "VPN Service",
                    NotificationImportance.Default)
                {
                    Description = "VPN Proxy notifications"
                };

                var notificationManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
                notificationManager?.CreateNotificationChannel(channel);
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == 0) // VPN permission request
            {
                VpnPermissionResultHandler?.Invoke(resultCode);
                VpnPermissionResultHandler = null;
            }

            base.OnActivityResult(requestCode, resultCode, data);
        }
    }
}

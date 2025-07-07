using MAUI_Proxy.Platforms.Android.Services;

namespace MAUI_Proxy.Views;

public partial class VpnPermissionPage : ContentPage
{
    public VpnPermissionPage()
    {
        InitializeComponent();

        Content = new StackLayout
        {
            Children =
            {
                new Label { Text = "��� ������ ���������� ��������� ���������� VPN" },
                new Button
                {
                    Text = "������������ ����������",
                    Command = new Command(async () => await RequestVpnPermission())
                }
            }
        };
    }

    private async Task RequestVpnPermission()
    {
#if ANDROID
        var context = Platform.CurrentActivity;
        var intent = new Android.Content.Intent(context, typeof(ProxyVpnService));
        context.StartActivity(intent);
        await Shell.Current.GoToAsync("//mainpage");
#endif
    }
}

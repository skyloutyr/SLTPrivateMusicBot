namespace SLTPrivateMusicBot
{
    using SLTPrivateMusicBot.Player;
    using System.Windows;

    public partial class App : Application
    {
        private void App_Exit(object sender, ExitEventArgs e)
        {
            YoutubeDL.Cleanup();
        }
    }
}

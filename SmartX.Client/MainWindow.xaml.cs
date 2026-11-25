using System.Windows;
using System.Windows.Controls;
using SmartX.Client.Views;

namespace SmartX.Client
{
    public partial class MainWindow : Window
    {
        private const string AppTitle = "Smart-X IoT Management System";

        // Each view is created once and reused, so its controls keep their state.
        private TelemetryDashboard? _telemetry;
        private CommandStream? _commandStream;
        private NetworkTopology? _topology;

        public MainWindow()
        {
            InitializeComponent();
            ShowMainMenu();
        }

        private void ShowView(UserControl view, Button navButton, string pillarName)
        {
            MainMenuGrid.Visibility = Visibility.Collapsed;
            ContentHost.Visibility = Visibility.Visible;

            if (!ReferenceEquals(ContentHost.Content, view))
                ContentHost.Content = view;

            SetActivePillar(navButton, pillarName);
        }

        public void ShowMainMenu()
        {
            ContentHost.Content = null;   // Unloaded fires, so the view's timer stops
            ContentHost.Visibility = Visibility.Collapsed;
            MainMenuGrid.Visibility = Visibility.Visible;

            SetActivePillar(NavHomeButton, "Main Menu");
        }

        // Shows where you are in three places: the highlighted nav button,
        // the breadcrumb next to it, and the window title bar.
        private void SetActivePillar(Button activeButton, string pillarName)
        {
            foreach (var button in new[] { NavHomeButton, NavTelemetryButton, NavCommandButton, NavTopologyButton })
                button.Tag = ReferenceEquals(button, activeButton) ? "Active" : null;

            bool atHome = ReferenceEquals(activeButton, NavHomeButton);

            BreadcrumbText.Text = atHome ? "Main Menu" : $"Main Menu  ›  {pillarName}";
            Title = atHome ? AppTitle : $"{AppTitle} — {pillarName}";
        }

        private void NavHome_Click(object sender, RoutedEventArgs e) => ShowMainMenu();

        private void TelemetryButton_Click(object sender, RoutedEventArgs e)
            => ShowView(_telemetry ??= new TelemetryDashboard(), NavTelemetryButton, "Sensor Data Ingestion & Telemetry");

        private void CommandStreamButton_Click(object sender, RoutedEventArgs e)
            => ShowView(_commandStream ??= new CommandStream(), NavCommandButton, "Real-Time Command Stream & History");

        private void NetworkTopologyButton_Click(object sender, RoutedEventArgs e)
            => ShowView(_topology ??= new NetworkTopology(), NavTopologyButton, "Network Topology & Mesh Routing");
    }
}

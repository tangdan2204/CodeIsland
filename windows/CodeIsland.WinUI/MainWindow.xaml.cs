using CodeIsland.WinUI.Services;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace CodeIsland.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly NamedPipeHookServer hookServer;

    public SessionStore Store { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(620, 560));
        FooterText.Text = "Waiting for hook events...";

        Store.EventReceived += (_, hookEvent) =>
        {
            FooterText.Text = $"Last event: {hookEvent.EventName} / {DateTimeOffset.Now:T}";
        };
        Store.BlockingRequestChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateBlockingPanel);

        hookServer = new NamedPipeHookServer(Store, DispatcherQueue);
        hookServer.Start();
    }

    private async void InstallHooks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FooterText.Text = await HookInstaller.InstallAsync();
        }
        catch (Exception ex)
        {
            FooterText.Text = "Install failed: " + ex.Message;
        }
    }

    private void Approve_Click(object sender, RoutedEventArgs e) => Store.ApprovePermission();

    private void Always_Click(object sender, RoutedEventArgs e) => Store.ApprovePermission(always: true);

    private void Deny_Click(object sender, RoutedEventArgs e) => Store.DenyPermission();

    private void Answer_Click(object sender, RoutedEventArgs e) => Store.AnswerQuestion(QuestionAnswerBox.Text);

    private void UpdateBlockingPanel()
    {
        if (Store.PendingPermission is { } permission)
        {
            BlockingPanel.Visibility = Visibility.Visible;
            BlockingTitle.Text = $"Permission: {permission.ToolName}";
            BlockingDetail.Text = permission.Detail ?? permission.SessionId;
            QuestionAnswerBox.Visibility = Visibility.Collapsed;
            AnswerButton.Visibility = Visibility.Collapsed;
            ApproveButton.Visibility = Visibility.Visible;
            AlwaysButton.Visibility = Visibility.Visible;
            DenyButton.Visibility = Visibility.Visible;
            return;
        }

        if (Store.PendingQuestion is { } question)
        {
            BlockingPanel.Visibility = Visibility.Visible;
            BlockingTitle.Text = "Question";
            BlockingDetail.Text = question.Options.Length == 0 ? question.Question : question.Question + Environment.NewLine + string.Join(Environment.NewLine, question.Options);
            QuestionAnswerBox.Visibility = Visibility.Visible;
            AnswerButton.Visibility = Visibility.Visible;
            ApproveButton.Visibility = Visibility.Collapsed;
            AlwaysButton.Visibility = Visibility.Collapsed;
            DenyButton.Visibility = Visibility.Collapsed;
            return;
        }

        BlockingPanel.Visibility = Visibility.Collapsed;
    }
}
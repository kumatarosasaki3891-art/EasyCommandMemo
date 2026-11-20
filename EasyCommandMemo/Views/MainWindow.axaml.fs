namespace EasyCommandMemo.Views

open System
open System.Collections.Generic
open System.Reflection
open System.Text.RegularExpressions
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Markup.Xaml
open Avalonia.Platform.Storage
open Avalonia.Styling
open Avalonia.Threading
open EasyCommandMemo
open EasyCommandMemo.Models
open EasyCommandMemo.ViewModels

type MainWindow() as this =
    inherit Window()

    let toastTimer = DispatcherTimer()
    let mutable quickLauncher: QuickLauncherWindow option = None
    let viewModel () = this.DataContext :?> MainViewModel

    let showCopyToast () =
        let toast = this.FindControl<Border>("CopyToast")
        toast.IsVisible <- true
        toastTimer.Stop()
        toastTimer.Start()

    let copyText (owner: Window) (entry: CommandEntry) text (onCopied: unit -> unit) =
        let transfer = new DataTransfer()
        transfer.Add(DataTransferItem.CreateText(text))
        owner.Clipboard.SetDataAsync(transfer) |> ignore
        (viewModel()).RecordCopy(entry.Id)
        onCopied ()

    let toggleTheme () =
        let application = Application.Current
        application.RequestedThemeVariant <-
            if application.ActualThemeVariant = ThemeVariant.Dark then ThemeVariant.Light else ThemeVariant.Dark

    let copyCommand (owner: Window) (entry: CommandEntry) (onCopied: unit -> unit) =
        let variablePattern = Regex(@"\{([^{}]+)\}")
        let variableNames =
            variablePattern.Matches(entry.CommandText)
            |> Seq.cast<Match>
            |> Seq.map (fun matched -> matched.Groups[1].Value)
            |> Seq.distinct
            |> Seq.toList

        if List.isEmpty variableNames then
            copyText owner entry entry.CommandText onCopied
        else
            let values = Dictionary<string, TextBox>()
            let dialog = Window()
            dialog.Title <- Localization.get "Text_VariableTitle"
            dialog.Width <- 620.0
            dialog.Height <- min 680.0 (300.0 + float variableNames.Length * 68.0)
            dialog.MinWidth <- 520.0
            dialog.MinHeight <- 320.0
            dialog.WindowStartupLocation <- WindowStartupLocation.CenterOwner

            let content = StackPanel(Margin = Thickness(24.0), Spacing = 14.0)
            content.Children.Add(TextBlock(Text = Localization.get "Text_VariablePrompt", FontSize = 20.0, FontWeight = Avalonia.Media.FontWeight.Bold)) |> ignore

            for name in variableNames do
                let input = TextBox(PlaceholderText = name, Height = 38.0)
                values.Add(name, input)
                let field = StackPanel(Spacing = 5.0)
                field.Children.Add(TextBlock(Text = name, FontSize = 12.0, FontWeight = Avalonia.Media.FontWeight.SemiBold)) |> ignore
                field.Children.Add(input) |> ignore
                content.Children.Add(field) |> ignore

            let preview = TextBox(IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 72.0)
            let render () =
                variablePattern.Replace(entry.CommandText, MatchEvaluator(fun matched ->
                    match values.TryGetValue(matched.Groups[1].Value) with
                    | true, input when not (String.IsNullOrWhiteSpace(input.Text)) -> input.Text
                    | _ -> matched.Value))
            preview.Text <- render ()
            for input in values.Values do
                input.TextChanged.Add(fun _ -> preview.Text <- render ())

            let previewField = StackPanel(Spacing = 5.0)
            previewField.Children.Add(TextBlock(Text = Localization.get "Text_CopyPreview", FontSize = 12.0, FontWeight = Avalonia.Media.FontWeight.SemiBold)) |> ignore
            previewField.Children.Add(preview) |> ignore
            content.Children.Add(previewField) |> ignore

            let actions = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10.0)
            let cancelButton = Button(Content = Localization.get "Text_Cancel", MinWidth = 100.0, Height = 38.0)
            let copyButton = Button(Content = Localization.get "Text_Copy", MinWidth = 100.0, Height = 38.0, Background = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#15745A")), Foreground = Avalonia.Media.Brushes.White)
            cancelButton.Click.Add(fun _ -> dialog.Close())
            copyButton.Click.Add(fun _ -> copyText owner entry preview.Text onCopied; dialog.Close())
            actions.Children.Add(cancelButton) |> ignore
            actions.Children.Add(copyButton) |> ignore
            content.Children.Add(actions) |> ignore
            dialog.Content <- ScrollViewer(Content = content, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto)
            dialog.ShowDialog(owner) |> ignore

    let openQuickSearch () =
        let launcher =
            match quickLauncher with
            | Some window -> window
            | None ->
                let window = QuickLauncherWindow()
                window.Configure(
                    viewModel(),
                    (fun entry owner copied -> copyCommand owner entry copied),
                    (fun entry ->
                        (viewModel()).SelectedCommand <- box entry
                        if this.WindowState = WindowState.Minimized then
                            this.WindowState <- WindowState.Normal
                        this.Show()
                        this.Activate()))
                window.Closed.Add(fun _ -> quickLauncher <- None)
                quickLauncher <- Some window
                window
        launcher.ShowSearch()

    let configureLanguageSelector () =
        let selector = this.FindControl<ComboBox>("LanguageSelector")
        selector.ItemsSource <- Localization.languages |> List.map snd
        selector.SelectedIndex <-
            Localization.languages
            |> List.tryFindIndex (fun (code, _) -> code = Localization.current ())
            |> Option.defaultValue 0
        selector.SelectionChanged.Add(fun _ ->
            match Localization.languages |> List.tryItem selector.SelectedIndex with
            | Some(language, _) when language <> Localization.current() ->
                Localization.set language
                viewModel().RefreshLocalizedCategories()
                viewModel().StatusMessage <- Localization.get "Text_InitialStatus"
            | _ -> ())

    let showFaq () =
        let dialog = Window()
        dialog.Title <- Localization.get "Text_FaqTitle"
        dialog.Width <- 680.0
        dialog.Height <- 640.0
        dialog.MinWidth <- 520.0
        dialog.MinHeight <- 420.0
        dialog.WindowStartupLocation <- WindowStartupLocation.CenterOwner

        let content = StackPanel(Margin = Thickness(24.0), Spacing = 16.0)
        content.Children.Add(TextBlock(Text = Localization.get "Text_FaqTitle", FontSize = 22.0, FontWeight = Avalonia.Media.FontWeight.Bold)) |> ignore
        for index in 1..5 do
            let question = Localization.get ($"Text_FaqQ{index}")
            let answer = Localization.get ($"Text_FaqA{index}")
            let item = StackPanel(Spacing = 6.0)
            item.Children.Add(TextBlock(Text = question, FontSize = 14.0, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap)) |> ignore
            item.Children.Add(TextBlock(Text = answer, FontSize = 13.0, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#718079")))) |> ignore
            content.Children.Add(item) |> ignore

        let closeButton = Button(Content = Localization.get "Text_Close", MinWidth = 100.0, Height = 38.0, HorizontalAlignment = HorizontalAlignment.Right)
        closeButton.Click.Add(fun _ -> dialog.Close())
        content.Children.Add(closeButton) |> ignore
        dialog.Content <- ScrollViewer(Content = content, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto)
        dialog.ShowDialog(this) |> ignore

    let openBrowser (url: string) =
        try
            let psi = System.Diagnostics.ProcessStartInfo(url)
            psi.UseShellExecute <- true
            System.Diagnostics.Process.Start(psi) |> ignore
        with _ -> ()

    let appVersion () =
        let assembly = Assembly.GetExecutingAssembly()
        let informational =
            assembly.GetCustomAttributes(typeof<AssemblyInformationalVersionAttribute>, false)
            |> Array.tryPick (function
                | :? AssemblyInformationalVersionAttribute as attr when not (String.IsNullOrWhiteSpace(attr.InformationalVersion)) ->
                    let raw = attr.InformationalVersion.Trim()
                    let plusIndex = raw.IndexOf('+')
                    Some(if plusIndex >= 0 then raw.Substring(0, plusIndex) else raw)
                | _ -> None)
        match informational with
        | Some version -> version
        | None ->
            match Option.ofObj (assembly.GetName().Version) with
            | Some version -> $"%d{version.Major}.%d{version.Minor}.%d{version.Build}"
            | None ->
                let localized = Localization.get "Text_InformationVersion"
                if localized = "Text_InformationVersion" then "1.1.0" else localized

    let showInformation () =
        let dialog = Window()
        try
            use iconStream = Avalonia.Platform.AssetLoader.Open(System.Uri("avares://EasyCommandMemo/Assets/app.ico"))
            dialog.Icon <- WindowIcon(iconStream)
        with _ -> ()
        dialog.Title <- Localization.get "Text_Information"
        dialog.Width <- 480.0
        dialog.Height <- 430.0
        dialog.MinWidth <- 440.0
        dialog.MinHeight <- 400.0
        dialog.CanResize <- false
        dialog.WindowStartupLocation <- WindowStartupLocation.CenterOwner

        let isDark = Application.Current.ActualThemeVariant = Avalonia.Styling.ThemeVariant.Dark
        let primaryBg = if isDark then "#55B995" else "#15745A"
        let primaryFg = if isDark then "#10251C" else "#FFFFFF"
        let surfaceBg = if isDark then "#202825" else "#FFFFFF"
        let borderCol = if isDark then "#37443E" else "#DCE5E1"
        let textCol = if isDark then "#E7EFEB" else "#172D28"
        let subTextCol = if isDark then "#9AA9A1" else "#40564D"

        let rootPanel = StackPanel(Margin = Thickness(28.0), Spacing = 14.0)

        // 1. ヘッダー (アイコン + タイトル)
        let headerPanel = StackPanel(Orientation = Orientation.Horizontal, Spacing = 14.0, VerticalAlignment = VerticalAlignment.Center)

        let iconBorder = Border(Width = 44.0, Height = 44.0, CornerRadius = CornerRadius(10.0), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center)
        let iconImage = Image(Width = 44.0, Height = 44.0, Stretch = Media.Stretch.UniformToFill)
        try
            use imgStream = Avalonia.Platform.AssetLoader.Open(System.Uri("avares://EasyCommandMemo/Assets/icon.png"))
            iconImage.Source <- new Media.Imaging.Bitmap(imgStream)
        with _ -> ()
        iconBorder.Child <- iconImage
        headerPanel.Children.Add(iconBorder) |> ignore

        let titleText = TextBlock(
            Text = Localization.get "Text_AppName",
            FontSize = 21.0,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        )
        headerPanel.Children.Add(titleText) |> ignore
        rootPanel.Children.Add(headerPanel) |> ignore

        // 区切り線
        let divider = Border(Height = 1.0, Margin = Thickness(0.0, 2.0, 0.0, 4.0), Background = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(borderCol)))
        rootPanel.Children.Add(divider) |> ignore

        // 2. バージョン
        let versionPanel = StackPanel(Orientation = Orientation.Horizontal, Spacing = 6.0)
        let verLabel = TextBlock(Text = (Localization.get "Text_VersionLabel") + ":", FontSize = 14.0, FontWeight = Avalonia.Media.FontWeight.Bold)
        let verValue = TextBlock(Text = appVersion (), FontSize = 14.0, FontWeight = Avalonia.Media.FontWeight.SemiBold)
        versionPanel.Children.Add(verLabel) |> ignore
        versionPanel.Children.Add(verValue) |> ignore
        rootPanel.Children.Add(versionPanel) |> ignore

        // 3. 説明文
        let descText = TextBlock(
            Text = Localization.get "Text_AboutDescription",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13.0,
            Foreground = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(subTextCol)),
            Margin = Thickness(0.0, 2.0, 0.0, 8.0)
        )
        rootPanel.Children.Add(descText) |> ignore

        // 4. ホームページボタン
        let homepageButton = Button(
            Content = Localization.get "Text_Homepage",
            Height = 42.0,
            CornerRadius = CornerRadius(8.0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = Thickness(1.0),
            Background = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(surfaceBg)),
            BorderBrush = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(borderCol)),
            Foreground = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(textCol))
        )
        let homepageUrl = "https://sites.google.com/view/kuma-applications/%E7%B0%A1%E5%8D%98%E3%82%B3%E3%83%9E%E3%83%B3%E3%83%89%E5%B8%B3"
        homepageButton.Click.Add(fun _ -> openBrowser homepageUrl)
        rootPanel.Children.Add(homepageButton) |> ignore

        // 5. Instagram ボタン
        let instagramButton = Button(
            Content = Localization.get "Text_Instagram",
            Height = 42.0,
            CornerRadius = CornerRadius(8.0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = Thickness(1.0),
            Background = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(surfaceBg)),
            BorderBrush = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(borderCol)),
            Foreground = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(textCol))
        )
        let instagramUrl = "https://www.instagram.com/zefa_25_zefa/"
        instagramButton.Click.Add(fun _ -> openBrowser instagramUrl)
        rootPanel.Children.Add(instagramButton) |> ignore

        // 6. 閉じるボタン (Primary Accent)
        let closeButton = Button(
            Content = Localization.get "Text_Close",
            Height = 44.0,
            CornerRadius = CornerRadius(8.0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Background = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(primaryBg)),
            Foreground = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(primaryFg)),
            Margin = Thickness(0.0, 6.0, 0.0, 0.0)
        )
        closeButton.Click.Add(fun _ -> dialog.Close())
        rootPanel.Children.Add(closeButton) |> ignore

        dialog.Content <- rootPanel
        dialog.ShowDialog(this) |> ignore

    let openEditor (initial: CommandEntry option) =
        let dialog = EditCommandWindow(initial)
        dialog.Closed.Add(fun _ ->
            match dialog.SavedEntry with
            | Some entry ->
                try viewModel().SaveEntry(entry)
                with error -> viewModel().StatusMessage <- error.Message
            | None -> ())
        dialog.ShowDialog(this) |> ignore

    let showDeleteConfirmation () =
        let dialog = Window()
        dialog.Title <- Localization.get "Text_DeleteConfirmTitle"
        dialog.Width <- 390.0
        dialog.Height <- 190.0
        dialog.CanResize <- false
        dialog.WindowStartupLocation <- WindowStartupLocation.CenterOwner
        let content = StackPanel(Margin = Thickness(24.0), Spacing = 18.0)
        content.Children.Add(TextBlock(Text = Localization.get "Text_DeleteConfirm", TextWrapping = Avalonia.Media.TextWrapping.Wrap)) |> ignore
        let actions = StackPanel(Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10.0)
        let cancelButton = Button(Content = Localization.get "Text_Cancel", MinWidth = 90.0)
        let deleteButton = Button(Content = Localization.get "Text_Delete", MinWidth = 90.0, Foreground = Avalonia.Media.Brushes.White, Background = Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A63D36")))
        actions.Children.Add(cancelButton) |> ignore
        actions.Children.Add(deleteButton) |> ignore
        content.Children.Add(actions) |> ignore
        cancelButton.Click.Add(fun _ -> dialog.Close())
        deleteButton.Click.Add(fun _ ->
            viewModel().DeleteSelected()
            dialog.Close())
        dialog.Content <- content
        dialog.ShowDialog(this) |> ignore

    let importJson () = task {
        let options = FilePickerOpenOptions()
        options.Title <- Localization.get "Text_ImportJson"
        options.AllowMultiple <- false
        let! files = this.StorageProvider.OpenFilePickerAsync(options)
        match files |> Seq.tryHead with
        | Some file ->
            try
                use! stream = file.OpenReadAsync()
                viewModel().ImportJson(stream)
                with error -> (viewModel()).StatusMessage <- Localization.get "Text_ImportFailed" + error.Message
        | None -> ()
    }

    let exportJson () = task {
        let options = FilePickerSaveOptions()
        options.Title <- Localization.get "Text_ExportJson"
        options.SuggestedFileName <- "easy-command-memo.json"
        let! file = this.StorageProvider.SaveFilePickerAsync(options)
        if not (obj.ReferenceEquals(file, null)) then
            try
                use! stream = file.OpenWriteAsync()
                viewModel().ExportJson(stream)
                viewModel().StatusMessage <- Localization.get "Text_StatusExported"
                with error -> (viewModel()).StatusMessage <- Localization.get "Text_ExportFailed" + error.Message
    }

    do
        this.InitializeComponent()
        toastTimer.Interval <- TimeSpan.FromMilliseconds(1800.0)
        toastTimer.Tick.Add(fun _ ->
            toastTimer.Stop()
            this.FindControl<Border>("CopyToast").IsVisible <- false)
        this.AddHandler(Button.ClickEvent, EventHandler<RoutedEventArgs>(fun _ args ->
            match args.Source with
            | :? Button as button when button.Name = "CardCopyButton" ->
                match button.DataContext with
                | :? CommandEntry as entry ->
                    copyCommand this entry showCopyToast
                    args.Handled <- true
                | _ -> ()
            | _ -> ()), RoutingStrategies.Bubble)
        this.FindControl<Button>("ThemeButton").Click.Add(fun _ -> toggleTheme ())
        configureLanguageSelector ()
        this.FindControl<Button>("FaqButton").Click.Add(fun _ -> showFaq ())
        this.FindControl<Button>("InformationButton").Click.Add(fun _ -> showInformation ())
        this.FindControl<Button>("QuickLaunchButton").Click.Add(fun _ -> openQuickSearch ())
        this.FindControl<Button>("AddButton").Click.Add(fun _ -> openEditor (None))
        this.FindControl<Button>("EditButton").Click.Add(fun _ ->
            if (viewModel()).HasSelection then openEditor (Some(unbox<CommandEntry> ((viewModel()).SelectedCommand))))
        this.FindControl<Button>("DeleteButton").Click.Add(fun _ ->
            if (viewModel()).HasSelection then showDeleteConfirmation ())
        this.FindControl<Button>("FavoriteButton").Click.Add(fun _ -> (viewModel()).ToggleFavorite())
        this.FindControl<Button>("CopyButton").Click.Add(fun _ ->
            if (viewModel()).HasSelection then
                let entry = unbox<CommandEntry> ((viewModel()).SelectedCommand)
                copyCommand this entry showCopyToast)
        this.FindControl<Button>("ImportButton").Click.Add(fun _ -> importJson() |> ignore)
        this.FindControl<Button>("ExportButton").Click.Add(fun _ -> exportJson() |> ignore)

    member this.ShowQuickSearch() = openQuickSearch ()

    member this.BringToFront() =
        if this.WindowState = WindowState.Minimized then
            this.WindowState <- WindowState.Normal
        this.Show()
        this.Activate()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

namespace EasyCommandMemo.Views

open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Platform
open Avalonia.Controls.Primitives
open EasyCommandMemo
open EasyCommandMemo.Models

type EditCommandWindow(initial: CommandEntry option) as this =
    inherit Window()

    let titleBox = TextBox()
    let categoryBox = TextBox()
    let tagsBox = TextBox()
    let commandBox = TextBox()
    let descriptionBox = TextBox()
    let mutable savedEntry: CommandEntry option = None

    let initialValue selector = initial |> Option.map selector |> Option.defaultValue ""

    let addField (panel: StackPanel) label (input: Control) =
        let field = StackPanel(Spacing = 6.0)
        let labelText = TextBlock(Text = label, FontSize = 13.0, FontWeight = FontWeight.SemiBold)
        field.Children.Add(labelText) |> ignore
        field.Children.Add(input) |> ignore
        panel.Children.Add(field) |> ignore

    do
        try
            use iconStream = AssetLoader.Open(System.Uri("avares://EasyCommandMemo/Assets/app.ico"))
            this.Icon <- WindowIcon(iconStream)
        with _ -> ()
        this.Title <- Localization.get (if initial.IsSome then "Text_EditCommand" else "Text_NewCommand")
        this.Width <- 620.0
        this.Height <- 690.0
        this.MinWidth <- 560.0
        this.MinHeight <- 620.0
        this.WindowStartupLocation <- WindowStartupLocation.CenterOwner
        this.CanResize <- true

        titleBox.Text <- initialValue _.Title
        titleBox.PlaceholderText <- Localization.get "Text_TitlePlaceholder"
        titleBox.Height <- 38.0
        categoryBox.Text <- initialValue _.Category
        categoryBox.PlaceholderText <- Localization.get "Text_CategoryPlaceholder"
        categoryBox.Height <- 38.0
        tagsBox.Text <- initialValue _.Tags
        tagsBox.PlaceholderText <- Localization.get "Text_TagsPlaceholder"
        tagsBox.Height <- 38.0
        commandBox.Text <- initialValue _.CommandText
        commandBox.PlaceholderText <- Localization.get "Text_CommandPlaceholder"
        commandBox.Height <- 150.0
        commandBox.AcceptsReturn <- true
        commandBox.TextWrapping <- TextWrapping.Wrap
        commandBox.FontFamily <- FontFamily("Cascadia Code, Consolas, monospace")
        descriptionBox.Text <- initialValue _.Description
        descriptionBox.PlaceholderText <- Localization.get "Text_DescriptionPlaceholder"
        descriptionBox.Height <- 90.0
        descriptionBox.AcceptsReturn <- true
        descriptionBox.TextWrapping <- TextWrapping.Wrap

        let content = StackPanel(Spacing = 16.0)
        let heading = TextBlock(Text = this.Title, FontSize = 23.0, FontWeight = FontWeight.Bold)
        content.Children.Add(heading) |> ignore
        addField content (Localization.get "Text_TitleRequired") titleBox
        addField content (Localization.get "Text_CategoryRequired") categoryBox
        addField content (Localization.get "Text_Tags") tagsBox
        addField content (Localization.get "Text_CommandLabel" + " *") commandBox
        addField content (Localization.get "Text_Description") descriptionBox

        let cancelButton = Button(Content = Localization.get "Text_Cancel", MinWidth = 100.0, Height = 38.0)
        let isDark = Application.Current.ActualThemeVariant = Avalonia.Styling.ThemeVariant.Dark
        let saveColor = if isDark then "#55B995" else "#15745A"
        let saveForeground = if isDark then "#10251C" else "#FFFFFF"
        let saveButton = Button(Content = Localization.get "Text_Save", MinWidth = 100.0, Height = 38.0, Background = SolidColorBrush(Color.Parse(saveColor)), Foreground = SolidColorBrush(Color.Parse(saveForeground)))
        cancelButton.HorizontalContentAlignment <- HorizontalAlignment.Center
        cancelButton.VerticalContentAlignment <- VerticalAlignment.Center
        saveButton.HorizontalContentAlignment <- HorizontalAlignment.Center
        saveButton.VerticalContentAlignment <- VerticalAlignment.Center
        let buttonRow = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10.0)
        buttonRow.Children.Add(cancelButton) |> ignore
        buttonRow.Children.Add(saveButton) |> ignore
        content.Children.Add(buttonRow) |> ignore

        cancelButton.Click.Add(fun _ -> this.Close())
        saveButton.Click.Add(fun _ ->
            if not (System.String.IsNullOrWhiteSpace(titleBox.Text))
               && not (System.String.IsNullOrWhiteSpace(categoryBox.Text))
               && not (System.String.IsNullOrWhiteSpace(commandBox.Text)) then
                savedEntry <-
                    Some
                        { Id = initial |> Option.map _.Id |> Option.defaultValue 0L
                          Title = titleBox.Text
                          Category = categoryBox.Text
                          Tags = if isNull tagsBox.Text then "" else tagsBox.Text
                          CommandText = commandBox.Text
                          Description = if isNull descriptionBox.Text then "" else descriptionBox.Text
                          IsFavorite = initial |> Option.exists _.IsFavorite
                          CopyCount = initial |> Option.map _.CopyCount |> Option.defaultValue 0L
                          UpdatedAt = System.DateTimeOffset.UtcNow.ToString("O") }
                this.Close())

        let scroll = ScrollViewer(Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto)
        scroll.Margin <- Thickness(24.0)
        this.Content <- scroll

    member _.SavedEntry = savedEntry

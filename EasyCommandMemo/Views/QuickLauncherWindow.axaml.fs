namespace EasyCommandMemo.Views

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Markup.Xaml
open Avalonia.Threading
open EasyCommandMemo.Models
open EasyCommandMemo.ViewModels

type QuickLauncherWindow() as this =
    inherit Window()

    let toastTimer = DispatcherTimer()
    let mutable viewModel: MainViewModel = Unchecked.defaultof<MainViewModel>
    let mutable copyEntry: CommandEntry -> Window -> (unit -> unit) -> unit = fun _ _ _ -> ()
    let mutable openEntry: CommandEntry -> unit = fun _ -> ()
    let searchBox () = this.FindControl<TextBox>("QuickSearchBox")
    let favoritesCheckBox () = this.FindControl<CheckBox>("FavoritesOnlyCheckBox")
    let results () = this.FindControl<ListBox>("QuickResults")

    let selectedEntry () =
        match viewModel.QuickSelectedCommand with
        | :? CommandEntry as entry -> Some entry
        | _ -> None

    let copySelected () =
        selectedEntry ()
        |> Option.iter (fun entry -> copyEntry entry this (fun () -> this.ShowCopiedToast()))

    do
        this.InitializeComponent()
        favoritesCheckBox().Click.Add(fun _ ->
            Dispatcher.UIThread.Post(fun () ->
                searchBox().Focus() |> ignore))
        toastTimer.Interval <- TimeSpan.FromSeconds(1.0)
        toastTimer.Tick.Add(fun _ ->
            toastTimer.Stop()
            this.Hide())
        this.AddHandler(InputElement.KeyDownEvent, (fun _ args ->
            let list = results ()
            match args.Key with
            | Key.Escape ->
                args.Handled <- true
                this.Hide()
            | Key.Down ->
                if list.ItemCount > 0 then
                    list.SelectedIndex <- min (list.ItemCount - 1) (max 0 (list.SelectedIndex + 1))
                    list.ScrollIntoView(list.SelectedItem)
                args.Handled <- true
            | Key.Up ->
                if list.ItemCount > 0 then
                    list.SelectedIndex <- max 0 (list.SelectedIndex - 1)
                    list.ScrollIntoView(list.SelectedItem)
                args.Handled <- true
            | Key.Enter ->
                match selectedEntry () with
                | Some entry when args.KeyModifiers.HasFlag(KeyModifiers.Control) ->
                    args.Handled <- true
                    this.Hide()
                    openEntry entry
                | Some _ ->
                    args.Handled <- true
                    copySelected ()
                | None -> ()
            | _ -> ()), RoutingStrategies.Tunnel)
        this.Opened.Add(fun _ ->
            viewModel.QuickSearchQuery <- ""
            this.FindControl<Border>("CopyToast").IsVisible <- false
            this.FindControl<TextBlock>("EmptyMessage").IsVisible <- not viewModel.HasQuickResults
            Dispatcher.UIThread.Post(fun () ->
                searchBox().Focus() |> ignore
                searchBox().SelectAll()))

    member this.ShowSearch() =
        viewModel.QuickSearchQuery <- ""
        this.FindControl<Border>("CopyToast").IsVisible <- false
        this.FindControl<TextBlock>("EmptyMessage").IsVisible <- not viewModel.HasQuickResults
        if not this.IsVisible then this.Show()
        this.WindowStartupLocation <- WindowStartupLocation.CenterScreen
        this.Activate()
        Dispatcher.UIThread.Post(fun () ->
            searchBox().Focus() |> ignore
            searchBox().SelectAll())

    member this.Configure(model: MainViewModel, copy: CommandEntry -> Window -> (unit -> unit) -> unit, openCommand: CommandEntry -> unit) =
        viewModel <- model
        copyEntry <- copy
        openEntry <- openCommand
        this.DataContext <- model
        model.QuickCommands.CollectionChanged.Add(fun _ ->
            this.FindControl<TextBlock>("EmptyMessage").IsVisible <- not model.HasQuickResults)
        this.FindControl<TextBlock>("EmptyMessage").IsVisible <- not model.HasQuickResults

    member this.ShowCopiedToast() =
        this.FindControl<Border>("CopyToast").IsVisible <- true
        toastTimer.Stop()
        toastTimer.Start()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
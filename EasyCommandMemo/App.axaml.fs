namespace EasyCommandMemo

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open Avalonia.Threading
open EasyCommandMemo.ViewModels
open EasyCommandMemo.Views

type App() =
    inherit Application()

    override this.Initialize() =
        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            Localization.load ()
            let mainWindow = MainWindow(DataContext = MainViewModel())
            desktop.MainWindow <- mainWindow

            let singleInstanceServer =
                new SingleInstanceServer(
                    AppStartupOptions.InstanceLock,
                    fun command ->
                        Dispatcher.UIThread.Post(Action(fun () ->
                            match command with
                            | "quick" -> mainWindow.ShowQuickSearch()
                            | _ -> mainWindow.BringToFront())))
            AppStartupOptions.InstanceLock <- None

            if AppStartupOptions.StartInQuickSearch then
                let mutable opened = false
                mainWindow.Opened.Add(fun _ ->
                    if not opened then
                        opened <- true
                        Dispatcher.UIThread.Post(Action(fun () -> mainWindow.ShowQuickSearch())))

            let hotKeyService =
                if GlobalHotKeyService.IsWaylandSession() || GlobalHotKeyService.IsFlatpakSandbox() then
                    None
                else
                    try
                        let service =
                            GlobalHotKeyService(
                                mainWindow.ShowQuickSearch,
                                fun error ->
                                    let viewModel = mainWindow.DataContext :?> MainViewModel
                                    viewModel.StatusMessage <- $"グローバルホットキーを開始できませんでした: {error}")
                        service.Start()
                        Some service
                    with error ->
                        let viewModel = mainWindow.DataContext :?> MainViewModel
                        viewModel.StatusMessage <- $"グローバルホットキーを開始できませんでした: {error.Message}"
                        None

            desktop.Exit.Add(fun _ ->
                singleInstanceServer.Dispose()
                hotKeyService |> Option.iter _.Dispose())
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

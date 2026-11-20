namespace EasyCommandMemo

open System
open Avalonia

module Program =

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()
            .WithInterFont()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace(areas = Array.empty)

    [<EntryPoint; STAThread>]
    let main argv =
        let isQuick = SingleInstanceService.isQuickArg argv
        let ipcCommand = if isQuick then "quick" else "show"

        match SingleInstanceService.tryAcquireLock () with
        | InstanceLockResult.Secondary ->
            if SingleInstanceService.tryNotifyExistingInstance ipcCommand then
                0
            else
                AppStartupOptions.StartInQuickSearch <- isQuick
                buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
        | InstanceLockResult.Primary lockStream ->
            AppStartupOptions.StartInQuickSearch <- isQuick
            AppStartupOptions.InstanceLock <- Some lockStream
            buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
        | InstanceLockResult.Fallback ->
            AppStartupOptions.StartInQuickSearch <- isQuick
            buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)

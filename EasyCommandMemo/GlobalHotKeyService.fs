namespace EasyCommandMemo

open System
open System.IO
open Avalonia.Threading
open SharpHook
open SharpHook.Data

type GlobalHotKeyService(onTriggered: unit -> unit, onError: string -> unit) =
    let hook = new EventLoopGlobalHook()
    let mutable controlPressed = false
    let mutable shiftPressed = false
    let mutable spaceTriggered = false

    let updateModifier key pressed =
        match key with
        | KeyCode.VcLeftControl | KeyCode.VcRightControl -> controlPressed <- pressed
        | KeyCode.VcLeftShift | KeyCode.VcRightShift -> shiftPressed <- pressed
        | _ -> ()

    do
        hook.KeyPressed.Add(fun (args: KeyboardHookEventArgs) ->
            let key = args.Data.KeyCode
            updateModifier key true
            if key = KeyCode.VcSpace && controlPressed && shiftPressed && not spaceTriggered then
                spaceTriggered <- true
                Dispatcher.UIThread.Post(Action(onTriggered)))
        hook.KeyReleased.Add(fun (args: KeyboardHookEventArgs) ->
            let key = args.Data.KeyCode
            updateModifier key false
            if key = KeyCode.VcSpace then spaceTriggered <- false)

    static member IsWaylandSession() =
        OperatingSystem.IsLinux()
        && (String.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
            || not (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))))

    static member IsFlatpakSandbox() =
        OperatingSystem.IsLinux()
        && (File.Exists("/.flatpak-info")
            || not (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID"))))

    member _.Start() =
        let task = hook.RunAsync()
        task.ContinueWith(fun completed ->
            if completed.IsFaulted then
                let message = completed.Exception.GetBaseException().Message
                Dispatcher.UIThread.Post(Action(fun () -> onError message)))
        |> ignore

    member _.Dispose() = hook.Dispose()
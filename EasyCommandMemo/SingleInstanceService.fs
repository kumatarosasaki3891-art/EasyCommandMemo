namespace EasyCommandMemo

open System
open System.IO
open System.IO.Pipes
open System.Security.Cryptography
open System.Text
open System.Threading
open System.Threading.Tasks

module AppStartupOptions =
    let mutable StartInQuickSearch = false
    let mutable InstanceLock: FileStream option = None

type InstanceLockResult =
    | Primary of FileStream
    | Secondary
    | Fallback

module SingleInstanceService =
    let isQuickArg (argv: string array) =
        if isNull argv then
            false
        else
            argv
            |> Array.exists (fun arg ->
                String.Equals(arg, "--quick", StringComparison.OrdinalIgnoreCase)
                || String.Equals(arg, "-q", StringComparison.OrdinalIgnoreCase))

    let isRunningInFlatpak () =
        OperatingSystem.IsLinux()
        && (File.Exists("/.flatpak-info")
            || not (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID"))))

    let getPipeNameFor (isWindows: bool) (xdgRuntime: string) (flatpakId: string) (tempPath: string) (userKey: string) =
        if isWindows then
            $"EasyCommandMemo_IPC_{userKey}"
        elif not (String.IsNullOrWhiteSpace(xdgRuntime)) && not (String.IsNullOrWhiteSpace(flatpakId)) then
            let flatpakRuntimeDir = Path.Combine(xdgRuntime, "app", flatpakId)
            if Directory.Exists(flatpakRuntimeDir) then
                Path.Combine(flatpakRuntimeDir, "ipc.sock")
            else
                let baseDir = if Directory.Exists(xdgRuntime) then xdgRuntime else tempPath
                Path.Combine(baseDir, $"easy-command-memo-{userKey}.sock")
        else
            let baseDir =
                if not (String.IsNullOrWhiteSpace(xdgRuntime)) && Directory.Exists(xdgRuntime) then
                    xdgRuntime
                else
                    tempPath
            Path.Combine(baseDir, $"easy-command-memo-{userKey}.sock")

    let getPipeName () =
        let identity = $"{Environment.UserName}@{Environment.UserDomainName}"
        let hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity))
        let userKey = Convert.ToHexString(hashBytes).Substring(0, 16).ToLowerInvariant()
        let xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
        let flatpakId = Environment.GetEnvironmentVariable("FLATPAK_ID")
        if isRunningInFlatpak ()
           && not (String.IsNullOrWhiteSpace(xdgRuntime))
           && not (String.IsNullOrWhiteSpace(flatpakId))
           && Directory.Exists(xdgRuntime) then
            try
                Directory.CreateDirectory(Path.Combine(xdgRuntime, "app", flatpakId)) |> ignore
            with _ -> ()
        getPipeNameFor (OperatingSystem.IsWindows()) xdgRuntime flatpakId (Path.GetTempPath()) userKey

    let tryAcquireLockAt (lockPath: string) =
        try
            let directory = Path.GetDirectoryName(lockPath)
            if not (String.IsNullOrWhiteSpace(directory)) then
                Directory.CreateDirectory(directory) |> ignore
            let stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
            Primary stream
        with
        | :? IOException -> Secondary
        | _ -> Fallback

    let tryAcquireLock () =
        let lockPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasyCommandMemo",
                "instance.lock"
            )
        tryAcquireLockAt lockPath

    let tryNotifyExistingInstanceWithPipe (pipe: string) (command: string) =
        let rec attempt remaining =
            if remaining <= 0 then
                false
            else
                let sent =
                    try
                        use client = new NamedPipeClientStream(".", pipe, PipeDirection.Out)
                        client.Connect(500)
                        use writer = new StreamWriter(client, UTF8Encoding(false))
                        writer.WriteLine(command)
                        writer.Flush()
                        true
                    with _ ->
                        false
                if sent then
                    true
                else
                    if remaining > 1 then Thread.Sleep(100)
                    attempt (remaining - 1)
        attempt 6

    let tryNotifyExistingInstance (command: string) =
        tryNotifyExistingInstanceWithPipe (getPipeName ()) command

type SingleInstanceServer(pipe: string, lockStream: FileStream option, onMessage: string -> unit) =
    let cts = new CancellationTokenSource()
    let mutable disposed = false

    do
        if not (OperatingSystem.IsWindows()) && File.Exists(pipe) then
            try File.Delete(pipe) with _ -> ()

        Task.Run(
            Func<Task>(fun () ->
                task {
                    try
                        while not cts.IsCancellationRequested do
                            try
                                use server =
                                    new NamedPipeServerStream(
                                        pipe,
                                        PipeDirection.In,
                                        4,
                                        PipeTransmissionMode.Byte,
                                        PipeOptions.Asynchronous
                                    )
                                do! server.WaitForConnectionAsync(cts.Token)
                                use reader = new StreamReader(server, Encoding.UTF8)
                                let! line = reader.ReadLineAsync(cts.Token)
                                if not (String.IsNullOrWhiteSpace(line)) then
                                    onMessage (line.Trim())
                            with
                            | :? OperationCanceledException -> ()
                            | _ ->
                                if not cts.IsCancellationRequested then
                                    do! Task.Delay(50, cts.Token)
                    with
                    | :? OperationCanceledException -> ()
                }),
            cts.Token
        )
        |> ignore

    new(lockStream: FileStream option, onMessage: string -> unit) =
        new SingleInstanceServer(SingleInstanceService.getPipeName (), lockStream, onMessage)

    member _.Dispose() =
        if not disposed then
            disposed <- true
            try cts.Cancel() with _ -> ()
            cts.Dispose()
            lockStream |> Option.iter (fun stream -> try stream.Dispose() with _ -> ())
            if not (OperatingSystem.IsWindows()) && File.Exists(pipe) then
                try File.Delete(pipe) with _ -> ()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

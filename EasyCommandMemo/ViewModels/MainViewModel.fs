namespace EasyCommandMemo.ViewModels

open System
open System.Collections.ObjectModel
open System.IO
open System.Text.Json
open EasyCommandMemo
open EasyCommandMemo.Data
open EasyCommandMemo.Models

[<RequireQualifiedAccess>]
type CategorySelection =
    | All
    | Favorites
    | Custom of string

type MainViewModel(repository: CommandRepository) as this =
    inherit ViewModelBase()

    let mutable commands = repository.GetAll()
    let filteredCommands = ObservableCollection<CommandEntry>()
    let quickCommands = ObservableCollection<CommandEntry>()
    let categories = ObservableCollection<string>()
    let allCategories () = Localization.get "Text_AllCategories"
    let favoritesCategory () = Localization.get "Text_Favorites"
    let mutable searchQuery = ""
    let mutable quickSearchQuery = ""
    let mutable quickFavoritesOnly = false
    let mutable currentSelection = CategorySelection.All
    let mutable isUpdatingCategories = false
    let mutable selectedCommand: CommandEntry option = None
    let mutable quickSelectedCommand: CommandEntry option = None
    let mutable commandCount = 0
    let mutable filteredCount = 0
    let mutable statusMessage = Localization.get "Text_InitialStatus"

    let updateCategories () =
        isUpdatingCategories <- true
        try
            let categoryNames =
                commands
                |> List.map _.Category
                |> List.distinct
                |> List.sortWith (fun left right -> StringComparer.OrdinalIgnoreCase.Compare(left, right))
            let desired = [ allCategories (); favoritesCategory () ] @ categoryNames

            // リストの全削除を避け、インデックス単位でインプレース同期
            for i = 0 to desired.Length - 1 do
                let item = desired[i]
                if i < categories.Count then
                    if categories[i] <> item then categories[i] <- item
                else
                    categories.Add(item)
            while categories.Count > desired.Length do
                categories.RemoveAt(categories.Count - 1)

            // 選択中のカスタムカテゴリが存在しなくなった場合は All に戻す
            match currentSelection with
            | CategorySelection.Custom name when not (List.contains name categoryNames) ->
                currentSelection <- CategorySelection.All
            | _ -> ()
        finally
            isUpdatingCategories <- false

    let refresh () =
        let query = searchQuery.Trim()
        let selectedId = selectedCommand |> Option.map _.Id
        let matches (value: string) = value.Contains(query, StringComparison.OrdinalIgnoreCase)
        let visible =
            commands
            |> List.filter (fun entry ->
                let categoryMatches =
                    match currentSelection with
                    | CategorySelection.All -> true
                    | CategorySelection.Favorites -> entry.IsFavorite
                    | CategorySelection.Custom name -> entry.Category = name
                let queryMatches =
                    String.IsNullOrWhiteSpace(query)
                    || ([ entry.Title; entry.Category; entry.Tags; entry.CommandText; entry.Description ] |> List.exists matches)
                categoryMatches && queryMatches)

        // 差分同期を行い、Clear() による UI 要素の一瞬の全消失・チラつきを防止
        let visibleIds = visible |> List.map _.Id |> Set.ofList
        for i = filteredCommands.Count - 1 downto 0 do
            if not (Set.contains filteredCommands[i].Id visibleIds) then
                filteredCommands.RemoveAt(i)

        for i = 0 to visible.Length - 1 do
            let item = visible[i]
            if i < filteredCommands.Count then
                if filteredCommands[i].Id <> item.Id then
                    let mutable found = -1
                    for j = i + 1 to filteredCommands.Count - 1 do
                        if found = -1 && filteredCommands[j].Id = item.Id then found <- j
                    if found <> -1 then
                        filteredCommands.Move(found, i)
                        filteredCommands[i] <- item
                    else
                        filteredCommands.Insert(i, item)
                else
                    if filteredCommands[i] <> item then filteredCommands[i] <- item
            else
                filteredCommands.Add(item)

        let nextSelection =
            selectedId
            |> Option.bind (fun id -> visible |> List.tryFind (fun entry -> entry.Id = id))
        match nextSelection |> Option.orElseWith (fun () -> List.tryHead visible) with
        | Some entry -> this.SelectedCommand <- box entry
        | None -> this.SelectedCommand <- null

        if commandCount <> commands.Length then
            commandCount <- commands.Length
            this.NotifyCommandCounts()
        if filteredCount <> visible.Length then
            filteredCount <- visible.Length
            this.NotifyCommandCounts()

    let refreshQuick () =
        let query = quickSearchQuery.Trim()
        let matches (value: string) = value.Contains(query, StringComparison.OrdinalIgnoreCase)
        let score (entry: CommandEntry) =
            if String.IsNullOrWhiteSpace(query) then 0
            elif String.Equals(entry.Title, query, StringComparison.OrdinalIgnoreCase) then 0
            elif entry.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) then 1
            elif entry.CommandText.StartsWith(query, StringComparison.OrdinalIgnoreCase) then 2
            elif entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase) then 3
            elif entry.CommandText.Contains(query, StringComparison.OrdinalIgnoreCase) then 4
            elif entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.Tags.Contains(query, StringComparison.OrdinalIgnoreCase) then 5
            else 6
        let visible =
            commands
            |> List.filter (fun entry ->
                let favoriteMatches = not quickFavoritesOnly || entry.IsFavorite
                let queryMatches =
                    String.IsNullOrWhiteSpace(query)
                    || ([ entry.Title; entry.Category; entry.Tags; entry.CommandText; entry.Description ] |> List.exists matches)
                favoriteMatches && queryMatches)
            |> List.sortWith (fun left right ->
                let relevance = compare (score left) (score right)
                if relevance <> 0 then relevance
                else
                    let usage = compare right.CopyCount left.CopyCount
                    if usage <> 0 then usage else StringComparer.Ordinal.Compare(right.UpdatedAt, left.UpdatedAt))

        let selectedId = quickSelectedCommand |> Option.map _.Id
        quickCommands.Clear()
        visible |> List.iter quickCommands.Add
        quickSelectedCommand <-
            selectedId
            |> Option.bind (fun id -> visible |> List.tryFind (fun entry -> entry.Id = id))
            |> Option.orElseWith (fun () -> List.tryHead visible)
        this.NotifyQuickResults()

    do
        updateCategories ()
        refresh ()
        refreshQuick ()

    new() = MainViewModel(CommandRepository())

    member _.FilteredCommands = filteredCommands
    member _.QuickCommands = quickCommands
    member _.Categories = categories

    member private this.NotifyCommandCounts() =
        this.OnPropertyChanged("CommandCount")
        this.OnPropertyChanged("FilteredCount")

    member private this.NotifyQuickResults() =
        this.OnPropertyChanged("QuickSelectedCommand")
        this.OnPropertyChanged("HasQuickResults")

    member _.SearchQuery
        with get () = searchQuery
        and set value =
            if this.SetProperty(&searchQuery, value) then refresh ()

    member this.QuickSearchQuery
        with get () = quickSearchQuery
        and set value =
            if this.SetProperty(&quickSearchQuery, value) then refreshQuick ()

    member this.QuickFavoritesOnly
        with get () = quickFavoritesOnly
        and set value =
            if this.SetProperty(&quickFavoritesOnly, value) then refreshQuick ()

    member this.SelectedCategory
        with get () =
            match currentSelection with
            | CategorySelection.All -> allCategories ()
            | CategorySelection.Favorites -> favoritesCategory ()
            | CategorySelection.Custom name -> name
        and set (value: string) =
            if isUpdatingCategories then ()
            elif isNull value then ()
            else
                let newSelection =
                    if value = allCategories () then CategorySelection.All
                    elif value = favoritesCategory () then CategorySelection.Favorites
                    else CategorySelection.Custom value
                if currentSelection <> newSelection then
                    currentSelection <- newSelection
                    this.OnPropertyChanged("SelectedCategory")
                    refresh ()

    member this.SelectedCommand
        with get () : obj = selectedCommand |> Option.map box |> Option.defaultValue null
        and set (value: obj) =
            let nextValue = if isNull value then None else Some(unbox<CommandEntry> value)
            if selectedCommand <> nextValue then
                selectedCommand <- nextValue
                this.OnPropertyChanged("SelectedCommand")
                this.OnPropertyChanged("HasSelection")
                this.OnPropertyChanged("HasNoSelection")
                this.OnPropertyChanged("FavoriteLabel")

    member this.QuickSelectedCommand
        with get () : obj = quickSelectedCommand |> Option.map box |> Option.defaultValue null
        and set (value: obj) =
            let nextValue = if isNull value then None else Some(unbox<CommandEntry> value)
            if quickSelectedCommand <> nextValue then
                quickSelectedCommand <- nextValue
                this.OnPropertyChanged("QuickSelectedCommand")
                this.OnPropertyChanged("HasQuickSelection")

    member _.CommandCount = commandCount
    member _.FilteredCount = filteredCount
    member _.HasSelection = selectedCommand.IsSome
    member _.HasQuickSelection = quickSelectedCommand.IsSome
    member _.HasQuickResults = quickCommands.Count > 0
    member _.HasNoSelection = selectedCommand.IsNone
    member _.FavoriteLabel =
        if selectedCommand |> Option.exists _.IsFavorite then Localization.get "Text_FavoriteRemove"
        else Localization.get "Text_FavoriteAdd"

    member this.StatusMessage
        with get () = statusMessage
        and set value = this.SetProperty(&statusMessage, value) |> ignore

    member this.Reload() =
        commands <- repository.GetAll()
        updateCategories ()
        this.OnPropertyChanged("SelectedCategory")
        refresh ()
        refreshQuick ()

    member this.RefreshLocalizedCategories() =
        updateCategories ()
        this.OnPropertyChanged("SelectedCategory")
        refresh ()
        this.OnPropertyChanged("FavoriteLabel")

    member this.SaveEntry(entry: CommandEntry) =
        if String.IsNullOrWhiteSpace(entry.Title) then invalidArg "entry" "タイトルを入力してください。"
        if String.IsNullOrWhiteSpace(entry.Category) then invalidArg "entry" "カテゴリを入力してください。"
        if String.IsNullOrWhiteSpace(entry.CommandText) then invalidArg "entry" "コマンドを入力してください。"
        let id = repository.Save(entry)
        this.Reload()
        this.SelectedCategory <- entry.Category.Trim()
        this.SelectedCommand <- commands |> List.find (fun saved -> saved.Id = id) |> box
        this.StatusMessage <- Localization.get "Text_StatusSaved"

    member this.DeleteSelected() =
        match selectedCommand with
        | Some entry ->
            repository.Delete(entry.Id)
            this.StatusMessage <- entry.Title + Localization.get "Text_StatusDeletedPrefix"
            this.Reload()
        | None -> ()

    member this.ToggleFavorite() =
        match selectedCommand with
        | Some entry ->
            let isFavorite = not entry.IsFavorite
            repository.Save({ entry with IsFavorite = isFavorite }) |> ignore
            this.Reload()
            this.StatusMessage <- Localization.get (if isFavorite then "Text_StatusFavoriteAdded" else "Text_StatusFavoriteRemoved")
        | None -> ()

    member this.RecordCopy(id: int64) =
        repository.RecordCopy(id)
        this.Reload()
        this.StatusMessage <- Localization.get "Text_StatusCopied"

    member _.ExportJson(stream: Stream) =
        let options = JsonSerializerOptions()
        options.WriteIndented <- true
        JsonSerializer.Serialize(stream, commands, options)

    member this.ImportJson(stream: Stream) =
        let entries = JsonSerializer.Deserialize<CommandEntry array>(stream)
        if not (isNull entries) then
            repository.Import(Array.toList entries)
            this.Reload()
            let importedMessage = Localization.get "Text_StatusImported"
            this.StatusMessage <- string entries.Length + importedMessage

namespace EasyCommandMemo.Data

open System
open System.IO
open Microsoft.Data.Sqlite
open EasyCommandMemo.Models

type CommandRepository(databasePath: string) =
    let starterCommands =
                [ ("Docker", "コンテナ一覧", "docker ps", "実行中のコンテナを一覧表示します。", "container,list");
                    ("Docker", "全コンテナ一覧", "docker ps -a", "停止中を含むすべてのコンテナを表示します。", "container,list");
                    ("Docker", "イメージ一覧", "docker images", "ローカルに保存されたイメージを一覧表示します。", "image,list");
                    ("Docker", "コンテナログ", "docker logs -f {container}", "指定したコンテナのログを追跡します。", "container,logs");
                    ("Docker", "コンテナに入る", "docker exec -it {container} sh", "コンテナ内でシェルを起動します。", "container,shell");
                    ("Git", "リポジトリを複製", "git clone {url}", "リモートリポジトリをローカルに複製します。", "clone,repository");
                    ("Git", "変更を取得", "git pull --ff-only", "現在のブランチにリモートの変更を取り込みます。", "pull,update");
                    ("Git", "リモート情報を更新", "git fetch --all --prune", "全リモートの情報を取得し、削除済み参照を整理します。", "fetch,remote");
                    ("Git", "作業ツリーを確認", "git status --short --branch", "ブランチ名と変更ファイルを簡潔に表示します。", "status");
                    ("Git", "コミット履歴", "git log --oneline --graph --decorate -20", "直近20件の履歴をグラフ付きで表示します。", "log,history");
                    ("Linux", "ファイル一覧", "ls -lah", "隠しファイルを含めて詳細表示します。", "files,list");
                    ("Linux", "文字列を検索", "grep -Rni -- {pattern} {path}", "指定ディレクトリ以下から文字列を検索します。", "grep,search");
                    ("Linux", "ファイルを検索", "find {path} -type f -name '{pattern}'", "名前が一致するファイルを検索します。", "find,search");
                    ("Linux", "ディスク使用量", "df -h", "ファイルシステムごとの空き容量を表示します。", "disk,storage");
                    ("Linux", "アーカイブを展開", "tar -xzf {file}.tar.gz", "gzip圧縮されたtarアーカイブを展開します。", "tar,archive");
                    ("Kubernetes", "Pod一覧", "kubectl get pods -A", "全NamespaceのPodを一覧表示します。", "kubectl,pod");
                    ("Kubernetes", "リソースを確認", "kubectl get {resource} -n {namespace}", "指定Namespaceのリソースを一覧表示します。", "kubectl,get");
                    ("Kubernetes", "Podログ", "kubectl logs -f {pod} -n {namespace}", "指定Podのログを追跡します。", "kubectl,logs");
                    ("Kubernetes", "リソース詳細", "kubectl describe {resource} {name} -n {namespace}", "リソースの詳細とイベントを表示します。", "kubectl,describe");
                    ("PowerShell", "ディレクトリ一覧", "Get-ChildItem -Force", "隠し項目を含めて一覧表示します。", "files,list");
                    ("PowerShell", "プロセス検索", "Get-Process | Where-Object ProcessName -like '*{name}*'", "名前でプロセスを絞り込みます。", "process,search");
                    ("PowerShell", "ポート使用状況", "Get-NetTCPConnection -State Listen | Sort-Object LocalPort", "待ち受け中のTCPポートを一覧表示します。", "network,port") ]

    let createConnection () = new SqliteConnection($"Data Source={databasePath}")

    do
        let directory = Path.GetDirectoryName(databasePath)
        if not (String.IsNullOrWhiteSpace(directory)) then
            Directory.CreateDirectory(directory) |> ignore
        use connection = createConnection ()
        connection.Open()
        use command = connection.CreateCommand()
        command.CommandText <- """
            CREATE TABLE IF NOT EXISTS Commands (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Category TEXT NOT NULL,
                Tags TEXT NOT NULL DEFAULT '',
                CommandText TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                CopyCount INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Commands_Title ON Commands(Title);
            CREATE TABLE IF NOT EXISTS AppMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
        """
        command.ExecuteNonQuery() |> ignore

        let hasCopyCount =
            use columnCommand = connection.CreateCommand()
            columnCommand.CommandText <- "PRAGMA table_info(Commands)"
            use reader = columnCommand.ExecuteReader()
            let mutable found = false
            while reader.Read() do
                if reader.GetString(1) = "CopyCount" then found <- true
            found

        if not hasCopyCount then
            use migration = connection.CreateCommand()
            migration.CommandText <- "ALTER TABLE Commands ADD COLUMN CopyCount INTEGER NOT NULL DEFAULT 0"
            migration.ExecuteNonQuery() |> ignore

        use transaction = connection.BeginTransaction()
        use countCommand = connection.CreateCommand()
        countCommand.Transaction <- transaction
        countCommand.CommandText <- "SELECT COUNT(*) FROM Commands"
        let isEmpty = Convert.ToInt64(countCommand.ExecuteScalar()) = 0L

        use markerCommand = connection.CreateCommand()
        markerCommand.Transaction <- transaction
        markerCommand.CommandText <- "INSERT OR IGNORE INTO AppMetadata (Key, Value) VALUES ('StarterPackV1', '1')"
        let isFirstLaunch = markerCommand.ExecuteNonQuery() = 1

        if isEmpty && isFirstLaunch then
            use seedCommand = connection.CreateCommand()
            seedCommand.Transaction <- transaction
            seedCommand.CommandText <- "INSERT INTO Commands (Title, Category, Tags, CommandText, Description, IsFavorite, UpdatedAt) VALUES ($title, $category, $tags, $text, $description, 0, $updatedAt)"
            let titleParameter = seedCommand.Parameters.Add("$title", SqliteType.Text)
            let categoryParameter = seedCommand.Parameters.Add("$category", SqliteType.Text)
            let tagsParameter = seedCommand.Parameters.Add("$tags", SqliteType.Text)
            let textParameter = seedCommand.Parameters.Add("$text", SqliteType.Text)
            let descriptionParameter = seedCommand.Parameters.Add("$description", SqliteType.Text)
            let updatedAtParameter = seedCommand.Parameters.Add("$updatedAt", SqliteType.Text)
            let now = DateTimeOffset.UtcNow.ToString("O")
            for category, title, commandText, description, tags in starterCommands do
                titleParameter.Value <- title
                categoryParameter.Value <- category
                tagsParameter.Value <- tags
                textParameter.Value <- commandText
                descriptionParameter.Value <- description
                updatedAtParameter.Value <- now
                seedCommand.ExecuteNonQuery() |> ignore

        transaction.Commit()

    new() =
        CommandRepository(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasyCommandMemo",
                "commands.db"
            )
        )

    member _.GetAll() =
        use connection = createConnection ()
        connection.Open()
        use command = connection.CreateCommand()
        command.CommandText <- "SELECT Id, Title, Category, Tags, CommandText, Description, IsFavorite, UpdatedAt, CopyCount FROM Commands ORDER BY CopyCount DESC, IsFavorite DESC, Title COLLATE NOCASE"
        use reader = command.ExecuteReader()
        [ while reader.Read() do
              yield
                  { Id = reader.GetInt64(0)
                    Title = reader.GetString(1)
                    Category = reader.GetString(2)
                    Tags = reader.GetString(3)
                    CommandText = reader.GetString(4)
                    Description = reader.GetString(5)
                    IsFavorite = reader.GetInt64(6) <> 0L
                    UpdatedAt = reader.GetString(7)
                    CopyCount = reader.GetInt64(8) } ]

    member _.Save(entry: CommandEntry) =
        use connection = createConnection ()
        connection.Open()
        use command = connection.CreateCommand()
        command.CommandText <-
            if entry.Id = 0L then
                "INSERT INTO Commands (Title, Category, Tags, CommandText, Description, IsFavorite, UpdatedAt) VALUES ($title, $category, $tags, $text, $description, $favorite, $updatedAt); SELECT last_insert_rowid();"
            else
                "UPDATE Commands SET Title=$title, Category=$category, Tags=$tags, CommandText=$text, Description=$description, IsFavorite=$favorite, UpdatedAt=$updatedAt WHERE Id=$id; SELECT $id;"
        command.Parameters.AddWithValue("$title", entry.Title.Trim()) |> ignore
        command.Parameters.AddWithValue("$category", entry.Category.Trim()) |> ignore
        command.Parameters.AddWithValue("$tags", entry.Tags.Trim()) |> ignore
        command.Parameters.AddWithValue("$text", entry.CommandText.Trim()) |> ignore
        command.Parameters.AddWithValue("$description", entry.Description.Trim()) |> ignore
        command.Parameters.AddWithValue("$favorite", if entry.IsFavorite then 1 else 0) |> ignore
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O")) |> ignore
        if entry.Id <> 0L then command.Parameters.AddWithValue("$id", entry.Id) |> ignore
        command.ExecuteScalar() |> Convert.ToInt64

    member _.Delete(id: int64) =
        use connection = createConnection ()
        connection.Open()
        use command = connection.CreateCommand()
        command.CommandText <- "DELETE FROM Commands WHERE Id=$id"
        command.Parameters.AddWithValue("$id", id) |> ignore
        command.ExecuteNonQuery() |> ignore

    member _.RecordCopy(id: int64) =
        use connection = createConnection ()
        connection.Open()
        use command = connection.CreateCommand()
        command.CommandText <- "UPDATE Commands SET CopyCount = CopyCount + 1 WHERE Id = $id"
        command.Parameters.AddWithValue("$id", id) |> ignore
        command.ExecuteNonQuery() |> ignore

    member this.Import(entries: CommandEntry list) =
        for entry in entries do
            this.Save({ entry with Id = 0L }) |> ignore

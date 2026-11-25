<#
.SYNOPSIS
    EasyCommandMemo の Microsoft Store 向け MSIX パッケージを作成するスクリプト

.DESCRIPTION
    dotnet publish でアプリケーションをビルドし、Windows SDK ツール (makepri, makeappx, signtool) を
    使用して Microsoft Store 提出用およびローカルテスト用の MSIX パッケージを自動生成します。

.PARAMETER Configuration
    ビルド構成 (Release または Debug)。既定値は Release です。

.PARAMETER Runtime
    ランタイム識別子 (RID)。既定値は win-x64 です。

.PARAMETER Version
    パッケージバージョン (例: 1.0.0.0)。

.PARAMETER PackageName
    Microsoft パートナーセンターで予約したパッケージ名 (例: KumaProject.EasyCommandMemo)。

.PARAMETER Publisher
    パートナーセンターに登録されている発行者 ID (例: CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX)。
    未指定の場合はマニフェスト既定値またはテスト用 "CN=KumaProject" が使用されます。

.PARAMETER PublisherDisplayName
    発行者表示名。既定値は "KumaProject" です。

.PARAMETER SelfContained
    .NET ランタイムを同梱するかどうか。既定値は $true です。

.PARAMETER SignForTesting
    ローカル環境で直接インストール・テストできるように自己署名証明書を作成して署名します。
    (※Microsoft Store 提出用には未署名、またはこの証明書のまま提出可能です)

.PARAMETER SkipPublish
    直前の dotnet publish 出力を再利用してパッケージングのみを高速に行います。
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.1.1.0",
    [string]$PackageName = "XXXX",
    [string]$Publisher = "CN=XXXX",
    [string]$PublisherDisplayName = "XXXX",
    [bool]$SelfContained = $true,
    [switch]$SignForTesting,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "EasyCommandMemo"
$fsprojPath = Join-Path $projectDir "EasyCommandMemo.fsproj"
$manifestPath = Join-Path $projectDir "Package.appxmanifest"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   EasyCommandMemo MSIX パッケージ ビルダー" -ForegroundColor Cyan
Write-Host "   構成: $Configuration | ランタイム: $Runtime | バージョン: $Version" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Windows SDK ツールの検出
function Find-SdkTool {
    param([string]$toolName)
    $searchPaths = @(
        "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\$toolName",
        "C:\Program Files\Windows Kits\10\bin\*\x64\$toolName"
    )
    $found = Get-ChildItem -Path $searchPaths -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($null -eq $found) {
        throw "Windows SDK ツール '$toolName' が見つかりませんでした。Windows 10/11 SDK がインストールされているか確認してください。"
    }
    return $found.FullName
}

$makeAppx = Find-SdkTool "makeappx.exe"
$makePri = Find-SdkTool "makepri.exe"
$signTool = Find-SdkTool "signtool.exe"

Write-Host "`nWindows SDK ツールを検出しました:" -ForegroundColor Green
Write-Host "  makeappx: $makeAppx"
Write-Host "  makepri:  $makePri"
Write-Host "  signtool: $signTool"

# 2. 出力ディレクトリの準備
$distDir = Join-Path $scriptDir "dist"
$publishDir = Join-Path $distDir "publish-$Runtime"
$stagingDir = Join-Path $distDir "msix-staging"
$outputMsix = Join-Path $distDir "EasyCommandMemo-$Version-$Runtime.msix"

if (!(Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
New-Item -ItemType Directory -Path $stagingDir | Out-Null

# 3. dotnet publish の実行
if (!$SkipPublish) {
    Write-Host "`n[1/5] dotnet publish を実行中 (自己完結型バイナリのビルド)..." -ForegroundColor Cyan
    $selfContainedArg = if ($SelfContained) { "--self-contained true" } else { "--self-contained false" }
    $publishCmd = "dotnet publish `"$fsprojPath`" -c $Configuration -r $Runtime $selfContainedArg -o `"$publishDir`" -p:PublishSingleFile=false"
    Write-Host "実行コマンド: $publishCmd"
    Invoke-Expression $publishCmd
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish に失敗しました。" }
} else {
    Write-Host "`n[1/5] 発行をスキップし、既存の $publishDir を使用します。" -ForegroundColor Yellow
}

# 4. ステージング領域へのファイル配置
Write-Host "`n[2/5] パッケージ ステージング領域を準備中..." -ForegroundColor Cyan
Copy-Item -Path "$publishDir\*" -Destination $stagingDir -Recurse -Force

# Assets/Store を配置
$stagingAssetsStore = Join-Path $stagingDir "Assets\Store"
if (!(Test-Path $stagingAssetsStore)) { New-Item -ItemType Directory -Path $stagingAssetsStore -Force | Out-Null }
$sourceAssetsStore = Join-Path $projectDir "Assets\Store"
if (Test-Path $sourceAssetsStore) {
    Copy-Item -Path "$sourceAssetsStore\*" -Destination $stagingAssetsStore -Recurse -Force
}

# 5. AppxManifest.xml の生成・適用
Write-Host "`n[3/5] AppxManifest.xml を構成中..." -ForegroundColor Cyan
$manifestRaw = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
[xml]$manifestXml = $manifestRaw

if (![string]::IsNullOrWhiteSpace($PackageName)) {
    $manifestXml.Package.Identity.Name = $PackageName
}
if (![string]::IsNullOrWhiteSpace($Version)) {
    $manifestXml.Package.Identity.Version = $Version
}
if (![string]::IsNullOrWhiteSpace($Publisher)) {
    $manifestXml.Package.Identity.Publisher = $Publisher
}
if (![string]::IsNullOrWhiteSpace($PublisherDisplayName)) {
    $manifestXml.Package.Properties.PublisherDisplayName = $PublisherDisplayName
}

$targetManifest = Join-Path $stagingDir "AppxManifest.xml"
$writerSettings = New-Object System.Xml.XmlWriterSettings
$writerSettings.Encoding = [System.Text.Encoding]::UTF8
$writerSettings.Indent = $true
$xmlWriter = [System.Xml.XmlWriter]::Create($targetManifest, $writerSettings)
$manifestXml.Save($xmlWriter)
$xmlWriter.Dispose()
Write-Host "マニフェストを設定しました:"
Write-Host "  PackageName:  $($manifestXml.Package.Identity.Name)"
Write-Host "  Version:      $($manifestXml.Package.Identity.Version)"
Write-Host "  Publisher:    $($manifestXml.Package.Identity.Publisher)"

# 6. PRI (Package Resource Index) の生成
Write-Host "`n[4/5] resources.pri (リソースインデックス) を生成中..." -ForegroundColor Cyan
$priConfigFile = Join-Path $distDir "priconfig.xml"
$targetPri = Join-Path $stagingDir "resources.pri"

& $makePri createconfig /cf "$priConfigFile" /pr "$stagingDir" /dq lang-ja-JP /o | Out-Null
& $makePri new /pr "$stagingDir" /cf "$priConfigFile" /of "$targetPri" /o | Out-Null

if (Test-Path $targetPri) {
    Write-Host "resources.pri を正常に生成しました。" -ForegroundColor Green
}

# 7. makeappx による MSIX パッケージ生成
Write-Host "`n[5/5] MSIX パッケージをビルド中..." -ForegroundColor Cyan
if (Test-Path $outputMsix) { Remove-Item -Force $outputMsix }

& $makeAppx pack /d "$stagingDir" /p "$outputMsix" /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack に失敗しました。" }

$item = Get-Item $outputMsix
Write-Host "`nMSIX パッケージの作成に成功しました！" -ForegroundColor Green
Write-Host "出力先: $($item.FullName)"
Write-Host "サイズ: $([math]::Round($item.Length / 1MB, 2)) MB"

# 8. ローカルテスト用署名 (オプション)
if ($SignForTesting) {
    Write-Host "`n=== テスト用自己署名証明書で署名中 ===" -ForegroundColor Cyan
    $certSubject = $manifestXml.Package.Identity.Publisher
    Write-Host "証明書サブジェクト: $certSubject"

    $pfxPath = Join-Path $distDir "TestCert.pfx"
    $cerPath = Join-Path $distDir "TestCert.cer"
    $pfxPassword = "TestPassword123!"

    # 証明書が未作成なら .NET 暗号化ライブラリで生成 (OSストア非依存)
    if (!(Test-Path $pfxPath) -or !(Test-Path $cerPath)) {
        Write-Host "テスト用証明書を生成しています..."
        Add-Type -AssemblyName System.Security
        $rsa = [System.Security.Cryptography.RSA]::Create(2048)
        $req = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
            $certSubject,
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
        )
        $ext = New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
            (New-Object System.Security.Cryptography.OidCollection),
            $false
        )
        $ext.EnhancedKeyUsages.Add((New-Object System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.3"))) | Out-Null
        $req.CertificateExtensions.Add($ext) | Out-Null
        $cert = $req.CreateSelfSigned([System.DateTimeOffset]::UtcNow.AddDays(-1), [System.DateTimeOffset]::UtcNow.AddYears(5))
        
        $pfxBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $pfxPassword)
        [System.IO.File]::WriteAllBytes($pfxPath, $pfxBytes)
        $cerBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        [System.IO.File]::WriteAllBytes($cerPath, $cerBytes)
    }

    # signtool による署名
    & $signTool sign /fd SHA256 /a /f "$pfxPath" /p $pfxPassword "$outputMsix"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "テスト署名に成功しました！" -ForegroundColor Green
        Write-Host "※PC上で直接ダブルクリックしてインストールする場合は、生成された $cerPath を「信頼されたルート証明機関」に一度インポートしてください。" -ForegroundColor Yellow
    }
}

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "Microsoft Store 提出用 MSIX パッケージの準備完了！" -ForegroundColor Green
Write-Host "ファイル: $($item.FullName)"
Write-Host "サイズ:   $([math]::Round($item.Length / 1MB, 2)) MB"
Write-Host ""
Write-Host "【ストアへの提出手順】"
Write-Host " 1. Microsoft パートナーセンターでアプリ名を予約し、発行者 ID を確認"
Write-Host " 2. 必要に応じてパラメータを指定してパッケージを再ビルド:"
Write-Host "    .\build-msix.ps1 -Publisher `"CN=XXXXXXXX-XXXX-...`" -PackageName `"YourReservedName`""
Write-Host " 3. パートナーセンターの「パッケージ」画面に生成された .msix をアップロード"
Write-Host "========================================================" -ForegroundColor Green
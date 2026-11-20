<#
.SYNOPSIS
    EasyCommandMemo MSIX パッケージのビルド (build-msix.ps1 のエイリアス)
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.1.0.0",
    [string]$PackageName = "XXXX",
    [string]$Publisher = "CN=XXXX",
    [string]$PublisherDisplayName = "XXXX",
    [bool]$SelfContained = $true,
    [switch]$SignForTesting,
    [switch]$SkipPublish
)

& "$PSScriptRoot\build-msix.ps1" @PSBoundParameters
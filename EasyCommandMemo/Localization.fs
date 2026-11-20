namespace EasyCommandMemo

open System
open System.Globalization
open System.IO
open System.Reflection
open System.Resources
open Avalonia

module Localization =
    let private resourceManager = ResourceManager("EasyCommandMemo.Strings", Assembly.GetExecutingAssembly())
    let private baseCulture = CultureInfo("ja")
    let private settingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyCommandMemo", "language.txt")
    let mutable private currentLanguage = "ja"

    let languages =
        [ "ja", "日本語"
          "en", "English"
          "zh-CN", "简体中文"
          "zh-TW", "繁體中文 (台灣)"
          "ko", "한국어"
          "vi", "Tiếng Việt"
          "de", "Deutsch"
          "fr", "Français"
          "es", "Español"
          "en-AU", "English (Australia)"
          "de-CH", "Deutsch (Schweiz)" ]

    let current () = currentLanguage

    let get key =
        resourceManager.GetString(key, CultureInfo(currentLanguage))
        |> Option.ofObj
        |> Option.defaultValue key

    let set (language: string) =
        if not (languages |> List.exists (fun (code, _) -> code = language)) then invalidArg "language" "Unsupported language."
        let culture = CultureInfo(language)
        currentLanguage <- language
        CultureInfo.CurrentUICulture <- culture
        CultureInfo.DefaultThreadCurrentUICulture <- culture

        if not (isNull Application.Current) then
            let baseResources = resourceManager.GetResourceSet(baseCulture, true, true)
            let entries = baseResources.GetEnumerator()
            while entries.MoveNext() do
                let key = string entries.Key
                let value = resourceManager.GetString(key, culture)
                Application.Current.Resources[key] <- (if isNull value then string entries.Value else value)

        try
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)) |> ignore
            File.WriteAllText(settingsPath, language)
        with _ -> ()

    let load () =
        let language =
            try
                if File.Exists(settingsPath) then File.ReadAllText(settingsPath).Trim() else "ja"
            with _ -> "ja"
        set (if languages |> List.exists (fun (code, _) -> code = language) then language else "ja")

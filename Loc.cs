using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;

namespace ChatTranslator;

/// <summary>
/// Traductions de l'interface du plugin. Anglais par défaut : toute clé manquante
/// dans une langue retombe sur l'anglais, puis sur la clé elle-même.
/// </summary>
public static class Loc
{
    public static readonly string[] Supported = { "en", "fr", "de", "ja" };

    private static string current = "en";

    public static string Current => current;

    /// <summary>Déclenché quand la langue de l'interface change réellement.</summary>
    public static event Action? LanguageChanged;

    /// <param name="setting">"auto" (langue du jeu) ou un code : en, fr, de, ja.</param>
    public static void Apply(string setting, ClientLanguage game)
    {
        var code = setting == "auto" ? FromGame(game) : setting;
        if (!Supported.Contains(code))
            code = "en";

        if (code == current)
            return;

        current = code;
        LanguageChanged?.Invoke();
    }

    private static string FromGame(ClientLanguage game) => game switch
    {
        ClientLanguage.French => "fr",
        ClientLanguage.German => "de",
        ClientLanguage.Japanese => "ja",
        _ => "en",
    };

    public static string T(string key)
    {
        if (Tables.TryGetValue(current, out var table) && table.TryGetValue(key, out var value))
            return value;

        if (Tables["en"].TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["en"] = new()
        {
            ["ui_language"] = "Interface language",
            ["ui_language_auto"] = "Automatic (game language)",
            ["enable"] = "Enable translation",
            ["target"] = "Translate to",
            ["overwrite"] = "Replace the original message with its translation",
            ["wait_translation"] = "Wait briefly for the translation (lets the game chat bubbles show it)",
            ["show_tag"] = "Show [EN→FR] (click it to see the original)",
            ["provider"] = "Service",
            ["provider_google"] = "Google (free, no key)",
            ["provider_deepl"] = "DeepL (API key)",
            ["service_tip"] = "Google: free and needs no account, but less accurate.\nDeepL: more accurate, but needs your own API key. Free up to 500,000 characters per month, then a paid plan is required.\nClick (i) to open a guide on creating a DeepL API key.",
            ["deepl_guide"] = "How to create a DeepL API key",
            ["tw_title"] = "Translate my message",
            ["tw_target"] = "Translate to",
            ["tw_input"] = "Your message",
            ["tw_output"] = "Translation",
            ["tw_detected"] = "Detected language:",
            ["tw_copy"] = "Copy to clipboard",
            ["tw_copied"] = "Copied!",
            ["tw_translating"] = "Translating...",
            ["tw_error"] = "Translation failed. Check your connection or your DeepL key.",
            ["tw_clear"] = "Clear",
            ["htranslator_help"] = "/htranslator: open the window to translate your own messages.",
            ["deepl_key"] = "DeepL key",
            ["ignored"] = "Ignored languages (e.g. en,de)",
            ["min_length"] = "Minimum length",
            ["channels"] = "Channels to translate",
            ["ch_say"] = "Say",
            ["ch_shout"] = "Shout",
            ["ch_yell"] = "Yell",
            ["ch_party"] = "Party",
            ["ch_crossparty"] = "Party (cross-world)",
            ["ch_alliance"] = "Alliance",
            ["ch_fc"] = "Free Company",
            ["ch_tell"] = "Incoming tells",
            ["ls"] = "Linkshell",
            ["cwls"] = "CWLS",
            ["popup_title"] = "Original message:",
            ["msg_enabled"] = "[Chat Translator] Translation enabled.",
            ["msg_disabled"] = "[Chat Translator] Translation disabled.",
            ["command_help"] = "/htranslate: open settings. /htranslate on|off: enable / disable translation.",
            ["lang_fr"] = "French",
            ["lang_en"] = "English",
            ["lang_de"] = "German",
            ["lang_ja"] = "Japanese",
            ["lang_es"] = "Spanish",
            ["lang_it"] = "Italian",
            ["lang_pt"] = "Portuguese",
            ["lang_nl"] = "Dutch",
            ["lang_pl"] = "Polish",
            ["lang_ru"] = "Russian",
            ["lang_tr"] = "Turkish",
            ["lang_ko"] = "Korean",
            ["lang_zh"] = "Chinese (simplified)",
            ["lang_ar"] = "Arabic",
            ["version"] = "Version",
        },
        ["fr"] = new()
        {
            ["ui_language"] = "Langue de l'interface",
            ["ui_language_auto"] = "Automatique (langue du jeu)",
            ["enable"] = "Activer la traduction",
            ["target"] = "Traduire vers",
            ["overwrite"] = "Remplacer le message d'origine par la traduction",
            ["wait_translation"] = "Attendre brièvement la traduction (les bulles de chat du jeu l'affichent)",
            ["show_tag"] = "Afficher [EN→FR] (clic dessus = voir l'original)",
            ["provider"] = "Service",
            ["provider_google"] = "Google (gratuit, sans clé)",
            ["provider_deepl"] = "DeepL (clé API)",
            ["service_tip"] = "Google : gratuit et sans compte, mais moins précis.\nDeepL : plus précis, mais nécessite votre propre clé API. Gratuit jusqu'à 500 000 caractères par mois, ensuite un abonnement payant est nécessaire.\nCliquez sur (i) pour ouvrir un guide de création de clé API DeepL.",
            ["deepl_guide"] = "Comment créer une clé API DeepL",
            ["tw_title"] = "Traduire mon message",
            ["tw_target"] = "Traduire vers",
            ["tw_input"] = "Votre message",
            ["tw_output"] = "Traduction",
            ["tw_detected"] = "Langue détectée :",
            ["tw_copy"] = "Copier dans le presse-papiers",
            ["tw_copied"] = "Copié !",
            ["tw_translating"] = "Traduction en cours...",
            ["tw_error"] = "Échec de la traduction. Vérifiez votre connexion ou votre clé DeepL.",
            ["tw_clear"] = "Effacer",
            ["htranslator_help"] = "/htranslator : ouvre la fenêtre pour traduire vos propres messages.",
            ["deepl_key"] = "Clé DeepL",
            ["ignored"] = "Langues ignorées (ex: en,de)",
            ["min_length"] = "Longueur minimale",
            ["channels"] = "Canaux à traduire",
            ["ch_say"] = "Dire",
            ["ch_shout"] = "Crier",
            ["ch_yell"] = "Hurler",
            ["ch_party"] = "Équipe",
            ["ch_crossparty"] = "Équipe (inter-monde)",
            ["ch_alliance"] = "Alliance",
            ["ch_fc"] = "Compagnie libre",
            ["ch_tell"] = "Messages privés reçus",
            ["ls"] = "Linkshell",
            ["cwls"] = "CWLS",
            ["popup_title"] = "Message d'origine :",
            ["msg_enabled"] = "[Chat Translator] Traduction activée.",
            ["msg_disabled"] = "[Chat Translator] Traduction désactivée.",
            ["command_help"] = "/htranslate : ouvre la config. /htranslate on|off : active / désactive la traduction.",
            ["lang_fr"] = "Français",
            ["lang_en"] = "Anglais",
            ["lang_de"] = "Allemand",
            ["lang_ja"] = "Japonais",
            ["lang_es"] = "Espagnol",
            ["lang_it"] = "Italien",
            ["lang_pt"] = "Portugais",
            ["lang_nl"] = "Néerlandais",
            ["lang_pl"] = "Polonais",
            ["lang_ru"] = "Russe",
            ["lang_tr"] = "Turc",
            ["lang_ko"] = "Coréen",
            ["lang_zh"] = "Chinois (simplifié)",
            ["lang_ar"] = "Arabe",
            ["version"] = "Version",
        },
        ["de"] = new()
        {
            ["ui_language"] = "Oberflächensprache",
            ["ui_language_auto"] = "Automatisch (Spielsprache)",
            ["enable"] = "Übersetzung aktivieren",
            ["target"] = "Übersetzen nach",
            ["overwrite"] = "Originalnachricht durch die Übersetzung ersetzen",
            ["wait_translation"] = "Kurz auf die Übersetzung warten (Sprechblasen im Spiel zeigen sie an)",
            ["show_tag"] = "[EN→FR] anzeigen (anklicken, um das Original zu sehen)",
            ["provider"] = "Dienst",
            ["provider_google"] = "Google (kostenlos, ohne Schlüssel)",
            ["provider_deepl"] = "DeepL (API-Schlüssel)",
            ["service_tip"] = "Google: kostenlos und ohne Konto, aber weniger genau.\nDeepL: genauer, benötigt aber einen eigenen API-Schlüssel. Kostenlos bis 500.000 Zeichen pro Monat, danach ist ein kostenpflichtiger Tarif nötig.\nKlicke auf (i), um eine Anleitung zum Erstellen eines DeepL-API-Schlüssels zu öffnen.",
            ["deepl_guide"] = "So erstellst du einen DeepL-API-Schlüssel",
            ["tw_title"] = "Meine Nachricht übersetzen",
            ["tw_target"] = "Übersetzen nach",
            ["tw_input"] = "Deine Nachricht",
            ["tw_output"] = "Übersetzung",
            ["tw_detected"] = "Erkannte Sprache:",
            ["tw_copy"] = "In die Zwischenablage kopieren",
            ["tw_copied"] = "Kopiert!",
            ["tw_translating"] = "Wird übersetzt ...",
            ["tw_error"] = "Übersetzung fehlgeschlagen. Prüfe deine Verbindung oder deinen DeepL-Schlüssel.",
            ["tw_clear"] = "Leeren",
            ["htranslator_help"] = "/htranslator: Öffnet das Fenster, um eigene Nachrichten zu übersetzen.",
            ["deepl_key"] = "DeepL-Schlüssel",
            ["ignored"] = "Ignorierte Sprachen (z. B. en,de)",
            ["min_length"] = "Mindestlänge",
            ["channels"] = "Zu übersetzende Kanäle",
            ["ch_say"] = "Sagen",
            ["ch_shout"] = "Rufen",
            ["ch_yell"] = "Schreien",
            ["ch_party"] = "Gruppe",
            ["ch_crossparty"] = "Gruppe (weltübergreifend)",
            ["ch_alliance"] = "Allianz",
            ["ch_fc"] = "Freie Gesellschaft",
            ["ch_tell"] = "Eingehende Flüsternachrichten",
            ["ls"] = "Linkshell",
            ["cwls"] = "CWLS",
            ["popup_title"] = "Originalnachricht:",
            ["msg_enabled"] = "[Chat Translator] Übersetzung aktiviert.",
            ["msg_disabled"] = "[Chat Translator] Übersetzung deaktiviert.",
            ["command_help"] = "/htranslate: Einstellungen öffnen. /htranslate on|off: Übersetzung ein-/ausschalten.",
            ["lang_fr"] = "Französisch",
            ["lang_en"] = "Englisch",
            ["lang_de"] = "Deutsch",
            ["lang_ja"] = "Japanisch",
            ["lang_es"] = "Spanisch",
            ["lang_it"] = "Italienisch",
            ["lang_pt"] = "Portugiesisch",
            ["lang_nl"] = "Niederländisch",
            ["lang_pl"] = "Polnisch",
            ["lang_ru"] = "Russisch",
            ["lang_tr"] = "Türkisch",
            ["lang_ko"] = "Koreanisch",
            ["lang_zh"] = "Chinesisch (vereinfacht)",
            ["lang_ar"] = "Arabisch",
            ["version"] = "Version",
        },
        ["ja"] = new()
        {
            ["ui_language"] = "表示言語",
            ["ui_language_auto"] = "自動（ゲームの言語）",
            ["enable"] = "翻訳を有効にする",
            ["target"] = "翻訳先の言語",
            ["overwrite"] = "元のメッセージを翻訳で置き換える",
            ["wait_translation"] = "翻訳を少し待つ（ゲームのチャットバブルに翻訳を表示）",
            ["show_tag"] = "[EN→FR] を表示（クリックで原文を表示）",
            ["provider"] = "サービス",
            ["provider_google"] = "Google（無料・キー不要）",
            ["provider_deepl"] = "DeepL（APIキー）",
            ["service_tip"] = "Google: 無料でアカウント不要ですが、精度は低めです。\nDeepL: 精度は高めですが、自分のAPIキーが必要です。月50万文字まで無料で、それを超えると有料プランが必要です。\n(i) をクリックすると、DeepL APIキーの作成ガイドが開きます。",
            ["deepl_guide"] = "DeepL APIキーの作成方法",
            ["tw_title"] = "メッセージを翻訳",
            ["tw_target"] = "翻訳先の言語",
            ["tw_input"] = "あなたのメッセージ",
            ["tw_output"] = "翻訳結果",
            ["tw_detected"] = "検出された言語:",
            ["tw_copy"] = "クリップボードにコピー",
            ["tw_copied"] = "コピーしました！",
            ["tw_translating"] = "翻訳中...",
            ["tw_error"] = "翻訳に失敗しました。接続またはDeepLキーを確認してください。",
            ["tw_clear"] = "クリア",
            ["htranslator_help"] = "/htranslator: 自分のメッセージを翻訳するウィンドウを開きます。",
            ["deepl_key"] = "DeepLキー",
            ["ignored"] = "無視する言語（例: en,de）",
            ["min_length"] = "最小文字数",
            ["channels"] = "翻訳するチャンネル",
            ["ch_say"] = "発言",
            ["ch_shout"] = "シャウト",
            ["ch_yell"] = "イエル",
            ["ch_party"] = "パーティ",
            ["ch_crossparty"] = "パーティ（ワールド間）",
            ["ch_alliance"] = "アライアンス",
            ["ch_fc"] = "フリーカンパニー",
            ["ch_tell"] = "受信したテル",
            ["ls"] = "リンクシェル",
            ["cwls"] = "CWLS",
            ["popup_title"] = "元のメッセージ:",
            ["msg_enabled"] = "[Chat Translator] 翻訳を有効にしました。",
            ["msg_disabled"] = "[Chat Translator] 翻訳を無効にしました。",
            ["command_help"] = "/htranslate: 設定を開く。/htranslate on|off: 翻訳の有効/無効を切り替え。",
            ["lang_fr"] = "フランス語",
            ["lang_en"] = "英語",
            ["lang_de"] = "ドイツ語",
            ["lang_ja"] = "日本語",
            ["lang_es"] = "スペイン語",
            ["lang_it"] = "イタリア語",
            ["lang_pt"] = "ポルトガル語",
            ["lang_nl"] = "オランダ語",
            ["lang_pl"] = "ポーランド語",
            ["lang_ru"] = "ロシア語",
            ["lang_tr"] = "トルコ語",
            ["lang_ko"] = "韓国語",
            ["lang_zh"] = "中国語（簡体字）",
            ["lang_ar"] = "アラビア語",
            ["version"] = "バージョン",
        },
    };
}

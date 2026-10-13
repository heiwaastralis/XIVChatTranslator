using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenchChatTranslator;

public sealed class ConfigWindow : Window
{
    // Type de canal, clé de traduction, numéro (0 = pas de numéro).
    private static readonly (XivChatType Type, string Key, int Number)[] Channels =
    {
        (XivChatType.Say, "ch_say", 0),
        (XivChatType.Shout, "ch_shout", 0),
        (XivChatType.Yell, "ch_yell", 0),
        (XivChatType.Party, "ch_party", 0),
        (XivChatType.CrossParty, "ch_crossparty", 0),
        (XivChatType.Alliance, "ch_alliance", 0),
        (XivChatType.FreeCompany, "ch_fc", 0),
        (XivChatType.TellIncoming, "ch_tell", 0),
        (XivChatType.Ls1, "ls", 1), (XivChatType.Ls2, "ls", 2),
        (XivChatType.Ls3, "ls", 3), (XivChatType.Ls4, "ls", 4),
        (XivChatType.Ls5, "ls", 5), (XivChatType.Ls6, "ls", 6),
        (XivChatType.Ls7, "ls", 7), (XivChatType.Ls8, "ls", 8),
        (XivChatType.CrossLinkShell1, "cwls", 1), (XivChatType.CrossLinkShell2, "cwls", 2),
        (XivChatType.CrossLinkShell3, "cwls", 3), (XivChatType.CrossLinkShell4, "cwls", 4),
        (XivChatType.CrossLinkShell5, "cwls", 5), (XivChatType.CrossLinkShell6, "cwls", 6),
        (XivChatType.CrossLinkShell7, "cwls", 7), (XivChatType.CrossLinkShell8, "cwls", 8),
    };

    // Codes de langue cibles (Google / DeepL). Les libellés viennent de Loc ("lang_xx").
    private static readonly string[] LanguageCodes =
        { "fr", "en", "de", "ja", "es", "it", "pt", "nl", "pl", "ru", "tr", "ko", "zh-CN", "ar" };

    // Réglage de la langue de l'interface : "auto" ou un code supporté.
    private static readonly string[] UiSettings = { "auto", "en", "fr", "de", "ja" };

    // Noms natifs, pour que chacun retrouve sa langue même si l'interface est dans une autre.
    private static readonly string[] UiNativeNames = { "English", "Français", "Deutsch", "日本語" };

    // Guide officiel DeepL : créer un compte, choisir un plan (gratuit ou payant) et récupérer sa clé API.
    private const string DeepLGuideUrl = "https://developers.deepl.com/docs/getting-started/quickstart";

    private static void OpenDeepLGuide()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DeepLGuideUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Impossible d'ouvrir le guide DeepL");
        }
    }

    // La version vient de <Version> dans le .csproj : un seul endroit à modifier.
    private static readonly string VersionText = GetVersionText();

    private static string GetVersionText()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private readonly Configuration config;

    public ConfigWindow(Configuration config) : base("Chat Translator##config")
    {
        this.config = config;
        this.Size = new Vector2(440, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    private static string LanguageLabel(string code)
        => Loc.T("lang_" + (code == "zh-CN" ? "zh" : code));

    public override void Draw()
    {
        // --- Langue de l'interface ---
        var uiItems = new[] { Loc.T("ui_language_auto") }.Concat(UiNativeNames).ToArray();
        var uiIndex = Array.IndexOf(UiSettings, this.config.UiLanguage);
        if (uiIndex < 0) uiIndex = 0;
        if (ImGui.Combo(Loc.T("ui_language"), ref uiIndex, uiItems, uiItems.Length))
        {
            this.config.UiLanguage = UiSettings[uiIndex];
            this.config.Save();
            Loc.Apply(this.config.UiLanguage, Plugin.ClientState.ClientLanguage);
        }

        ImGui.Separator();

        var enabled = this.config.Enabled;
        if (ImGui.Checkbox(Loc.T("enable"), ref enabled))
        {
            this.config.Enabled = enabled;
            this.config.Save();
        }

        // --- Langue cible ---
        var langLabels = LanguageCodes.Select(LanguageLabel).ToArray();
        var langIndex = Array.IndexOf(LanguageCodes, this.config.TargetLanguage);
        if (langIndex < 0) langIndex = 0;
        if (ImGui.Combo(Loc.T("target"), ref langIndex, langLabels, langLabels.Length))
        {
            this.config.TargetLanguage = LanguageCodes[langIndex];
            this.config.Save();
        }

        var overwrite = this.config.OverwriteOriginal;
        if (ImGui.Checkbox(Loc.T("overwrite"), ref overwrite))
        {
            this.config.OverwriteOriginal = overwrite;
            this.config.Save();
        }

        var showTag = this.config.ShowTag;
        if (ImGui.Checkbox(Loc.T("show_tag"), ref showTag))
        {
            this.config.ShowTag = showTag;
            this.config.Save();
        }

        // --- Service ---
        var providers = new[] { Loc.T("provider_google"), Loc.T("provider_deepl") };
        var provider = (int)this.config.Provider;
        if (ImGui.Combo(Loc.T("provider"), ref provider, providers, providers.Length))
        {
            this.config.Provider = (Provider)provider;
            this.config.Save();
        }

        // Icône d'information (rond avec un « i », FontAwesome) : survol = comparatif Google / DeepL,
        // clic = guide de création d'une clé DeepL. HelpMarker se place sur la même ligne que le sélecteur.
        ImGuiComponents.HelpMarker(Loc.T("service_tip"));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (ImGui.IsItemClicked())
            OpenDeepLGuide();

        if (this.config.Provider == Provider.DeepLFree)
        {
            var key = this.config.DeepLApiKey;
            if (ImGui.InputText(Loc.T("deepl_key"), ref key, 100, ImGuiInputTextFlags.Password))
            {
                this.config.DeepLApiKey = key;
                this.config.Save();
            }

            // Lien cliquable vers le guide de création de clé.
            ImGui.TextColored(new Vector4(0.45f, 0.7f, 1f, 1f), Loc.T("deepl_guide"));
            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            if (ImGui.IsItemClicked())
                OpenDeepLGuide();
        }

        var ignored = this.config.IgnoredLanguages;
        if (ImGui.InputText(Loc.T("ignored"), ref ignored, 100))
        {
            this.config.IgnoredLanguages = ignored;
            this.config.Save();
        }

        var min = this.config.MinLength;
        if (ImGui.SliderInt(Loc.T("min_length"), ref min, 1, 30))
        {
            this.config.MinLength = min;
            this.config.Save();
        }

        ImGui.Separator();
        ImGui.TextUnformatted(Loc.T("channels"));

        foreach (var (type, chKey, number) in Channels)
        {
            var label = number == 0 ? Loc.T(chKey) : $"{Loc.T(chKey)} {number}";
            var on = this.config.EnabledTypes.Contains(type);
            if (ImGui.Checkbox(label, ref on))
            {
                if (on) this.config.EnabledTypes.Add(type);
                else this.config.EnabledTypes.Remove(type);
                this.config.Save();
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted($"{Loc.T("version")} {VersionText}");
    }
}

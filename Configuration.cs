using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.Text;

namespace FrenchChatTranslator;

public enum Provider
{
    GoogleFree = 0,
    DeepLFree = 1,
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public Provider Provider { get; set; } = Provider.GoogleFree;
    public string DeepLApiKey { get; set; } = string.Empty;

    /// <summary>Langue de l'interface du plugin : "auto" (langue du jeu), "en", "fr", "de" ou "ja".</summary>
    public string UiLanguage { get; set; } = "auto";

    /// <summary>Code langue cible (fr, en, de, ja...).</summary>
    public string TargetLanguage { get; set; } = "fr";

    /// <summary>
    /// true : le message d'origine est remplacé par sa traduction.
    /// false : la traduction est affichée sur une ligne séparée sous l'original.
    /// </summary>
    public bool OverwriteOriginal { get; set; } = true;

    /// <summary>Préfixe les messages traduits de [EN→FR] (langue détectée → langue cible).</summary>
    public bool ShowTag { get; set; } = true;

    /// <summary>Codes ISO séparés par des virgules à ne jamais traduire (ex: "en,de").</summary>
    public string IgnoredLanguages { get; set; } = string.Empty;

    /// <summary>Messages plus courts que ça (en caractères) ne sont pas traduits.</summary>
    public int MinLength { get; set; } = 4;

    public HashSet<XivChatType> EnabledTypes { get; set; } = new()
    {
        XivChatType.Shout,
        XivChatType.Yell,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
        XivChatType.TellIncoming,
        XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
        XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
        XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4, XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8,
    };

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

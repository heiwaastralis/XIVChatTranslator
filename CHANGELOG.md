# Changelog

All notable changes to this plugin are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

To release a new version: change `<Version>` in `HeiwaChatTranslator.csproj`, add a new section at the top of this file, then rebuild.

## [Unreleased]

## [1.0.3.1] - 2026-10-18

### Fixed
- Native game chat bubbles could show unrelated messages (such as combat information) instead of the translated message. Messages are now translated in place when the translation arrives within 0.6 s, so the bubble gets the translated text. New option "Wait briefly for the translation" (on by default) in the settings.

## [1.0.2] - 2026-10-17

### Changed
- The plugin's internal name is now `HeiwaChatTranslator` (the name `ChatTranslator` made the plugin invisible in the Dalamud plugin installer). The displayed name is still "Chat Translator". The settings are reset on first load.
- The `/htranslator` window now converts accented and special letters to their basic Latin equivalent (e.g. `ł` becomes `l`, `é` becomes `e`, `ß` becomes `ss`), because the game chat does not accept them. Japanese text is kept as is, and typographic quotes and dashes become plain ASCII.

## [1.0.1-beta] - 2026-10-13

### Added
- Info icon (circled "i") next to "Service" in the settings. Hovering it compares Google (free, less accurate) and DeepL (more accurate, free up to 500,000 characters per month); clicking it opens a guide on creating a DeepL API key.
- Clickable link to the DeepL API key guide under the DeepL key field.
- `/htranslator` command: a window to translate your own messages. Choose the target language, the source language is detected automatically, and a button copies the translation to the clipboard. The window is localized like the rest of the plugin and nothing is sent to the game automatically.
- Plugin icon (`images/icon.png`), shown in the Dalamud plugin installer.
- Custom plugin repository (`repo.json`) so the plugin can be installed from the Dalamud installer.
- Support for DeepL paid (Pro) API keys in addition to free keys. The right DeepL server is chosen automatically from the key.

### Changed
- The "DeepL Free" service is now simply called "DeepL".
- The plugin's internal name is now `ChatTranslator` (was `FrenchChatTranslator`). The settings are reset on first load.
- Commands renamed: `/frtrad` is now `/htranslate` (settings, `/htranslate on|off`), and the former `/htranslate` translation window is now `/htranslator`.

## [1.0.0] - 2026-10-13

### Added
- Automatic language detection and translation of chat messages.
- Choice of the target language (French, English, German, Japanese, Spanish, Italian, Portuguese, Dutch, Polish, Russian, Turkish, Korean, Chinese simplified, Arabic).
- The translation replaces the original message in the chat (optional: display it on a separate line instead).
- Clickable `[EN→FR]` tag that shows the original message in a tooltip above the mouse.
- Per-channel selection (Say is disabled by default), ignored languages and minimum message length.
- Two translation services: Google Translate (no key) and DeepL Free (API key).
- Interface in English, French, German and Japanese, following the game language (English by default).
- `/frtrad` command to open the settings, `/frtrad on` and `/frtrad off` to toggle the translation.
- Version number displayed in the settings window.

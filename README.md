# Cult of the Lamb Save File Extractor and Loader

A BepInEx plugin that enables Cult of the Lamb `.json` save file editing

> [!WARNING]
>
> ## DISCLAIMER
>
> This plugin has the potential to create new, delete and corrupt your save files! Make sure that you are aware of the consequences before continuing with the installation of this plugin.
>
> If you encounter any issues that arise after modifying your save file, please make sure that you also load your save file in the un-modded game and make sure that you do not have other mods running alongside this plugin. Once you have confirmed that, you may [create an issue](https://github.com/osoclos/CotlSaveExtractorLoader/issues) on this repository first before the green light is given for you to report it as an official bug to Massive Monster.

## Prerequisites

- [BepInEx](https://docs.bepinex.dev/index.html) [[Installation Guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html)]

## Installation

You can either download the `.dll` file located in the `dist` folder or clone this repository using `git clone` and build the plugin yourself.

## Usage

After installing the plugin, you can begin running Cult of the Lamb like how you normally would.

Note that the extraction only takes place during saving; if you quit or exit to the main menu without saving, your save files will not be extracted.

After extraction, you can then modify the extracted `.json` file to your heart's content, as long as the file is syntactically correct. Upon modification, you can then load up said `.json` file and start playing using that file.

Warning: If you are already running Cult of the Lamb while modifying your save file, it is advised that you exit to the main menu without saving after modification to prevent any changes made to your extracted `.json` file being lost.

## Docs

This contains entry names and descriptions for this plugin.

### `ExtractSaveFiles` (default: `true`)

Enable extraction of save files.

### `ExtractedJsonSuffix` (default: `extracted`)

The string that will be appended after the filename to prevent overwriting of the default `slot_#.json` file. Leaving it empty will overwrite it.

### `ForceLoadJsonFiles` (default: `true`)

Whether to read the extracted `.json` save files instead of the `.mp` save files, if available.

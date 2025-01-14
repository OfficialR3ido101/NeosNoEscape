# NoEscape

A [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod for [Neos VR](https://neos.com/) that can disable the emergency respawn / disconnect gesture based on a bunch of settings.

The disabling can be turned on and off completely, and also remotely controlled via a cloud variable.
It can be configured when the controller vibrations stop to indicate that the respawn won't happen.
Also, disabling emergency disconnect can be separately disabled, and an override time can be set, after which the gestures will trigger even when otherwise disabled.

## Installation
1. Install [NeosModLoader](https://github.com/zkxs/NeosModLoader).
1. Place [NoEscape.dll](https://github.com/zkxs/NoEscape/releases/latest/download/NoEscape.dll) into your `nml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\nml_mods` for a default install. You can create it if it's missing, or if you launch the game once with NeosModLoader installed it will create the folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Neos logs.

## Why have you made this?
There are people who have various reasons for wanting the emergency respawn gesture disabled. In a world where this mod doesn't exist, their only option is to seek in-game exploits to achieve their goal. This presents a strong incentive to not report any security exploits they find. The primary motivation behind this mod is not to remove safety features—it's to disincentivize black-hat exploit research.

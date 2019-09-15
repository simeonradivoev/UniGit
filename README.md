# UniGit
An open source GIT Unity3D editor plugin.

[![GitHub release](https://img.shields.io/github/release/simeonradivoev/UniGit.svg)](https://github.com/simeonradivoev/UniGit/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://github.com/simeonradivoev/UniGit/blob/master/LICENSE.md)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=4A4LQGA69LQ5A)

![UniGit Icon](https://i.imgur.com/k63M0tG.png)

# Contents
* [Wiki](https://github.com/simeonradivoev/UniGit/wiki)
* [Features](#features)
* [Screenshots](#screenshots)
* [Installation](#installation)
* [Building](#building)
* [Asset Store](#asset-store)
* [Limitations](#limitations)
* [Not implemented yet](#not-implemented-yet)
* [Unity Thread](https://forum.unity3d.com/threads/opensource-unigit-in-editor-git-gui.440646/)

# [Features:](https://github.com/simeonradivoev/UniGit/wiki/Features-and-Usage)
* Pull, Push, Merge, Fetch changes
* Remote Management
* Secure Credentials Manager
* Project View status icons
* Open Source
* Conflict resolvent 
* Support for External programs like Tortoise Git
* Support for Credential Managers like Windows Credentials Manager
* (Beta) Support for Git LFS
* Multi-Threaded support
* Branch Switching and Creation
* In-Editor Diff Inspection
* Git Log Window
* Non Root Project Repositories
* Animated UI

For more info on all the features and how to use them, check out the [wiki](https://github.com/simeonradivoev/UniGit/wiki/Features-and-Usage).

# Screenshots
### History Window
![Git history window](https://i.imgur.com/ciX4Vdo.png)
### Diff Window
![Git Diff Window](https://i.imgur.com/EUWwd3L.png)
### Project View status overlays
![Project View Overlays](https://i.imgur.com/5YMjxjG.png)
### Diff Inspector
![Diff Inspector](https://i.imgur.com/xHO8AJD.png)
### Settings window
![Settings window](https://i.imgur.com/OcDCyEK.png)
### Git Log
![Git Log Window](https://i.imgur.com/sUUBBel.png)
### Blame Window
![Blame Window](https://i.imgur.com/33dmPAG.png)
### Sub Modules
![Sub Modules](https://i.imgur.com/tHSxZI8.png)

# Installation
In a unity project go to your `Packages` folder. Open `manifest.json` and add into the dependencies the following line: 

```
"uni-git": "https://github.com/simeonradivoev/UniGit.git"
```

It should look something like this:

```
{
    "dependencies": {
        "com.unity.ugui": "1.0.0",
        "com.unity.modules.ui": "1.0.0",
        "uni-git": "https://github.com/simeonradivoev/UniGit.git",
    } 
}
```

# Building
As of the new Unity Package system. There is no need to build UniGit into dlls. The new package system allows packages to be pulled directly from git and unity compiles all the source codes and generally keeps the package away from any project files. This is really convenient and allows for quick and easy updates. Images and resources also don't need to be packed in an assembly they can just be included in the package and be managed by unity.

# Asset store
As of version 1.5 the assets store is no longer supported because of the new package system. Check out the [Installation](#installation) guide to see how to include Uni git in your project

It may be re-added later down the line once the asset store is more tightly integrated with the package manager.
Older version can be found on the [Asset Store](http://u3d.as/Bxf)

## Notes
* UniGit is developed on a windows machine and has only been tested on a windows machine.

## Limitations:
* Inbuilt Credentials Manager works on Windows only, for now.
* Pushing only works with HTTP (libgit2sharp limitation)

## Not implemented yet
* Unity scene/prefab merging
* Rebasing (with inbuilt tools)
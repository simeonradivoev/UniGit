# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.5.1] - 2019-09-15
### Added
- New Changelog file

### Changed
- Some more file reordering

## [1.5.0] - 2019-09-15
### Added
- Support and resolved #27
- More to GitCommands
- Read-only check for commit message
- Help button to initialization window

### Removed
- Assembly image resources moved to normal in editor images

### Changed
- Updated to Unity 2019.2
- Unused imports
- Moved more windows to UIElements
- Moved to a package format
- Changed folder layout
- Removed Static project path helper and replaced it with assignable one.

### Fixed
- Settings file path
- Sub Module previewing
- Sub Module Project Overlays
- Settings tabs GUI getting called even if repo is invalid

## [1.4.3] - 2018-07-25 - Total performance
### Added
- Improved Performance on large and medium repositories significantly
- File commit message now has different paths for different modules
- Improved performance of project window overlays

## [1.4.2] - 2018-05-10
### Added
- Improved performance on large number of changes in Diff Window

### Changed
- Updated to Unity 2018.1

### Fixed
- #28

## [1.4.1] - 2018-03-28 - SubModules fixes
### Added
- More info and icons for Sub Modules
- Sub Module to Diff Window staging

### Fixed
- Asset Post Processing for Sub Modules

## [1.4.0] - 2018-03-27 - Sub Modules
### Added
- Support for sub module switching and updating
- Security improvements

### Fixed
- Default gitIgnore formatting

## [1.3.1] - 2018-02-15 - Unity
### Fixed
- #25

## [1.3.0] - 2018-02-01 - Caching and Performance
### Added
- Status data is now cached for maximum performance
- New System File Watching for folders that are outside Unity
- Showing multiple icons in project browser
- New Git Log window for git only notifications (+ option to use Unity's console)
- Added more animations (+option to disable all animations)
- Massive performance increase to Diff Inspector for large files
- Option to create missing folders for drifting .meta files

### Changed
- Lazy Mode now only governs if status should be refreshed on each assembly reload or entering/leaving playmode

### Fixed
- Editor Resources leaks
- Git History window errors when resizing it
- Line numbers in Diff Inspector
- #20
- #22
- #24

## [1.2.4] - 2017-12-27 - Lazy Mode
### Added
- Lazy Mode Update option
- Minor Performance improvements

### Changed
- Updated to Unity 2017.3

## [1.2.3] - 2017-12-19 - Minor Improvements
### Added
- Selections in Diff Window should now persist
- More informative text when updating
- Some GUI performance improvements
- Drag scrolling now possible in Diff Inspector

### Removed
- Thread Abort exceptions when using multi threaded update

### Changed
- Updated to Unity 2017.1.2

## [1.2.21] - 2017-08-10 - It's a Unity Thing
### Fixed
- Windows not initializing when entering Play mode
- Null errors when entering play mode

## [1.2.2] - 2017-08-08 - LibGit2Sharp hotfix
### Changed
- Reverted to LibGit2Sharp version 0.22 as version 0.24 causes some crashes
- Reverted to old version of GitLib2 because new one hangs for large repositories
- Removed Moq library from package and fixed AssetStoreTools sneaking into package.

## [1.2.1] - 2017-08-07 - Updated LibGit2Sharp
### Changed
- Updated LibGit2Sharp to version 0.24

### Fixed
- Repository Initialization problems
- Double Settings tabs in Settings Window

## [1.2.0] - 2017-08-05 - Threading
### Added
- In editor Diff Viewer
- Branch Switching
- Stashing
- Blaming
- Performance Improvements
- More threading options
- Made threading safer
- Warning indicator for files that have unstaged changes
- Status indicator to most windows when using async operations

### Removed
- The need for the 'EditorDefaultResources' folder

### Changed
- Updated to Unity 2017.1

## [1.1.0] - 2017-06-13 - Tiding Up
### Added
- A loading indicator
- About window for displaying version numbers
- Help buttons that lead to the wiki page
- Update notification icon for when using multi threads

### Changed
- Setting and Credentials now stored in .git folder
- "Editor Default Resources" folder is no longer used and can be removed
- Old settings and credentials will be automatically converted and can be safely removed
- Replaced all Scriptable Object dependencies with Json ones
- Git-Settings, Git-Credentials and Commit Message File are now stored in the .git folder and will not interfere with unity or git
- All icons moved to a Resource only DLL. All assets in folder "Editor Default Resources" will not longer be needed
- Git Status icon handling moved to GitOverlay class

## [1.0.9] - 2017-04-07 - Branch Creation
### Added
- Difference to meta files can now be shown
- Implemented Branch creation/removal
- Can now add credentials with empty password so that passwords stored in credential managers are not overwritten
- Updated UniGit to Unity 5.6

### Fixed
- Merged commit message now updated if using file.
- Bugfixes

## [1.0.8] - 2017-03-03 - File commit message
### Added
- Commit message can now be used from a file.
- More help tooltips and texts.

### Fixed
- #7

## [1.0.7] - 2016-12-22 - Sorting fixes
### Added
- Implemented Sorting on Diff Window files (by name, modification date, creation date, path)
- Diff Window commit section improvements
- Commit message now saves in a file (will almost never be lost)

### Removed
- Removed forgotten Debug messages

### Fixed
- Assembly reload locking
- A bunch of null errors

## [1.0.6] - 2016-12-11 - Hotfixes
### Changed
- External programs now sorted (so that my favorite tortoisegit is first)

### Fixed
- File diff icon errors
- Search filter errors in diff window

## [1.0.5] - 2016-12-07 - Partial Status Updates
### Added
- Partial Git status updates (increases performance a lot when modifying and handling individual files)
- Commit message text field can now be minimized
- Jump to file shortcut in commit detail window
- Option for built in avatars (First Letter of name)
- Option to disable Gavatar
- Downloaded avatars from Gavatar are now cached (this will lower the number of times avatars need to be downloaded)

### Removed
- Removed Profilers and left over Debug messages

## [1.0.4] - 2016-12-04 - Updated to Unity 5.5
### Added
- Updated to unity 5.5

### Removed
- Removed auto fetching on play

## [1.0.3] - 2016-11-27 - More threaded implementations
### Added
- Fully implemented threading
- Git LFS now works with threads
- Made threading faster and safer
- Added option to disable threaded Git status retrieval

## [1.0.2] - 2016-11-15
### Added
- Added initial values for Diff Window's filter

### Fixed
- Fixed Repository initialization errors described here

## [1.0.1] - 2016-11-11
### Added
- New Package Exporter

### Fixed
- Minor bug fixes

## [1.0.0] - 2016-11-08
### Added
- First UniGit release. It includes all dependency and UniGit DLL libraries needed.
- Extract either Debug.zip or Release.zip into your project's Assets folder.

[Unreleased]: https://github.com/simeonradivoev/UniGit/compare/v1.5.1...HEAD
[1.5.1]: https://github.com/simeonradivoev/UniGit/compare/v1.5.0...1.5.1
[1.5.0]: https://github.com/simeonradivoev/UniGit/compare/v1.4.3...1.5.0
[1.4.3]: https://github.com/simeonradivoev/UniGit/compare/v1.4.2...v1.4.3
[1.4.2]: https://github.com/simeonradivoev/UniGit/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/simeonradivoev/UniGit/compare/v1.4...v1.4.1
[1.4.0]: https://github.com/simeonradivoev/UniGit/compare/v1.3.1...v1.4
[1.3.1]: https://github.com/simeonradivoev/UniGit/compare/v1.3...v1.3.1
[1.3.0]: https://github.com/simeonradivoev/UniGit/compare/v1.2.4...v1.3
[1.2.4]: https://github.com/simeonradivoev/UniGit/compare/v1.2.3...v1.2.4
[1.2.3]: https://github.com/simeonradivoev/UniGit/compare/v1.2.21...v1.2.3
[1.2.21]: https://github.com/simeonradivoev/UniGit/compare/v1.2.2--hotfix...v1.2.21
[1.2.2]: https://github.com/simeonradivoev/UniGit/compare/v1.2.1...v1.2.2--hotfix
[1.2.1]: https://github.com/simeonradivoev/UniGit/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/simeonradivoev/UniGit/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/simeonradivoev/UniGit/compare/v1.0.9...v1.1.0
[1.0.9]: https://github.com/simeonradivoev/UniGit/compare/v1.0.8...v1.0.9
[1.0.8]: https://github.com/simeonradivoev/UniGit/compare/v1.0.7...v1.0.8
[1.0.7]: https://github.com/simeonradivoev/UniGit/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/simeonradivoev/UniGit/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/simeonradivoev/UniGit/compare/v1.0.4...v1.0.5
[1.0.4]: https://github.com/simeonradivoev/UniGit/compare/v1.0.3...v1.0.4
[1.0.3]: https://github.com/simeonradivoev/UniGit/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/simeonradivoev/UniGit/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/simeonradivoev/UniGit/compare/v1.0...v1.0.1
[1.0.0]: https://github.com/simeonradivoev/UniGit/releases/tag/v1.0
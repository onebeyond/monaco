![Logo Monaco](monaco-transp.png)
-

[![Nuget version](https://img.shields.io/nuget/v/Monaco.Template?style=plastic)](https://www.nuget.org/packages/Monaco.Template)
[![Nuget downloads](https://img.shields.io/nuget/dt/Monaco.Template?style=plastic)](https://www.nuget.org/packages/Monaco.Template)
[![License](https://img.shields.io/github/license/OneBeyond/monaco?style=plastic)](LICENSE.TXT)
[![Release to NuGet](https://github.com/onebeyond/monaco/actions/workflows/release.yml/badge.svg)](https://github.com/onebeyond/monaco/actions/workflows/release.yml)

# Introduction
Monaco is meant to be a set of .NET templates for different platforms, such as a backend with a REST API, a Blazor WASM webapp or a .NET MAUI desktop/mobile app (the last 2 are planned [here](https://github.com/onebeyond/monaco/milestone/1) and [here](https://github.com/onebeyond/monaco/milestone/2)), as well as other individual files templates, all in order to help accelerate the development of new projects with a flexible and easy to understand architecture.

The backend solution is based on the [Vertical Slices Architecture](https://www.youtube.com/watch?v=SUiWfhAhgQw). It ships the most basic structure required to run a REST API with EF Core and a rich model Domain, along with unit tests to cover the existing boilerplate.

Each of the different solution templates also provide some basic business components as example of real life implementation logic.

# Getting Started

### Supported .NET version:

9.0

### Installation

```console
dotnet new install Monaco.Template
```

### Uninstalling

```console
dotnet new uninstall Monaco.Template
```

### How to create a Monaco based solution

For generating a new backend solution, you can run the following command:

```console
dotnet new monaco-backend-solution -n MyFirstSolution
```

This will create a folder named `MyFirstSolution`, which will contain a structure of directories prefixed with the name as part of the namespace declaration. The resulting solution will include the default layout and all the files required to run the application (more info about this [here](https://github.com/onebeyond/monaco/wiki/Solution-projects-structure))

From there, is enough to configure `appsettings.json` with the required settings and run the app.

### Getting help about template's options

```console
dotnet new monaco-backend-solution --help
```

(For more information about Monaco options please refer [here](https://github.com/onebeyond/monaco/wiki/Template-options))

# Documentation

For more detailed documentation, please refer to our [Wiki](https://github.com/onebeyond/monaco/wiki)

# Visual Studio support

From version 2.4.0 Monaco has stopped supporting Visual Studio for the template `backend-solution-template` and it won't show up on its templates list until further notice due to the existing issues in VS to generate projects with this kind of template.

The experience offered on Visual Studio was subpar as the output generated from within the IDE excluded all the solution folders that Monaco intended to create by default and the file system folders structure was also generated incorrectly; while everything works as expected when ran from the CLI. Because of all this, we decided to leave the CLI as the only valid alternative for using Monaco as it's the only one that guarantees a correct structure both in the file system and in the solution folders generated.

# Contributing

If you want to contribute, we are currently accepting PRs and/or proposals/discussions in the issue tracker.

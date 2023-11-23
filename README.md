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

8.0

### Installation

`dotnet new install Monaco.Template`

### Uninstalling

`dotnet new uninstall Monaco.Template`

### How to create a Monaco based solution

For generating a new backend solution, you can run the following command:

`dotnet new monaco-backend-solution -n MyFirstSolution`

This will create a folder named `MyFirstSolution`, which will contain a structure of directories prefixed with the name as part of the namespace declaration. The resulting solution will include the default layout and all the files required to run the application (more info about this [here](https://github.com/onebeyond/monaco/wiki/Solution-projects-structure))

From there, is enough to configure `appsettings.json` with the required settings and run the app.

### Getting help about template's options

`dotnet new monaco-backend-solution --help`

(For more information about Monaco options please refer [here](https://github.com/onebeyond/monaco/wiki/Template-options))

# Documentation

For more detailed documentation, please refer to our [Wiki](https://github.com/onebeyond/monaco/wiki)

# Visual Studio support

Monaco provides support for generating the solution and projects from Visual Studio as well as providing the UI interface for configuring the project generation options.

However, it's not recommended to use VS for generating new solutions. Monaco provides a default layout for the solution with the projects organized in different folders and some solution files already included in it, but this default layout from the template is ignored by VS, which also creates an additional level of folders in the generated folder. Because of these behaviors we strongly recommend using the console for running Monaco and generating any new solutions.


# Contributing

If you want to contribute, we are currently accepting PRs and/or proposals/discussions in the issue tracker.

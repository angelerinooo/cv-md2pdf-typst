# MdCvConverter

A C# console app that converts Markdown CV files into PDF through a Typst template pipeline.

## What it does

- Reads a Markdown input file.
- Converts Markdown to basic Typst markup.
- Wraps the generated body with a Typst CV template.
- Calls the Typst CLI to compile PDF.

## Prerequisites

- .NET 10 SDK
- Fontawesome installation (version used: 7.2.0)
- Typst CLI installed and available on PATH (`typst --version`)

## Project layout

- `src/MdCvConverter` - console app
- `templates/default` - default Typst CV template files
- `sample/example-cv.md` - sample CV markdown input

## Usage

```powershell
dotnet run --project src/MdCvConverter --input <path/to/input.md> --output <path/to/output.pdf> --template <path/to/typst/template.typ>
```

### Lavandula template

The repository includes the Lavandula template under `templates/lavandula`.

Use it like this:

```powershell
dotnet run --project src/MdCvConverter --input sample/example-cv.md --output output/example-lavandula.pdf --template templates/lavandula/cv.typ
```

### Distributable package

A distributable package can be produced from the repository root with:

```powershell
.\publish.ps1
```

By default, this publishes a self-contained `win-x64` package. To target Linux or macOS, pass a supported runtime identifier:

```powershell
.\publish.ps1 -RuntimeIdentifier linux-x64
.\publish.ps1 -RuntimeIdentifier linux-arm64
.\publish.ps1 -RuntimeIdentifier osx-x64
.\publish.ps1 -RuntimeIdentifier osx-arm64
```

The script publishes a self-contained package that includes the app executable, templates, and assets. On Windows it creates a ZIP package. On Linux and macOS it creates a TAR.GZ package.

> Note: The package still requires the `typst` CLI to be installed and available on `PATH` at runtime.

### Font Awesome 7

Font Awesome 7 Free Desktop fonts were added under `assets/fonts/fontawesome-free-7.2.0-desktop`.
The converter automatically passes `assets/fonts` to Typst via `--font-path`.

### Repository assets in templates

Templates can reference files from the repository-level `assets` folder with `{{ASSETS_ROOT}}`.

Example:

```typ
#let asset-path(path) = "{{ASSETS_ROOT}}" + "/" + path
#image(asset-path("icons/pt_flag.png"))
```

### Sidebar components from markdown

You can define multiple sidebar components in the markdown file using repeated `## Sidebar: <Title>` sections.
These sections are rendered into the template `{{SIDEBAR_COMPONENTS}}` placeholder and excluded from the main body.

Example:

```md
## Sidebar: About me
Just working.

## Sidebar: Skills
- Things
```

For Lavandula-style skill groups in a sidebar section:

```md
## Sidebar: Technical skills
### Skill-Group: Backend Development [icon:python]
- C#
- .NET
- REST APIs

### Skill-Group: Cloud & DevOps [icon:cloud,solid]
- Azure
- CI/CD
```

You can also attach Lavandula icons to sidebar items:

```md
## Sidebar: Contacts
- [icon:at,solid] example@example.com
- [icon:linkedin] linkedin.com/in/example-profile
- [icon:phone,solid] (+000) 123 456 789
```

For language/flag-style components, use skill level lines with assets:

```md
## Sidebar: Languages
- [level:100,asset:icons/pt_flag.png] Portuguese
- [level:90,asset:icons/uk_flag.png] English
```

### Body sections and section-elements from markdown

Use `##` headings as Lavandula sections and `###` headings as section-elements.
You can optionally append an info value after `|`.

```md
## Experience
### Experienced Thing-Doer, Company Pro Max | 2022-Present
Did very important things.
- [icon:rocket] Improved things by 367%.

### Junior Thing-Doer, Company | 2018-2022
Improved a lot in doing things.
```

## Notes

- The app writes an intermediate `.typ` file next to the output PDF.
- If Typst is missing, the app exits with a clear error message.
- Run `--help` or `-h` to print CLI usage.
- Relative `#import` and `#include` paths in templates are resolved against the template file location.
- The default template is a starter and can be customized for your CV branding.

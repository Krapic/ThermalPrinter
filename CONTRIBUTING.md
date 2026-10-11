# Contributing

Use Windows and the .NET 8 SDK. Keep documentation, UI text, and comments in English. Preserve Unicode samples that test encoding and rendering.

Before submitting changes:

```powershell
dotnet build BT583Printer.sln -c Release
dotnet test BT583Printer.sln -c Release --no-build
```

For packaging changes, also run `./publish.ps1 -Test`. Tests use temporary data directories and send no print jobs. Keep hardware checks manual and report the model, firmware, transport, and settings.

Do not commit build output, releases, local SDKs, personal settings, templates, history, or logs. Distribute binaries through GitHub Releases. Update documentation when behavior changes.

## Initial GitHub upload

Initialize the repository, review staged files, and commit:

```powershell
git init -b main
git add .
git diff --cached --stat
git commit -m "Prepare BT-583 Studio for GitHub"
```

Create an empty GitHub repository, replace the URL placeholder, and push:

```powershell
git remote add origin YOUR_GITHUB_REPOSITORY_URL
git push -u origin main
```

Choose a project license before inviting reuse. Third-party notices do not license this application's own code.

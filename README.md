<h1 align="center">
  <br>
  <img src="https://github.com/Haruki1707/EZ_Updater/blob/main/EZ_Updater/EZ%20Updater.png?raw=true" alt="yuzu" width="200">
  <br>
  <b>EZ_Updater</b>
  <br>
</h1>

<h4 align="center"><b>EZ_Updater</b> Is a updater for standalone .NET apps that relies on GitHub releases.
<br>
Supports exe and zip files (which can contain multiple files).
</h4>

<p align="center">
    <a href="https://www.nuget.org/packages/EZ_Updater/">
        <img src="https://img.shields.io/badge/NuGet Package-EZ__Updater-blue?style=for-the-badge&logo=NuGet"
            alt="NuGet Package">
    </a>
</p>

# NuGet Package Install
````powershell
PM> Install-Package EZ_Updater
````

## Supported .NET versions
* .NET Framework 4.7.1 or above
* .NET Core 3.1
* .NET 5.0 or above

## How it works
EZ_Updater checks latest GitHub release on the repository specified, if tag version is supperior to your program **File Version** it downloads the asset that match your programname.exe. In case you want to download other file like a zip, you should specifie it.

## Current version detection
EZ_Updater uses File version to determine the current version of the application. You can update it by going to Properties of the project.
SCREENSHOT

## GitHub release version
GitHub tag should be like (Major).(Minor).(Patch).(Build)
Is not obligatory to user Minor, Patch and Build; there could be any letter or word on the tag name.
Examples:
    Tag name: 1.0
    Tag name: v1.0.0
    Tag name: v1.0.0.0-beta

## Usage example for GUI on WinForms and WPF
##### It can be used in console as well
<br>

Add a reference to
```csharp
using EZ_Updater;
```

Now on your form or window load you could do something like
```csharp
private async void Form_Loaded(object sender, RoutedEventArgs e)
{
    Updater.GitHub_User = "Your GitHub User Name";
    Updater.GitHub_Repository = "Your GitHub Repository";

    if (await Updater.CheckUpdateAsync() && MessageBox.Show("Update available\nDo you want to update?", "Confirmation", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
    {
        new UpdateForm()
        {
            Owner = this
        }.ShowDialog();
    }
}
```
To force the update don't use the MessageBox
<br><br>
There also exist a synchronous CheckUpdate if you are interested in
<br><br>
### Update Form (WinForms)
Add a reference to
```csharp
using EZ_Updater;
```

I recommend having a Button hiden for UpdateForm closing and a ProgressBar

In your form constructor you could do something like
```csharp
Closebutton.Visible = false;
Updater.Update(UIChange);
```

If you want to download other asset rather than yourprogram.exe then you should
```csharp
Closebutton.Visible = false;
Updater.GitHub_Asset = "YourAsset.zip";
Updater.Update(UIChange);
```

For the method sent in Updater.Update I recommend to be like this
```csharp
private void UIChange(object sender, EventArgs e)
{
    Messagelabel.text = Updater.Message;
    progressBar.Value = Updater.ProgressPercentage;

    switch (Updater.State)
    {
        case UpdaterState.Canceled:
        case UpdaterState.InstallFailed:
            Closebutton.Visible = true;
            break;
        case UpdaterState.Installed:
            Application.Restart();
            break;
    }
}
```

### Update Window (WPF)
Add a reference to
```csharp
using EZ_Updater;
```

I recommend having a Button hiden for UpdateForm closing and a ProgressBar

In your form constructor you could do something like
```csharp
Closebutton.Visibility = Visibility.Hidden;
Updater.Update(UIChange);
```

If you want to download other asset rather than yourprogram.exe then you should
```csharp
Closebutton.Visibility = Visibility.Hidden;
Updater.GitHub_Asset = "YourAsset.zip";
Updater.Update(UIChange);
```

For the method sent in Updater.Update I recommend to be like this
```csharp
private void UIChange(object sender, EventArgs e)
{
    Messagelabel.Content = Updater.Message;
    progressBar.Value = Updater.ProgressPercentage;

    switch (Updater.State)
    {
        case UpdaterState.Canceled:
        case UpdaterState.InstallFailed:
            Closebutton.Visibility = Visibility.Visible;
            break;
        case UpdaterState.Installed:
            Process.Start(Updater.ProgramFileName);
            Application.Current.Shutdown();
            break;
    }
}
```

## Documentation
EZ_Updater has all the properties, attributes, methods and events listed below
```csharp
Updater.GitHub_User //To get or set the GitHub user
Updater.GitHub_Repository //To get or set the GitHub repository
Updater.GitHub_Asset //To get or set the GitHub asset to be downloaded
Updater.Message //To get the a message to show on GUI or Console
Updater.State //To get the current state of the Updater
Updater.ProgressPercentage //To get current Percentage of Download progress or Install progress
Updater.ReleaseName //To get the latest GitHub release name (or title)
Updater.ReleaseBody //To get the latest GitHub release body text
Updater.ReleaseVersion //To get the latest GitHub release version (without letter)
Updater.ProgramFileName //To get the current program file name
Updater.ProgramFileVersion //To get the current program file version
Updater.GUI_Context //To set your GUI Synchronization context
Updater.DownloadRetryCount //To get the count of retries when downloading (MAX: 4)
Updater.CustomLogger //To set a method to be called by EZ_Updater for custom logging

Updater.CheckUpdate() //To get if update is available (sync)
Updater.CheckUpdate("user", "repo") //To get if update is available while stablishing user and repo (sync)
Updater.CheckUpdateAsync() //To get if update is available (Async)
Updater.CheckUpdateAsync("user", "repo") //To get if update is available while stablishing user and repo (Async)
Updater.Update() //To update even if there is no Update available
Updater.Update(method) //To update calling UpdaterChange Event
Updater.Update(method, method, method, method, method, method) //To update calling DownloadProgress, DownloadCanceled, RetryDownload, UpdateProgress, UpdateFailed and UpdateFinished Events respectively (method can arg be null)
Updater.TryToCleanEvents //Method that tries to clean all the events listed below

Updater.DownloadProgress //Event to be called when download make a progress
Updater.DownloadCanceled //Event to be called when download is canceled
Updater.RetryDownload //Event to be called when download is retried
Updater.UpdateProgress //Event to be called when update make a progress
Updater.UpdateFailed //Event to be called when update fails
Updater.UpdateFinished //Event to be called when update finishes
Updater.UpdaterChange //Event to be called when any of the above events is called
```
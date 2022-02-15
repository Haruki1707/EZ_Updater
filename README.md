<h1 align="center">
  <br>
  <a href="https://www.nuget.org/packages/EZ_Updater/">
    <img src="https://github.com/Haruki1707/EZ_Updater/blob/main/EZ_Updater/EZ%20Updater.png?raw=true" width="200">
  </a>
  <br>
  <b>EZ_Updater</b>
  <br>
</h1>

<h4 align="center"><b>EZ_Updater</b> is an updater for standalone .NET apps that relies on GitHub releases.
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
EZ_Updater checks latest GitHub release on the repository specified, it returns a bool if the tag version is higher than your program **File Version** returns true else false. You can download the asset that matches your programname.exe. In case you want to download other file like a zip, you should specify it.

## Current version detection
EZ_Updater uses File version to determine the current version of the application. You can update it by going to Properties of the project.

## GitHub release version
GitHub tag can follow this format (Major).(Minor).(Patch).(Build)
It´s not obligatory to use Minor, Patch and Build.
There could be any letter or word on the tag name.
Examples:
* Tag name: 1.0
* Tag name: v1.0.0
* Tag name: v1.0.0.0-beta

## Usage example for GUI on WinForms and WPF
##### It can be used in console as well
&nbsp;
### WinForms
Add a reference to
```csharp
using EZ_Updater;
```

Now on your form load you could do something like
```csharp
private async void Form_Loaded(object sender, EventArgs e)
{
    Updater.GitHub_User = "Your GitHub User Name";
    Updater.GitHub_Repository = "Your GitHub Repository";
    Updater.OriginalFileName = "Your .exe (or whatever) file name";

    if (await Updater.CheckUpdateAsync() && MessageBox.Show("Update available\nDo you want to update?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
    {
        if(Updater.CannotWriteOnDir)
            MessageBox.Show("Application cannot update in current directory, consider moving it to another folder or executing with Admin rights", "Alert");
        else
        {
            new UpdateForm()
            {
                Owner = this
            }.ShowDialog();
        }
    }
}
```
To force the update don't use the MessageBox
There also exist a synchronous CheckUpdate if you are interested in
### Update Form (WinForms)
Add a reference to
```csharp
using EZ_Updater;
```

I recommend having a Button hidden for UpdateForm closing and a ProgressBar

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

For the method sent in Updater.Update is recommended to be like this
```csharp
private void UIChange(object sender, EventArgs e)
{
    Messagelabel.Text = Updater.Message;
    progressBar.Value = Updater.ProgressPercentage;

    switch (Updater.ShortState)
    {
        case UpdaterShortState.Canceled:
            Closebutton.Visible = true;
            break;
        case UpdaterShortState.Installed:
            Application.Restart();
            break;
    }
}
```

### WPF
Add a reference to
```csharp
using EZ_Updater;
```

Now on your window load you could do something like
```csharp
private async void Window_Loaded(object sender, RoutedEventArgs e)
{
    Updater.GitHub_User = "Your GitHub User Name";
    Updater.GitHub_Repository = "Your GitHub Repository";
    Updater.OriginalFileName = "Your .exe (or whatever) file name";

    if (await Updater.CheckUpdateAsync() && MessageBox.Show("Update available\nDo you want to update?", "Confirmation", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
    {
        if(Updater.CannotWriteOnDir)
            MessageBox.Show("Application cannot update in current directory, consider moving it to another folder or executing with Admin rights", "Alert");
        else
        {
            new UpdateForm()
            {
                Owner = this
            }.ShowDialog();
        }
    }
}
```
To force the update don't use the MessageBox
There also exist a synchronous CheckUpdate if you are interested in
### Update Window (WPF)
Add a reference to
```csharp
using EZ_Updater;
```

I recommend having a Button hidden for UpdateForm closing and a ProgressBar

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

For the method sent in Updater.Update is recommended to be like this
```csharp
private void UIChange(object sender, EventArgs e)
{
    Messagelabel.Content = Updater.Message;
    progressBar.Value = Updater.ProgressPercentage;

    switch (Updater.ShortState)
    {
        case UpdaterShortState.Canceled:
            Closebutton.Visibility = Visibility.Visible;
            break;
        case UpdaterShortState.Installed:
            Process.Start(Updater.ProgramFileName);
            Application.Current.Shutdown();
            break;
    }
}
```

## Documentation
EZ_Updater has all the properties, attributes, methods and events listed below
```csharp
Updater.GitHub_User //To get or set the GitHub user | Required
Updater.GitHub_Repository //To get or set the GitHub repository | Required
Updater.OriginalFileName //To set the .exe original name | Required
Updater.CannotWriteOnDir //Bool to check if your program can write in current directory | Should check
Updater.KeepOriginalFileName //To rename .exe to original name even if user changes the name | Default: false
Updater.GitHub_Asset //To get or set the GitHub asset to be downloaded | Default: OriginalFileName
Updater.Message //To get a message to show on GUI or Console
Updater.State //To get the current state of the Updater
Updater.ShortState //To get if Updater is Idling, Updating, Canceled or Installed (Short version of Updater.State)
Updater.ProgressPercentage //To get current percentage of Download progress or Install progress
Updater.ReleaseName //To get the latest GitHub release name (or title)
Updater.ReleaseBody //To get the latest GitHub release body text
Updater.ReleaseVersion //To get the latest GitHub release version (without letters)
Updater.ProgramFileName //To get the current program file name
Updater.ProgramFileVersion //To get the current program file version
Updater.GUI_Context //To set your GUI Synchronization context
Updater.DownloadRetryCount //To get the count of retries when downloading (MAX: 4)
Updater.CustomLogger //To set a method to be called by EZ_Updater for custom logging
Updater.LogInterfix //Interfix that joins "EZ_Updater" with the message on logging

Updater.CheckUpdate() //To get if an update is available (sync)
Updater.CheckUpdate("user", "repo") //To get if an update is available while establishing user and repo (sync)
Updater.CheckUpdateAsync() //To get if an update is available (Async)
Updater.CheckUpdateAsync("user", "repo") //To get if an update is available while establishing user and repo (Async)
Updater.Update() //To update, even though if there is no Update available
Updater.Update(method) //To update calling UpdaterChange Event
Updater.Update(method, method, method, method, method, method) //To update calling DownloadProgress, DownloadCanceled, RetryDownload, UpdateProgress, UpdateFailed and UpdateFinished Events respectively (method args can be null)
Updater.TryToCleanEvents() //Method that tries to clean all the events listed below

Updater.DownloadProgress //Event to be called when download has progressed
Updater.DownloadCanceled //Event to be called when download is canceled
Updater.RetryDownload //Event to be called when download is retried
Updater.UpdateProgress //Event to be called when update has progressed
Updater.UpdateFailed //Event to be called when update fails
Updater.UpdateFinished //Event to be called when update finishes
Updater.UpdaterChange //Event to be called when any of the above events is called
```
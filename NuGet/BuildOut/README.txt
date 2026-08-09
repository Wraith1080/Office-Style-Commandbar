Local NuGet feed for CommandBars.Package.

Building the CommandBars.Package project drops a freshly versioned .nupkg here;
CommandBars.PackageDemo restores it from this folder (see NuGet.config at the
solution root). This folder must exist for restore to succeed — keep this file.

Old package versions accumulate here; it is safe to delete *.nupkg files (keep
the newest one, or simply rebuild CommandBars.Package to produce a new one).
